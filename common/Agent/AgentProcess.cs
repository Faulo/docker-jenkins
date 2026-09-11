using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Agent;

static class AgentProcess {
    const string JENKINS_AGENT_NAME = "JENKINS_AGENT_NAME";
    const string JENKINS_AGENT_WORKDIR = "JENKINS_AGENT_WORKDIR";
    const string JENKINS_DIRECT_CONNECTION = "JENKINS_DIRECT_CONNECTION";
    const string JENKINS_INSTANCE_IDENTITY = "JENKINS_INSTANCE_IDENTITY";
    const string JENKINS_JAVA_BIN = "JENKINS_JAVA_BIN";
    const string JENKINS_JAVA_OPTS = "JENKINS_JAVA_OPTS";
    const string JENKINS_NAME = "JENKINS_NAME";
    const string JENKINS_PROTOCOLS = "JENKINS_PROTOCOLS";
    const string JENKINS_SECRET = "JENKINS_SECRET";
    const string JENKINS_TUNNEL = "JENKINS_TUNNEL";
    const string JENKINS_URL = "JENKINS_URL";
    const string JAVA_HOME = "JAVA_HOME";
    const string JAVA_OPTS = "JAVA_OPTS";
    const string REMOTING_OPTS = "REMOTING_OPTS";

    public static int Run(IReadOnlyList<string> arguments) {
        if (TryAlternateStartInfo(arguments, out var alternate)) {
            return RunProcess(alternate, false);
        }

        AgentEnvironment.Normalize(arguments);
        var environment = CurrentEnvironment();
        string agentJar = AgentJar.DestinationPath(AppContext.BaseDirectory);
        AgentJar.Install(agentJar);
        string agentJarArgument = OperatingSystem.IsWindows()
            ? agentJar.Replace('\\', '/')
            : agentJar;
        string healthHook = AgentHealth.HookPath(AppContext.BaseDirectory);
        string healthHookArgument = OperatingSystem.IsWindows()
            ? healthHook.Replace('\\', '/')
            : healthHook;
        var start = JavaStartInfo(
            arguments,
            environment,
            OperatingSystem.IsWindows(),
            agentJarArgument,
            healthHookArgument
        );
        using var supervisor = AgentSupervisor.Acquire();
        return RunProcess(start, OperatingSystem.IsLinux());
    }

    internal static ProcessStartInfo JavaStartInfo(
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        bool windows,
        string agentJar,
        string healthHook
    ) {
        string? javaOptions = Read(environment, JENKINS_JAVA_OPTS);
        string javaOptionsName = JENKINS_JAVA_OPTS;
        if (string.IsNullOrWhiteSpace(javaOptions)) {
            javaOptions = Read(environment, JAVA_OPTS);
            javaOptionsName = JAVA_OPTS;
        }

        var start = new ProcessStartInfo {
            FileName = JavaExecutable(environment, windows),
            UseShellExecute = false
        };
        AddOptions(start, javaOptionsName, javaOptions);
        start.ArgumentList.Add($"-javaagent:{healthHook}");
        start.ArgumentList.Add("-jar");
        start.ArgumentList.Add(agentJar);

        string? name = Read(environment, JENKINS_NAME);
        if (string.IsNullOrWhiteSpace(name)) {
            name = Read(environment, JENKINS_AGENT_NAME);
        }
        AddEnvironmentArgument(start, arguments, "-secret", Read(environment, JENKINS_SECRET));
        AddEnvironmentArgument(start, arguments, "-name", name);
        AddEnvironmentArgument(start, arguments, "-tunnel", Read(environment, JENKINS_TUNNEL));
        AddEnvironmentArgument(start, arguments, "-url", Read(environment, JENKINS_URL));
        AddEnvironmentArgument(start, arguments, "-workDir", Read(environment, JENKINS_AGENT_WORKDIR));
        if (!HasOption(arguments, "-webSocket")
            && string.Equals(Read(environment, AgentEnvironment.WEB_SOCKET), "true", StringComparison.Ordinal)) {
            start.ArgumentList.Add("-webSocket");
        }
        AddEnvironmentArgument(start, arguments, "-direct", Read(environment, JENKINS_DIRECT_CONNECTION));
        AddEnvironmentArgument(start, arguments, "-protocols", Read(environment, JENKINS_PROTOCOLS));
        AddEnvironmentArgument(start, arguments, "-instanceIdentity", Read(environment, JENKINS_INSTANCE_IDENTITY));
        AddOptions(start, REMOTING_OPTS, Read(environment, REMOTING_OPTS));
        foreach (string argument in arguments) {
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    static IReadOnlyDictionary<string, string?> CurrentEnvironment() {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        return Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(
                entry => (string)entry.Key,
                entry => (string?)entry.Value,
                comparer
            );
    }

    static string JavaExecutable(IReadOnlyDictionary<string, string?> environment, bool windows) {
        string? configured = Read(environment, JENKINS_JAVA_BIN);
        if (!string.IsNullOrWhiteSpace(configured)) {
            return configured;
        }
        string? javaHome = Read(environment, JAVA_HOME);
        if (!string.IsNullOrWhiteSpace(javaHome)) {
            return javaHome.TrimEnd('/', '\\') + (windows ? "/bin/java.exe" : "/bin/java");
        }
        return windows ? "java.exe" : "java";
    }

    static void AddEnvironmentArgument(
        ProcessStartInfo start,
        IReadOnlyList<string> arguments,
        string option,
        string? value
    ) {
        if (string.IsNullOrWhiteSpace(value) || HasOption(arguments, option)) {
            return;
        }
        start.ArgumentList.Add(option);
        start.ArgumentList.Add(value);
    }

    static bool HasOption(IReadOnlyList<string> arguments, string option) {
        return arguments.Contains(option, StringComparer.Ordinal);
    }

    static string? Read(IReadOnlyDictionary<string, string?> environment, string name) {
        return environment.TryGetValue(name, out string? value) ? value : null;
    }

    static void AddOptions(ProcessStartInfo start, string name, string? configured) {
        if (string.IsNullOrWhiteSpace(configured)) {
            return;
        }
        foreach (string option in SplitOptions(name, configured)) {
            start.ArgumentList.Add(option);
        }
    }

    static IReadOnlyList<string> SplitOptions(string name, string configured) {
        var result = new List<string>();
        var value = new StringBuilder();
        char quote = '\0';
        bool started = false;
        for (int index = 0; index < configured.Length; index++) {
            char character = configured[index];
            if (quote == '\0' && char.IsWhiteSpace(character)) {
                if (started) {
                    result.Add(value.ToString());
                    value.Clear();
                    started = false;
                }
                continue;
            }
            if (character is '\'' or '"') {
                if (quote == '\0') {
                    quote = character;
                    started = true;
                    continue;
                }
                if (quote == character) {
                    quote = '\0';
                    continue;
                }
            }
            if (character == '\\' && quote != '\'' && index + 1 < configured.Length) {
                char next = configured[index + 1];
                if (next is '\\' or '\'' or '"' || char.IsWhiteSpace(next)) {
                    value.Append(next);
                    started = true;
                    index++;
                    continue;
                }
            }
            value.Append(character);
            started = true;
        }
        if (quote != '\0') {
            throw new ConfigurationException($"environment variable {name} contains an unterminated quoted option");
        }
        if (started) {
            result.Add(value.ToString());
        }
        return result;
    }

    static bool TryAlternateStartInfo(IReadOnlyList<string> arguments, out ProcessStartInfo start) {
        if (OperatingSystem.IsLinux()
            && arguments.Count == 1
            && !arguments[0].StartsWith("-", StringComparison.Ordinal)) {
            start = new ProcessStartInfo {
                FileName = arguments[0],
                UseShellExecute = false
            };
            return true;
        }
        if (OperatingSystem.IsWindows()
            && arguments.Count == 2
            && string.Equals(arguments[0], "-Cmd", StringComparison.OrdinalIgnoreCase)) {
            start = new ProcessStartInfo {
                FileName = "powershell.exe",
                UseShellExecute = false
            };
            start.ArgumentList.Add("-NoProfile");
            start.ArgumentList.Add("-Command");
            start.ArgumentList.Add(arguments[1]);
            return true;
        }
        start = null!;
        return false;
    }

    static int RunProcess(ProcessStartInfo start, bool forwardPosixSignals) {
        using var process = Process.Start(start)
                            ?? throw new InvalidOperationException("failed to start the Jenkins agent process");
        if (!forwardPosixSignals) {
            process.WaitForExit();
            return process.ExitCode;
        }

        using var interrupt = Forward(PosixSignal.SIGINT, process);
        using var terminate = Forward(PosixSignal.SIGTERM, process);
        process.WaitForExit();
        return process.ExitCode;
    }

    static PosixSignalRegistration Forward(PosixSignal signal, Process process) {
        return PosixSignalRegistration.Create(signal, context => {
            try {
                int nativeSignal = signal == PosixSignal.SIGINT ? 2 : 15;
                if (!process.HasExited && kill(process.Id, nativeSignal) == 0) {
                    context.Cancel = true;
                }
            } catch (InvalidOperationException) {
                // The child exited between signal delivery and inspection.
            }
        });
    }

    [DllImport("libc", SetLastError = true)]
    static extern int kill(int processId, int signal);
}
