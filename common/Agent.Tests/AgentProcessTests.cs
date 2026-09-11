using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Agent.Tests;

sealed class AgentProcessTests {
    [Test]
    public void JavaStartInfoMapsLauncherEnvironmentAndPreservesArguments() {
        var environment = new Dictionary<string, string?> {
            ["JENKINS_JAVA_BIN"] = "/custom/java",
            ["JENKINS_JAVA_OPTS"] = "-Xmx1g '-Dmessage=hello world'",
            ["JENKINS_URL"] = "https://jenkins.example/",
            ["JENKINS_SECRET"] = "secret",
            ["JENKINS_AGENT_NAME"] = "agent name",
            ["JENKINS_TUNNEL"] = "tunnel.example:50000",
            ["JENKINS_AGENT_WORKDIR"] = "/jenkins",
            ["JENKINS_WEB_SOCKET"] = "true",
            ["JENKINS_DIRECT_CONNECTION"] = "direct.example:50000",
            ["JENKINS_INSTANCE_IDENTITY"] = "identity",
            ["JENKINS_PROTOCOLS"] = "JNLP4-connect",
            ["REMOTING_OPTS"] = "-noReconnectAfter 1h"
        };

        var start = AgentProcess.JavaStartInfo(
            ["-disableHttpsCertValidation"],
            environment,
            false,
            "/jenkins/agent.jar",
            "/jenkins/agent-health.jar"
        );

        Assert.Multiple(() => {
            Assert.That(start.FileName, Is.EqualTo("/custom/java"));
            Assert.That(start.UseShellExecute, Is.False);
            Assert.That(start.ArgumentList.ToArray(), Is.EqualTo(new[] {
                "-Xmx1g",
                "-Dmessage=hello world",
                "-javaagent:/jenkins/agent-health.jar",
                "-jar", "/jenkins/agent.jar",
                "-secret", "secret",
                "-name", "agent name",
                "-tunnel", "tunnel.example:50000",
                "-url", "https://jenkins.example/",
                "-workDir", "/jenkins",
                "-webSocket",
                "-direct", "direct.example:50000",
                "-protocols", "JNLP4-connect",
                "-instanceIdentity", "identity",
                "-noReconnectAfter", "1h",
                "-disableHttpsCertValidation"
            }));
        });
    }

    [Test]
    public void ExplicitConnectionArgumentsAreNotDuplicatedFromEnvironment() {
        var environment = new Dictionary<string, string?> {
            ["JENKINS_URL"] = "https://environment.example/",
            ["JENKINS_SECRET"] = "environment-secret",
            ["JENKINS_AGENT_NAME"] = "environment-name",
            ["JENKINS_AGENT_WORKDIR"] = "/environment-work",
            ["JENKINS_WEB_SOCKET"] = "true"
        };
        string[] arguments = [
            "-url", "https://argument.example/",
            "-secret", "argument-secret",
            "-name", "argument-name",
            "-workDir", "/argument-work",
            "-webSocket"
        ];

        var start = AgentProcess.JavaStartInfo(
            arguments,
            environment,
            false,
            "/jenkins/agent.jar",
            "/jenkins/agent-health.jar"
        );

        Assert.Multiple(() => {
            Assert.That(start.ArgumentList.ToArray(), Does.Not.Contain("environment-secret"));
            Assert.That(start.ArgumentList.ToArray(), Does.Not.Contain("environment-name"));
            Assert.That(start.ArgumentList.ToArray().Count(value => value == "-webSocket"), Is.EqualTo(1));
            Assert.That(start.ArgumentList.TakeLast(arguments.Length), Is.EqualTo(arguments));
        });
    }

    [TestCase(false, "/opt/java/openjdk", "/opt/java/openjdk/bin/java")]
    [TestCase(true, "C:/openjdk-21", "C:/openjdk-21/bin/java.exe")]
    public void JavaHomeSelectsPlatformExecutable(bool windows, string javaHome, string expected) {
        var start = AgentProcess.JavaStartInfo(
            [],
            new Dictionary<string, string?> { ["JAVA_HOME"] = javaHome },
            windows,
            windows ? "C:/jenkins/agent.jar" : "/jenkins/agent.jar",
            windows ? "C:/jenkins/agent-health.jar" : "/jenkins/agent-health.jar"
        );

        Assert.That(start.FileName, Is.EqualTo(expected));
    }

    [TestCase(false, "java")]
    [TestCase(true, "java.exe")]
    public void JavaExecutableFallsBackToPath(bool windows, string expected) {
        var start = AgentProcess.JavaStartInfo(
            [],
            new Dictionary<string, string?>(),
            windows,
            windows ? "C:/jenkins/agent.jar" : "/jenkins/agent.jar",
            windows ? "C:/jenkins/agent-health.jar" : "/jenkins/agent-health.jar"
        );

        Assert.That(start.FileName, Is.EqualTo(expected));
    }

    [Test]
    public void InvalidOptionQuotingDoesNotRepeatTheConfiguredValue() {
        const string configured = "'highly-sensitive-value";
        var environment = new Dictionary<string, string?> {
            ["JENKINS_JAVA_OPTS"] = configured
        };

        var exception = Assert.Throws<ConfigurationException>(() =>
            AgentProcess.JavaStartInfo(
                [],
                environment,
                false,
                "/jenkins/agent.jar",
                "/jenkins/agent-health.jar"
            )
        );

        Assert.That(exception!.Message, Does.Contain("JENKINS_JAVA_OPTS").And.Not.Contain(configured));
    }
}
