package agent.health;

import hudson.remoting.Channel;
import hudson.remoting.ChannelBuilder;

import java.io.IOException;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;

public final class AgentHealthMonitorAcceptance {
    private static final Duration WAIT = Duration.ofSeconds(15);

    private AgentHealthMonitorAcceptance() {
    }

    public static void main(String[] arguments) {
        try {
            Path statusFile = Path.of(requiredEnvironment("JENKINS_HEALTH_FILE"));
            awaitState(statusFile, "starting");
            assertHealthProbe(false);
            ChannelPair pair = ChannelPair.open();
            try {
                awaitState(statusFile, "healthy");
                assertHealthProbe(true);
            } finally {
                pair.close();
            }
            awaitState(statusFile, "reconnecting");
            assertHealthProbe(false);
            System.err.println("health acceptance: passed");
            System.exit(0);
        } catch (Throwable failure) {
            failure.printStackTrace(System.err);
            System.exit(1);
        }
    }

    private static void assertHealthProbe(boolean expectedHealthy) throws Exception {
        boolean windows = System.getProperty("os.name").startsWith("Windows");
        String java = windows ? "java.exe" : "java";
        String launcher = windows ? "C:/jenkins/launcher.jar" : "/jenkins/launcher.jar";
        Process process = new ProcessBuilder(java, "-jar", launcher, "--health")
            .redirectErrorStream(true)
            .start();
        if (!process.waitFor(2, TimeUnit.SECONDS)) {
            process.destroyForcibly();
            throw new AssertionError("external health probe exceeded two seconds");
        }
        String output = new String(process.getInputStream().readAllBytes(), StandardCharsets.UTF_8).trim();
        boolean healthy = process.exitValue() == 0;
        if (healthy != expectedHealthy) {
            throw new AssertionError("external health probe result was " + process.exitValue() + ": " + output);
        }
    }

    private static void awaitState(Path statusFile, String expected) throws Exception {
        long deadline = System.nanoTime() + WAIT.toNanos();
        do {
            if (expected.equals(readStatus(statusFile).get("state"))) {
                return;
            }
            TimeUnit.MILLISECONDS.sleep(100);
        } while (System.nanoTime() < deadline);
        throw new AssertionError("health status did not become " + expected);
    }

    private static Map<String, String> readStatus(Path statusFile) throws IOException {
        var status = new HashMap<String, String>();
        if (!Files.exists(statusFile)) {
            return status;
        }
        for (String line : Files.readAllLines(statusFile)) {
            int separator = line.indexOf('=');
            if (separator > 0) {
                status.put(line.substring(0, separator), line.substring(separator + 1));
            }
        }
        return status;
    }

    private static String requiredEnvironment(String name) {
        String value = System.getenv(name);
        if (value == null || value.isBlank()) {
            throw new IllegalStateException(name + " is required");
        }
        return value;
    }

    private static final class ChannelPair implements AutoCloseable {
        private final ExecutorService leftExecutor;
        private final ExecutorService rightExecutor;
        private final ExecutorService tasks;
        private final Channel left;
        private final Channel right;

        private ChannelPair(
            ExecutorService leftExecutor,
            ExecutorService rightExecutor,
            ExecutorService tasks,
            Channel left,
            Channel right
        ) {
            this.leftExecutor = leftExecutor;
            this.rightExecutor = rightExecutor;
            this.tasks = tasks;
            this.left = left;
            this.right = right;
        }

        private static ChannelPair open() throws Exception {
            ExecutorService leftExecutor = Executors.newCachedThreadPool();
            ExecutorService rightExecutor = Executors.newCachedThreadPool();
            ExecutorService tasks = Executors.newCachedThreadPool();
            var server = new ServerSocket(0);
            try {
                Future<Channel> rightChannel = tasks.submit(() -> {
                    Socket socket = server.accept();
                    return builder("acceptance-right", rightExecutor).build(socket);
                });
                Socket socket = new Socket(InetAddress.getLoopbackAddress(), server.getLocalPort());
                Channel left = builder("acceptance-left", leftExecutor).build(socket);
                Channel right = rightChannel.get(WAIT.toMillis(), TimeUnit.MILLISECONDS);
                return new ChannelPair(leftExecutor, rightExecutor, tasks, left, right);
            } catch (Exception exception) {
                leftExecutor.shutdownNow();
                rightExecutor.shutdownNow();
                tasks.shutdownNow();
                throw exception;
            } finally {
                server.close();
            }
        }

        private static ChannelBuilder builder(String name, ExecutorService executor) {
            return new ChannelBuilder(name, executor)
                .withMode(Channel.Mode.BINARY)
                .withArbitraryCallableAllowed(true)
                .withRemoteClassLoadingAllowed(true);
        }

        @Override
        public void close() {
            try {
                left.terminate(new IOException("acceptance test complete"));
                right.terminate(new IOException("acceptance test complete"));
            } finally {
                leftExecutor.shutdownNow();
                rightExecutor.shutdownNow();
                tasks.shutdownNow();
            }
        }
    }
}
