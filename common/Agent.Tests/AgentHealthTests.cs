using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace Agent.Tests;

sealed class AgentHealthTests {
    [Test]
    public void HealthyRoundTripPasses() {
        var now = DateTimeOffset.Parse("2026-09-09T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status(now, "healthy", now.AddSeconds(-20), now.AddSeconds(-1)), now);

        Assert.That(exitCode, Is.Zero);
    }

    [Test]
    public void StaleRoundTripFails() {
        var now = DateTimeOffset.Parse("2026-09-09T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status(now, "healthy", now.AddSeconds(-31), now), now);

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [TestCase("starting")]
    [TestCase("connected")]
    [TestCase("reconnecting")]
    public void ConnectionStatePassesWithinGrace(string state) {
        var now = DateTimeOffset.Parse("2026-09-09T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status(now.AddSeconds(-119), state, null, now), now);

        Assert.That(exitCode, Is.Zero);
    }

    [TestCase("starting")]
    [TestCase("connected")]
    [TestCase("reconnecting")]
    public void ConnectionStateFailsBeyondGrace(string state) {
        var now = DateTimeOffset.Parse("2026-09-09T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status(now.AddSeconds(-121), state, null, now), now);

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void StaleHeartbeatFailsDuringGrace() {
        var now = DateTimeOffset.Parse("2026-09-09T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status(now.AddSeconds(-10), "reconnecting", null, now.AddSeconds(-31)), now);

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void StatusFromDifferentProcessFails() {
        var now = DateTimeOffset.Parse("2026-09-09T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status(now, "healthy", now, now), now, false);

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void MissingStatusFails() {
        string path = Path.Combine(Path.GetTempPath(), $"missing-agent-health-{Guid.NewGuid():N}");
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);

        int exitCode = AgentHealth.Run(
            path,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(120),
            TimeSpan.FromSeconds(30),
            (_, _) => true,
            output,
            error
        );

        Assert.Multiple(() => {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(error.ToString(), Does.Not.Contain(path));
        });
    }

    [Test]
    public void MalformedStatusDoesNotLeakItsContents() {
        const string secret = "highly-sensitive-value";
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        string path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, $"version=1\ndiagnostic={secret}\ndiagnostic=duplicate\n");
            int exitCode = AgentHealth.Run(
                path,
                DateTimeOffset.UtcNow,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(30),
                (_, _) => true,
                output,
                error
            );

            Assert.Multiple(() => {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(output.ToString(), Does.Not.Contain(secret));
                Assert.That(error.ToString(), Does.Not.Contain(secret));
            });
        } finally {
            File.Delete(path);
        }
    }

    static int Run(string status, DateTimeOffset now, bool processMatches = true) {
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        string path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, status);
            return AgentHealth.Run(
                path,
                now,
                TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(30),
                (_, _) => processMatches,
                output,
                error
            );
        } finally {
            File.Delete(path);
        }
    }

    static string Status(
        DateTimeOffset stateSince,
        string state,
        DateTimeOffset? lastSuccess,
        DateTimeOffset updated
    ) {
        return $"""
            version=1
            pid=123
            processStart=1788955200000
            state={state}
            stateSince={stateSince.ToUnixTimeMilliseconds()}
            lastSuccess={lastSuccess?.ToUnixTimeMilliseconds() ?? 0}
            updated={updated.ToUnixTimeMilliseconds()}
            diagnostic=none
            """;
    }
}
