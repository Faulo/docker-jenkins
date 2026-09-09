using NUnit.Framework;

namespace Agent.Tests;

sealed class AgentJarTests {
    [TestCase("https://jenkins.example/", "https://jenkins.example/jnlpJars/agent.jar")]
    [TestCase("https://jenkins.example/root", "https://jenkins.example/root/jnlpJars/agent.jar")]
    [TestCase("https://jenkins.example/root/", "https://jenkins.example/root/jnlpJars/agent.jar")]
    public void ControllerJarUriIsBelowJenkinsRoot(string configured, string expected) {
        Assert.That(AgentJar.ControllerJarUri(configured), Is.EqualTo(new System.Uri(expected)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("relative/path")]
    [TestCase("file:///tmp/jenkins")]
    public void ControllerJarUriRequiresHttpJenkinsUrl(string? configured) {
        var exception = Assert.Throws<ConfigurationException>(() => AgentJar.ControllerJarUri(configured));

        Assert.That(exception!.Message, Does.Contain("JENKINS_URL"));
    }
}
