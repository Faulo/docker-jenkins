using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Agent;

static class AgentHealth {
    const int DEFAULT_STALE_SECONDS = 30;
    const string HEALTH_FILE = "JENKINS_HEALTH_FILE";
    const string HEALTH_STALE_SECONDS = "JENKINS_HEALTH_STALE_SECONDS";

    public static int Run() {
        string statusFile = StatusPath(
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable(HEALTH_FILE)
        );
        int staleSeconds = ReadPositiveSeconds(HEALTH_STALE_SECONDS, DEFAULT_STALE_SECONDS);
        return Run(
            statusFile,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(staleSeconds),
            ProcessMatches,
            Console.Out,
            Console.Error
        );
    }

    internal static string HookPath(string baseDirectory) => Path.Combine(baseDirectory, "agent-health.jar");

    internal static string StatusPath(string baseDirectory, string? configured = null) {
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(baseDirectory, "agent-health.status")
            : configured;
    }

    internal static int Run(
        string statusFile,
        DateTimeOffset now,
        TimeSpan stale,
        Func<int, long, bool> processMatches,
        TextWriter output,
        TextWriter error
    ) {
        if (!TryRead(statusFile, out var values)) {
            error.WriteLine("docker-jenkins-agent: unhealthy; Remoting health state is unavailable");
            return 1;
        }

        if (!TryReadInt(values, "pid", out int pid)
            || !TryReadLong(values, "processStart", out long processStart)
            || !TryReadLong(values, "lastSuccess", out long lastSuccess)
            || !TryReadLong(values, "updated", out long updated)
            || !values.TryGetValue("version", out string? version)
            || !string.Equals(version, "1", StringComparison.Ordinal)
            || !values.TryGetValue("state", out string? state)
            || !processMatches(pid, processStart)) {
            error.WriteLine("docker-jenkins-agent: unhealthy; Remoting health state is invalid");
            return 1;
        }

        long nowMilliseconds = now.ToUnixTimeMilliseconds();
        if (!IsPastTimestamp(updated, nowMilliseconds)
            || Age(nowMilliseconds, updated) > stale) {
            error.WriteLine("docker-jenkins-agent: unhealthy; Remoting health state is stale");
            return 1;
        }

        if (string.Equals(state, "healthy", StringComparison.Ordinal)
            && IsPastTimestamp(lastSuccess, nowMilliseconds)
            && Age(nowMilliseconds, lastSuccess) <= stale) {
            output.WriteLine("docker-jenkins-agent: healthy; Remoting round trip succeeded");
            return 0;
        }

        error.WriteLine($"docker-jenkins-agent: unhealthy; Remoting state is {SafeState(state)}");
        return 1;
    }

    static string SafeState(string state) {
        return state is "starting" or "connected" or "reconnecting" or "terminal"
            ? state
            : "invalid";
    }

    static TimeSpan Age(long nowMilliseconds, long timestamp) {
        return TimeSpan.FromMilliseconds(nowMilliseconds - timestamp);
    }

    static bool IsPastTimestamp(long timestamp, long nowMilliseconds) {
        return timestamp > 0 && timestamp <= nowMilliseconds + 5000;
    }

    static bool ProcessMatches(int pid, long expectedStart) {
        try {
            using var process = Process.GetProcessById(pid);
            long actualStart = new DateTimeOffset(process.StartTime.ToUniversalTime()).ToUnixTimeMilliseconds();
            return !process.HasExited && Math.Abs(actualStart - expectedStart) <= 2000;
        } catch (ArgumentException) {
            return false;
        } catch (InvalidOperationException) {
            return false;
        } catch (Win32Exception) {
            return false;
        }
    }

    static int ReadPositiveSeconds(string name, int defaultValue) {
        string? value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value)) {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) || seconds <= 0) {
            throw new ConfigurationException($"environment variable {name} must be a positive integer");
        }

        return seconds;
    }

    static bool TryRead(string path, out IReadOnlyDictionary<string, string> values) {
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        values = parsed;
        try {
            foreach (string line in File.ReadAllLines(path)) {
                int separator = line.IndexOf('=');
                if (separator <= 0 || !parsed.TryAdd(line[..separator], line[(separator + 1)..])) {
                    return false;
                }
            }

            return true;
        } catch (IOException) {
            return false;
        } catch (UnauthorizedAccessException) {
            return false;
        }
    }

    static bool TryReadInt(IReadOnlyDictionary<string, string> values, string name, out int value) {
        value = 0;
        return values.TryGetValue(name, out string? text)
               && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
               && value > 0;
    }

    static bool TryReadLong(IReadOnlyDictionary<string, string> values, string name, out long value) {
        value = 0;
        return values.TryGetValue(name, out string? text)
               && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
               && value >= 0;
    }
}
