using System.IO;
using NUnit.Framework;

namespace Agent.Tests;

sealed class AgentHealthTests {
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    public void HealthReflectsSupervisorOwnership(bool running, int expectedExitCode) {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = AgentHealth.Run(() => running, output, error);

        Assert.That(exitCode, Is.EqualTo(expectedExitCode));
    }
}
