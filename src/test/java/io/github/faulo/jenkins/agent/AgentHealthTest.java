package io.github.faulo.jenkins.agent;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.time.Instant;

import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

final class AgentHealthTest {
    @TempDir
    Path temporaryDirectory;

    @Test
    void acceptsFreshHealthyMatchingProcess() throws IOException {
        long now = 1_800_000_000_000L;
        Path status = writeStatus(now, "healthy", now - 1000);
        assertEquals(0, run(status, now, true));
    }

    @Test
    void rejectsStaleOrMismatchedStatus() throws IOException {
        long now = 1_800_000_000_000L;
        assertEquals(1, run(writeStatus(now - 31_000, "healthy", now - 31_000), now, true));
        assertEquals(1, run(writeStatus(now, "healthy", now), now, false));
        assertEquals(1, run(writeStatus(now, "connected", now), now, true));
    }

    private Path writeStatus(long updated, String state, long success) throws IOException {
        Path status = temporaryDirectory.resolve("agent-health.status");
        Files.writeString(status, "version=1\npid=12\nprocessStart=34\nstate=" + state
            + "\nstateSince=1\nlastSuccess=" + success + "\nupdated=" + updated + "\ndiagnostic=none\n");
        return status;
    }

    private static int run(Path status, long now, boolean matches) {
        var bytes = new ByteArrayOutputStream();
        try (var output = new PrintStream(bytes, true, StandardCharsets.UTF_8)) {
            return AgentHealth.run(status, Instant.ofEpochMilli(now), Duration.ofSeconds(30),
                (pid, start) -> matches, output, output);
        }
    }
}
