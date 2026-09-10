using System;
using System.IO;

namespace Agent;

static class AgentHealth {
    public static int Run() => Run(AgentSupervisor.IsRunning, Console.Out, Console.Error);

    internal static int Run(Func<bool> isSupervisorRunning, TextWriter output, TextWriter error) {
        if (isSupervisorRunning()) {
            output.WriteLine("docker-jenkins-agent: healthy; managed Jenkins agent process is running");
            return 0;
        }
        error.WriteLine("docker-jenkins-agent: unhealthy; managed Jenkins agent process is not running");
        return 1;
    }
}
