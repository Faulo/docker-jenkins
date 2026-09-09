using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Agent.Tests;

sealed class AgentEnvironmentTests {
    static readonly string[] names = [
        AgentEnvironment.WEB_SOCKET,
        AgentEnvironment.HEALTH_FILE,
        AgentEnvironment.HEALTH_GRACE_SECONDS,
        AgentEnvironment.HEALTH_STALE_SECONDS,
        AgentEnvironment.HEALTH_INTERVAL_SECONDS,
        AgentEnvironment.HEALTH_TIMEOUT_SECONDS
    ];

    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("   ", true)]
    [TestCase("1", true)]
    [TestCase("true", true)]
    [TestCase(" TrUe ", true)]
    [TestCase("0", false)]
    [TestCase("false", false)]
    [TestCase(" FaLsE ", false)]
    public void ReadBooleanAcceptsDocumentedValues(string? value, bool expected) {
        Assert.That(AgentEnvironment.ReadBoolean(AgentEnvironment.WEB_SOCKET, value, true), Is.EqualTo(expected));
    }

    [TestCase("yes")]
    [TestCase("enabled")]
    [TestCase("ture")]
    public void ReadBooleanRejectsUnknownValuesWithoutRepeatingThem(string value) {
        var exception = Assert.Throws<ConfigurationException>(() =>
            AgentEnvironment.ReadBoolean(AgentEnvironment.WEB_SOCKET, value, true)
        );

        Assert.That(
            exception!.Message,
            Does.Contain(AgentEnvironment.WEB_SOCKET)
                .And.Contain("true, false, 1, or 0")
                .And.Not.Contain(value)
        );
    }

    [TestCase("1", 1)]
    [TestCase("01", 1)]
    [TestCase("2147483647", int.MaxValue)]
    public void ReadPositiveIntegerAcceptsPositiveDecimalIntegers(string value, int expected) {
        Assert.That(AgentEnvironment.ReadPositiveInteger("SECONDS", value), Is.EqualTo(expected));
    }

    [TestCase("0")]
    [TestCase("-1")]
    [TestCase("+1")]
    [TestCase(" 1 ")]
    [TestCase("1.0")]
    [TestCase("2147483648")]
    public void ReadPositiveIntegerRejectsOtherValues(string value) {
        var exception = Assert.Throws<ConfigurationException>(() =>
            AgentEnvironment.ReadPositiveInteger("SECONDS", value)
        );

        Assert.That(exception!.Message, Is.EqualTo("environment variable SECONDS must be a positive integer"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ReadPositiveIntegerUsesDefaultForBlankValues(string? value) {
        Assert.That(AgentEnvironment.ReadPositiveInteger("SECONDS", value), Is.Null);
    }

    [Test]
    public void NormalizeCanonicalizesManagedEnvironment() {
        WithEnvironment(() => {
            Environment.SetEnvironmentVariable(AgentEnvironment.WEB_SOCKET, " TrUe ");
            Environment.SetEnvironmentVariable(AgentEnvironment.HEALTH_FILE, "   ");
            Environment.SetEnvironmentVariable(AgentEnvironment.HEALTH_INTERVAL_SECONDS, "01");

            AgentEnvironment.Normalize([]);

            Assert.Multiple(() => {
                Assert.That(Environment.GetEnvironmentVariable(AgentEnvironment.WEB_SOCKET), Is.EqualTo("true"));
                Assert.That(Environment.GetEnvironmentVariable(AgentEnvironment.HEALTH_FILE), Is.Null);
                Assert.That(Environment.GetEnvironmentVariable(AgentEnvironment.HEALTH_INTERVAL_SECONDS), Is.EqualTo("1"));
            });
        });
    }

    [TestCase("false")]
    [TestCase("0")]
    public void NormalizeRemovesDisabledWebSocketSetting(string value) {
        WithEnvironment(() => {
            Environment.SetEnvironmentVariable(AgentEnvironment.WEB_SOCKET, value);

            AgentEnvironment.Normalize([]);

            Assert.That(Environment.GetEnvironmentVariable(AgentEnvironment.WEB_SOCKET), Is.Null);
        });
    }

    [Test]
    public void NormalizeDoesNotDuplicateExplicitWebSocketArgument() {
        WithEnvironment(() => {
            Environment.SetEnvironmentVariable(AgentEnvironment.WEB_SOCKET, "true");

            AgentEnvironment.Normalize(["-webSocket"]);

            Assert.That(Environment.GetEnvironmentVariable(AgentEnvironment.WEB_SOCKET), Is.Null);
        });
    }

    static void WithEnvironment(TestDelegate action) {
        var original = new Dictionary<string, string?>();
        foreach (string name in names) {
            original[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }
        try {
            action();
        } finally {
            foreach ((string name, string? value) in original) {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
