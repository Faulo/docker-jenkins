using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Agent;

static class AgentEnvironment {
    public const string WEB_SOCKET = "JENKINS_WEB_SOCKET";
    public const string HEALTH_FILE = "JENKINS_HEALTH_FILE";
    public const string HEALTH_GRACE_SECONDS = "JENKINS_HEALTH_GRACE_SECONDS";
    public const string HEALTH_STALE_SECONDS = "JENKINS_HEALTH_STALE_SECONDS";
    public const string HEALTH_INTERVAL_SECONDS = "JENKINS_HEALTH_INTERVAL_SECONDS";
    public const string HEALTH_TIMEOUT_SECONDS = "JENKINS_HEALTH_TIMEOUT_SECONDS";

    static readonly string[] healthSeconds = [
        HEALTH_GRACE_SECONDS,
        HEALTH_STALE_SECONDS,
        HEALTH_INTERVAL_SECONDS,
        HEALTH_TIMEOUT_SECONDS
    ];

    public static void Normalize(IReadOnlyList<string> arguments) => Normalize(
        arguments,
        Environment.GetEnvironmentVariable,
        Environment.SetEnvironmentVariable
    );

    internal static void Normalize(IReadOnlyList<string> arguments, IDictionary<string, string> environment) {
        string? readValue(string name) => environment.TryGetValue(name, out string? value) ? value : null;
        void writeValue(string name, string? value) {
            if (value is null) {
                environment.Remove(name);
            } else {
                environment[name] = value;
            }
        }
        Normalize(arguments, readValue, writeValue);
    }

    static void Normalize(
        IReadOnlyList<string> arguments,
        Func<string, string?> read,
        Action<string, string?> write
    ) {
        bool webSocket = ReadBoolean(WEB_SOCKET, read(WEB_SOCKET), true);
        bool explicitWebSocket = arguments.Contains("-webSocket", StringComparer.Ordinal);
        write(WEB_SOCKET, webSocket && !explicitWebSocket ? "true" : null);

        string? healthFile = read(HEALTH_FILE);
        if (string.IsNullOrWhiteSpace(healthFile)) {
            write(HEALTH_FILE, null);
        }

        foreach (string name in healthSeconds) {
            string? value = read(name);
            int? seconds = ReadPositiveInteger(name, value);
            if (seconds.HasValue) {
                write(name, seconds.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    internal static bool ReadBoolean(string name, string? value, bool defaultValue) {
        if (string.IsNullOrWhiteSpace(value)) {
            return defaultValue;
        }
        string normalized = value.Trim();
        if (string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) || normalized == "1") {
            return true;
        }
        if (string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) || normalized == "0") {
            return false;
        }
        throw new ConfigurationException($"environment variable {name} must be true, false, 1, or 0");
    }

    internal static int? ReadPositiveInteger(string name, string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0) {
            throw new ConfigurationException($"environment variable {name} must be a positive integer");
        }
        return parsed;
    }
}
