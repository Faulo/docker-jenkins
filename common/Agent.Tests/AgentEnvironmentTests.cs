using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Agent.Tests;

sealed class AgentEnvironmentTests {
    static readonly string[] names = [
        AgentEnvironment.WEB_SOCKET
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

    [Test]
    public void NormalizeCanonicalizesManagedEnvironment() {
        WithEnvironment(() => {
            Environment.SetEnvironmentVariable(AgentEnvironment.WEB_SOCKET, " TrUe ");

            AgentEnvironment.Normalize([]);

            Assert.That(Environment.GetEnvironmentVariable(AgentEnvironment.WEB_SOCKET), Is.EqualTo("true"));
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
