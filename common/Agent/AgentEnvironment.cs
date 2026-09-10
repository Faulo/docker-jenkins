using System;
using System.Collections.Generic;
using System.Linq;

namespace Agent;

static class AgentEnvironment {
    public const string WEB_SOCKET = "JENKINS_WEB_SOCKET";

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

}
