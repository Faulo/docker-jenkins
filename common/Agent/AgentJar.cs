using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Agent;

static class AgentJar {
    const string JENKINS_URL = "JENKINS_URL";
    const long MAXIMUM_SIZE = 100 * 1024 * 1024;
    static readonly TimeSpan downloadTimeout = TimeSpan.FromMinutes(2);
    static readonly HttpClient client = new() { Timeout = downloadTimeout };

    public static string DestinationPath(string baseDirectory) => Path.Combine(baseDirectory, "agent.jar");

    public static void Install(string destination) {
        var source = ControllerJarUri(Environment.GetEnvironmentVariable(JENKINS_URL));
        Install(source, destination).GetAwaiter().GetResult();
    }

    internal static Uri ControllerJarUri(string? configured) {
        if (string.IsNullOrWhiteSpace(configured)
            || !Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var root)
            || (root.Scheme != Uri.UriSchemeHttp && root.Scheme != Uri.UriSchemeHttps)) {
            throw new ConfigurationException($"environment variable {JENKINS_URL} must be an absolute HTTP or HTTPS URL");
        }
        var rootWithSlash = new Uri(root.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
        return new Uri(rootWithSlash, "jnlpJars/agent.jar");
    }

    static async Task Install(Uri source, string destination) {
        string? directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrEmpty(directory)) {
            throw new InvalidOperationException("the Jenkins agent JAR destination has no parent directory");
        }
        string temporary = Path.Combine(directory, $"agent.jar.{Guid.NewGuid():N}.tmp");
        try {
            using var response = await client.GetAsync(
                source,
                HttpCompletionOption.ResponseHeadersRead
            ).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MAXIMUM_SIZE) {
                throw new ConfigurationException("the Jenkins controller agent JAR exceeds the maximum supported size");
            }

            await using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using (var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan
            )) {
                await Copy(input, output).ConfigureAwait(false);
            }
            ValidateJar(temporary);
            File.Move(temporary, destination, true);
        } catch (ConfigurationException) {
            throw;
        } catch (Exception exception) when (exception is HttpRequestException
                                             or TaskCanceledException
                                             or IOException
                                             or UnauthorizedAccessException) {
            throw new ConfigurationException(
                $"failed to install the Jenkins controller agent JAR from {JENKINS_URL}",
                exception
            );
        } finally {
            try {
                File.Delete(temporary);
            } catch (IOException) {
                // Preserve the original download or installation failure.
            } catch (UnauthorizedAccessException) {
                // Preserve the original download or installation failure.
            }
        }
    }

    static async Task Copy(Stream input, Stream output) {
        byte[] buffer = new byte[81920];
        long total = 0;
        while (true) {
            int read = await input.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0) {
                return;
            }
            total += read;
            if (total > MAXIMUM_SIZE) {
                throw new ConfigurationException("the Jenkins controller agent JAR exceeds the maximum supported size");
            }
            await output.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
        }
    }

    static void ValidateJar(string path) {
        Span<byte> signature = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        if (stream.Read(signature) != signature.Length
            || signature[0] != (byte)'P'
            || signature[1] != (byte)'K'
            || signature[2] != 3
            || signature[3] != 4) {
            throw new ConfigurationException("the Jenkins controller response is not an agent JAR");
        }
    }
}
