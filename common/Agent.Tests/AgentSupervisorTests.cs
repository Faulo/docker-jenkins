using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Agent.Tests;

sealed class AgentSupervisorTests {
    [Test]
    public void OwnershipIsVisibleFromAnotherThread() {
        string name = $@"Global\docker-jenkins-agent-test-{Guid.NewGuid():N}";

        using (AgentSupervisor.Acquire(name)) {
            Assert.That(Task.Run(() => AgentSupervisor.IsRunning(name)).GetAwaiter().GetResult(), Is.True);
        }

        Assert.That(AgentSupervisor.IsRunning(name), Is.False);
    }

    [Test]
    public void ASecondSupervisorCannotAcquireOwnership() {
        string name = $@"Global\docker-jenkins-agent-test-{Guid.NewGuid():N}";

        using (AgentSupervisor.Acquire(name)) {
            Assert.That(
                Task.Run(() => Assert.Throws<InvalidOperationException>(() => AgentSupervisor.Acquire(name)))
                    .GetAwaiter()
                    .GetResult(),
                Is.Not.Null
            );
        }
    }
}
