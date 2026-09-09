using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Agent;

static partial class AgentProcess {
    const string LINUX_HEALTH_AGENT = "-javaagent:/jenkins/agent-health.jar";
    const string LINUX_ENTRYPOINT = "/usr/local/bin/jenkins-agent";
    const string WINDOWS_HEALTH_AGENT = "\"-javaagent:C:/jenkins/agent-health.jar\"";
    const string WINDOWS_ENTRYPOINT = @"C:\ProgramData\Jenkins\jenkins-agent.ps1";

    public static int Run(IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> indexedEnvironment) {
        if (OperatingSystem.IsLinux()) {
            return Exec(LINUX_ENTRYPOINT, arguments, LinuxEnvironment(indexedEnvironment, arguments));
        }
        if (OperatingSystem.IsWindows()) {
            AgentEnvironment.Normalize(arguments);
            var start = WindowsStartInfo(arguments);
            using var process = Process.Start(start)
                                ?? throw new InvalidOperationException("failed to start the native Jenkins agent entrypoint");
            process.WaitForExit();
            return process.ExitCode;
        }
        throw new PlatformNotSupportedException("docker-jenkins-agent supports Linux and Windows only");
    }

    internal static ProcessStartInfo WindowsStartInfo(IReadOnlyList<string> arguments) {
        var preparedArguments = WindowsArguments(arguments);
        var start = new ProcessStartInfo {
            FileName = "powershell.exe",
            UseShellExecute = false
        };
        start.Environment["JENKINS_JAVA_OPTS"] = HealthJavaOptions(WINDOWS_HEALTH_AGENT);
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(WINDOWS_ENTRYPOINT);
        foreach (string argument in preparedArguments) {
            start.ArgumentList.Add(argument);
        }
        return start;
    }

    internal static IReadOnlyList<string> WindowsArguments(IReadOnlyList<string> arguments) {
        string[] prepared = arguments.ToArray();
        for (int index = 0; index < prepared.Length; index++) {
            if (!string.Equals(prepared[index], "-JenkinsJavaOpts", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }
            if (index + 1 >= prepared.Length) {
                throw new ConfigurationException("-JenkinsJavaOpts requires a value");
            }
            prepared[index + 1] = AddHealthJavaAgent(WINDOWS_HEALTH_AGENT, prepared[index + 1]);
            index++;
        }
        return prepared;
    }

    internal static IReadOnlyList<string> LinuxEnvironment(
        IReadOnlyDictionary<string, string> indexedEnvironment,
        IReadOnlyList<string> arguments
    ) {
        var environment = Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!, StringComparer.Ordinal);
        foreach ((string name, string value) in indexedEnvironment) {
            environment[name] = value;
        }
        AgentEnvironment.Normalize(arguments, environment);
        environment["JENKINS_JAVA_OPTS"] = HealthJavaOptions(LINUX_HEALTH_AGENT, environment);
        return environment.Select(entry => $"{entry.Key}={entry.Value}").ToArray();
    }

    static string HealthJavaOptions(
        string healthAgent,
        IReadOnlyDictionary<string, string>? environment = null
    ) {
        string? jenkinsJavaOptions = ReadEnvironment("JENKINS_JAVA_OPTS", environment);
        string? javaOptions = ReadEnvironment("JAVA_OPTS", environment);
        string existing = !string.IsNullOrWhiteSpace(jenkinsJavaOptions)
            ? jenkinsJavaOptions
            : javaOptions ?? string.Empty;
        return AddHealthJavaAgent(healthAgent, existing);
    }

    static string AddHealthJavaAgent(string healthAgent, string existing) {
        return existing.Contains(healthAgent, StringComparison.Ordinal)
            ? existing
            : string.IsNullOrWhiteSpace(existing)
                ? healthAgent
                : $"{healthAgent} {existing}";
    }

    static string? ReadEnvironment(string name, IReadOnlyDictionary<string, string>? environment) {
        if (environment is not null && environment.TryGetValue(name, out string? value)) {
            return value;
        }
        return Environment.GetEnvironmentVariable(name);
    }

    static int Exec(string executable, IReadOnlyList<string> arguments, IReadOnlyList<string> environment) {
        IntPtr[] strings = new IntPtr[arguments.Count + 1];
        IntPtr[] environmentStrings = new IntPtr[environment.Count];
        IntPtr argumentVector = IntPtr.Zero;
        IntPtr environmentVector = IntPtr.Zero;
        IntPtr executablePointer = IntPtr.Zero;
        try {
            executablePointer = Marshal.StringToCoTaskMemUTF8(executable);
            strings[0] = Marshal.StringToCoTaskMemUTF8(executable);
            for (int index = 0; index < arguments.Count; index++) {
                strings[index + 1] = Marshal.StringToCoTaskMemUTF8(arguments[index]);
            }
            argumentVector = Marshal.AllocHGlobal((strings.Length + 1) * IntPtr.Size);
            Marshal.Copy(strings, 0, argumentVector, strings.Length);
            Marshal.WriteIntPtr(argumentVector, strings.Length * IntPtr.Size, IntPtr.Zero);
            for (int index = 0; index < environment.Count; index++) {
                environmentStrings[index] = Marshal.StringToCoTaskMemUTF8(environment[index]);
            }
            environmentVector = Marshal.AllocHGlobal((environmentStrings.Length + 1) * IntPtr.Size);
            Marshal.Copy(environmentStrings, 0, environmentVector, environmentStrings.Length);
            Marshal.WriteIntPtr(environmentVector, environmentStrings.Length * IntPtr.Size, IntPtr.Zero);
            execve(executablePointer, argumentVector, environmentVector);
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "failed to execute the native Jenkins agent entrypoint");
        } finally {
            foreach (IntPtr value in strings) {
                Marshal.FreeCoTaskMem(value);
            }
            foreach (IntPtr value in environmentStrings) {
                Marshal.FreeCoTaskMem(value);
            }
            Marshal.FreeHGlobal(argumentVector);
            Marshal.FreeHGlobal(environmentVector);
            Marshal.FreeCoTaskMem(executablePointer);
        }
    }

    [LibraryImport("libc", SetLastError = true)]
    private static partial int execve(IntPtr path, IntPtr arguments, IntPtr environment);
}
