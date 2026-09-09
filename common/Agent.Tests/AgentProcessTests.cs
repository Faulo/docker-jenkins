using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Agent.Tests;

sealed class AgentProcessTests {
    [Test]
    public void LinuxEnvironmentAppliesIndexedValuesAfterProcessEnvironment() {
        string name = $"AGENT_TEST_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, "from-process-environment");
        try {
            var environment = AgentProcess.LinuxEnvironment(
                new Dictionary<string, string> {
                    [name] = "from-indexed-environment"
                },
                []
            );

            Assert.That(environment, Does.Contain($"{name}=from-indexed-environment"));
            Assert.That(environment, Does.Not.Contain($"{name}=from-process-environment"));
        } finally {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Test]
    public void LinuxEnvironmentPrependsHealthHookToJavaOptions() {
        var environment = AgentProcess.LinuxEnvironment(
            new Dictionary<string, string> {
                ["JENKINS_JAVA_OPTS"] = "-Xmx1g"
            },
            []
        );

        Assert.That(
            environment,
            Does.Contain("JENKINS_JAVA_OPTS=-javaagent:/jenkins/agent-health.jar -Xmx1g")
        );
    }

    [TestCase(" TrUe ", "JENKINS_WEB_SOCKET=true")]
    [TestCase("false", null)]
    public void LinuxEnvironmentNormalizesIndexedWebSocket(string value, string? expected) {
        var environment = AgentProcess.LinuxEnvironment(
            new Dictionary<string, string> {
                [AgentEnvironment.WEB_SOCKET] = value
            },
            []
        );

        Assert.That(
            environment.SingleOrDefault(item => item.StartsWith($"{AgentEnvironment.WEB_SOCKET}=", StringComparison.Ordinal)),
            Is.EqualTo(expected)
        );
    }

    [Test]
    public void WindowsStartInfoPreservesArguments() {
        var start = AgentProcess.WindowsStartInfo(["-url", "https://jenkins.example/with space", "Mörkö"]);

        Assert.Multiple(() => {
            Assert.That(start.FileName, Is.EqualTo("powershell.exe"));
            Assert.That(start.UseShellExecute, Is.False);
            Assert.That(
                start.Environment["JENKINS_JAVA_OPTS"],
                Does.StartWith("\"-javaagent:C:/jenkins/agent-health.jar\"")
            );
            Assert.That(start.ArgumentList.ToArray(), Is.EqualTo(new[] {
                "-File",
                @"C:\ProgramData\Jenkins\jenkins-agent.ps1",
                "-url",
                "https://jenkins.example/with space",
                "Mörkö"
            }));
        });
    }

    [Test]
    public void WindowsStartInfoPrependsHealthHookToExplicitJavaOptions() {
        var start = AgentProcess.WindowsStartInfo(["-JenkinsJavaOpts", "-Xmx1g"]);

        Assert.That(start.ArgumentList.ToArray(), Is.EqualTo(new[] {
            "-File",
            @"C:\ProgramData\Jenkins\jenkins-agent.ps1",
            "-JenkinsJavaOpts",
            "\"-javaagent:C:/jenkins/agent-health.jar\" -Xmx1g"
        }));
    }
}
