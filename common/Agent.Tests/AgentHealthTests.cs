using System;
using System.Globalization;
using System.IO;
using NUnit.Framework;

namespace Agent.Tests;

sealed class AgentHealthTests {
    [Test]
    public void FreshRoundTripPasses() {
        var now = DateTimeOffset.Parse("2026-09-11T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status("healthy", now.AddSeconds(-1), now), now);

        Assert.That(exitCode, Is.Zero);
    }

    [TestCase("starting")]
    [TestCase("connected")]
    [TestCase("reconnecting")]
    [TestCase("terminal")]
    public void NonHealthyStateFails(string state) {
        var now = DateTimeOffset.Parse("2026-09-11T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status(state, null, now), now);

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void StaleRoundTripFails() {
        var now = DateTimeOffset.Parse("2026-09-11T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status("healthy", now.AddSeconds(-31), now), now);

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void StatusFromDifferentProcessFails() {
        var now = DateTimeOffset.Parse("2026-09-11T12:00:00Z", CultureInfo.InvariantCulture);
        int exitCode = Run(Status("healthy", now, now), now, false);

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void MissingStatusFails() {
        string path = Path.Combine(Path.GetTempPath(), $"missing-agent-health-{Guid.NewGuid():N}");
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        int exitCode = AgentHealth.Run(
            path,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(30),
            (_, _) => true,
            output,
            error
        );

        Assert.That(exitCode, Is.EqualTo(1));
    }

    [Test]
    public void MalformedStatusDoesNotLeakItsContents() {
        const string secret = "highly-sensitive-value";
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        string path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, $"version=1\ndiagnostic={secret}\ndiagnostic=duplicate\n");
            int exitCode = AgentHealth.Run(
                path,
                DateTimeOffset.UtcNow,
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

    [TestCase("", "agent-health.status")]
    [TestCase("   ", "agent-health.status")]
    [TestCase("custom.status", "custom.status")]
    public void StatusPathUsesConfiguredOverride(string configured, string expected) {
        Assert.That(
            AgentHealth.StatusPath("agent-directory", configured),
            Is.EqualTo(configured == "custom.status" ? expected : Path.Combine("agent-directory", expected))
        );
    }

    static int Run(string status, DateTimeOffset now, bool processMatches = true) {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        string path = Path.GetTempFileName();
        try {
            File.WriteAllText(path, status);
            return AgentHealth.Run(
                path,
                now,
                TimeSpan.FromSeconds(30),
                (_, _) => processMatches,
                output,
                error
            );
        } finally {
            File.Delete(path);
        }
    }

    static string Status(string state, DateTimeOffset? lastSuccess, DateTimeOffset updated) {
        return $"""
                version=1
                pid=123
                processStart=1789128000000
                state={state}
                stateSince={updated.ToUnixTimeMilliseconds()}
                lastSuccess={lastSuccess?.ToUnixTimeMilliseconds() ?? 0}
                updated={updated.ToUnixTimeMilliseconds()}
                diagnostic=none
                """;
    }
}