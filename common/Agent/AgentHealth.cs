using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Agent;

static class AgentHealth {
    const int DEFAULT_GRACE_SECONDS = 120;
    const int DEFAULT_STALE_SECONDS = 30;

    public static int Run() {
        string? configuredFile = Environment.GetEnvironmentVariable(AgentEnvironment.HEALTH_FILE);
        string statusFile = string.IsNullOrWhiteSpace(configuredFile)
            ? OperatingSystem.IsWindows()
                ? @"C:\jenkins\agent-health.status"
                : "/jenkins/agent-health.status"
            : configuredFile;
        int graceSeconds = ReadPositiveSeconds(AgentEnvironment.HEALTH_GRACE_SECONDS, DEFAULT_GRACE_SECONDS);
        int staleSeconds = ReadPositiveSeconds(AgentEnvironment.HEALTH_STALE_SECONDS, DEFAULT_STALE_SECONDS);
        _ = ReadPositiveSeconds(AgentEnvironment.HEALTH_INTERVAL_SECONDS, 10);
        _ = ReadPositiveSeconds(AgentEnvironment.HEALTH_TIMEOUT_SECONDS, 5);
        return Run(
            statusFile,
            DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(graceSeconds),
            TimeSpan.FromSeconds(staleSeconds),
            ProcessMatches,
            Console.Out,
            Console.Error
        );
    }

    internal static int Run(
        string statusFile,
        DateTimeOffset now,
        TimeSpan grace,
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
            || !TryReadLong(values, "stateSince", out long stateSince)
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
        if (!IsPastTimestamp(stateSince, nowMilliseconds)
            || !IsPastTimestamp(updated, nowMilliseconds)
            || Age(nowMilliseconds, updated) > stale) {
            error.WriteLine("docker-jenkins-agent: unhealthy; Remoting health state is stale");
            return 1;
        }

        switch (state) {
            case "healthy":
                if (IsPastTimestamp(lastSuccess, nowMilliseconds)
                    && Age(nowMilliseconds, lastSuccess) <= stale) {
                    output.WriteLine("docker-jenkins-agent: healthy; Remoting round trip succeeded");
                    return 0;
                }
                error.WriteLine("docker-jenkins-agent: unhealthy; Remoting round trip is stale");
                return 1;
            case "starting":
            case "connected":
            case "reconnecting":
                if (Age(nowMilliseconds, stateSince) <= grace) {
                    output.WriteLine($"docker-jenkins-agent: {state}; within connection grace period");
                    return 0;
                }
                error.WriteLine($"docker-jenkins-agent: unhealthy; {state} grace period expired");
                return 1;
            case "terminal":
                error.WriteLine("docker-jenkins-agent: unhealthy; Remoting health monitor stopped");
                return 1;
            default:
                error.WriteLine("docker-jenkins-agent: unhealthy; Remoting health state is invalid");
                return 1;
        }
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
        return AgentEnvironment.ReadPositiveInteger(name, Environment.GetEnvironmentVariable(name)) ?? defaultValue;
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
