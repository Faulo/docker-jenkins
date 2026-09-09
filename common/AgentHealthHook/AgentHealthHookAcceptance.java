package agent.health;

import hudson.remoting.Callable;
import hudson.remoting.Channel;
import hudson.remoting.ChannelBuilder;
import hudson.remoting.Pipe;
import org.jenkinsci.remoting.Role;
import org.jenkinsci.remoting.RoleChecker;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.lang.reflect.Field;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.logging.Level;
import java.util.logging.Logger;

public final class AgentHealthHookAcceptance {
    private static final Duration WAIT = Duration.ofSeconds(15);
    private static final int TRANSFER_BYTES = 32 * 1024 * 1024;

    private AgentHealthHookAcceptance() {
    }

    public static void main(String[] arguments) {
        try {
            Logger.getLogger("hudson.remoting").setLevel(Level.WARNING);
            Path statusFile = Path.of(requiredEnvironment("JENKINS_HEALTH_FILE"));
            System.err.println("health acceptance: bidirectional load");
            verifyHeartbeatDuringBidirectionalLoad(statusFile);
            System.err.println("health acceptance: blocked control path");
            verifyOnePendingHeartbeatAndRecovery(statusFile);
            System.err.println("health acceptance: passed");
            System.exit(0);
        } catch (Throwable failure) {
            failure.printStackTrace(System.err);
            System.exit(1);
        }
    }

    private static void verifyHeartbeatDuringBidirectionalLoad(Path statusFile) throws Exception {
        try (ChannelPair pair = ChannelPair.open(false)) {
            long initialSuccess = awaitLastSuccess(statusFile, 0);
            Transfer leftToRight = startTransfer(pair.left, pair.tasks);
            Transfer rightToLeft = startTransfer(pair.right, pair.tasks);

            long successUnderLoad = awaitLastSuccess(statusFile, initialSuccess);
            if (successUnderLoad <= initialSuccess) {
                throw new AssertionError("control heartbeat did not advance during Remoting I/O load");
            }

            leftToRight.await();
            rightToLeft.await();
        }
        awaitState(statusFile, "reconnecting");
    }

    private static void verifyOnePendingHeartbeatAndRecovery(Path statusFile) throws Exception {
        try (ChannelPair pair = ChannelPair.open(true)) {
            long initialSuccess = awaitLastSuccess(statusFile, 0);
            var release = new CountDownLatch(1);
            var started = new CountDownLatch(2);
            pair.leftExecutor.submit(() -> block(started, release));
            pair.rightExecutor.submit(() -> block(started, release));
            try {
                if (!started.await(WAIT.toMillis(), TimeUnit.MILLISECONDS)) {
                    throw new AssertionError("failed to block both Remoting request executors");
                }

                awaitDiagnostic(statusFile, "heartbeat-timeout");
                TimeUnit.SECONDS.sleep(3);
                int pending = pendingCalls(pair.left) + pendingCalls(pair.right);
                if (pending != 1) {
                    throw new AssertionError("expected exactly one pending control heartbeat, got " + pending);
                }
            } finally {
                release.countDown();
            }
            awaitLastSuccess(statusFile, initialSuccess);
        }
    }

    private static Transfer startTransfer(Channel channel, ExecutorService tasks) throws Exception {
        Pipe pipe = Pipe.createLocalToRemote();
        hudson.remoting.Future<Long> reader = channel.callAsync(new SlowDrain(pipe));
        Future<?> writer = tasks.submit(() -> {
            byte[] chunk = new byte[16 * 1024];
            try (OutputStream output = pipe.getOut()) {
                for (int written = 0; written < TRANSFER_BYTES; written += chunk.length) {
                    output.write(chunk);
                }
            }
            return null;
        });
        return new Transfer(writer, reader);
    }

    private static long awaitLastSuccess(Path statusFile, long after) throws Exception {
        long deadline = System.nanoTime() + WAIT.toNanos();
        do {
            Map<String, String> status = readStatus(statusFile);
            long lastSuccess = Long.parseLong(status.getOrDefault("lastSuccess", "0"));
            if ("healthy".equals(status.get("state")) && lastSuccess > after) {
                return lastSuccess;
            }
            TimeUnit.MILLISECONDS.sleep(100);
        } while (System.nanoTime() < deadline);
        throw new AssertionError("control heartbeat did not succeed in time");
    }

    private static void awaitState(Path statusFile, String expected) throws Exception {
        awaitStatus(statusFile, "state", expected);
    }

    private static void awaitDiagnostic(Path statusFile, String expected) throws Exception {
        awaitStatus(statusFile, "diagnostic", expected);
    }

    private static void awaitStatus(Path statusFile, String key, String expected) throws Exception {
        long deadline = System.nanoTime() + WAIT.toNanos();
        do {
            if (expected.equals(readStatus(statusFile).get(key))) {
                return;
            }
            TimeUnit.MILLISECONDS.sleep(100);
        } while (System.nanoTime() < deadline);
        throw new AssertionError("health status " + key + " did not become " + expected);
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

    private static int pendingCalls(Channel channel) throws Exception {
        Field field = Channel.class.getDeclaredField("pendingCalls");
        field.setAccessible(true);
        return ((Map<?, ?>) field.get(channel)).size();
    }

    private static void block(CountDownLatch started, CountDownLatch release) {
        started.countDown();
        try {
            release.await();
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
        }
    }

    private static String requiredEnvironment(String name) {
        String value = System.getenv(name);
        if (value == null || value.isBlank()) {
            throw new IllegalStateException(name + " is required");
        }
        return value;
    }

    private record Transfer(Future<?> writer, Future<Long> reader) {
        private void await() throws Exception {
            writer.get(WAIT.toMillis(), TimeUnit.MILLISECONDS);
            long bytes = reader.get(WAIT.toMillis(), TimeUnit.MILLISECONDS);
            if (bytes != TRANSFER_BYTES) {
                throw new AssertionError("unexpected transfer size " + bytes);
            }
        }
    }

    private static final class SlowDrain implements Callable<Long, IOException> {
        private static final long serialVersionUID = 1L;
        private final Pipe pipe;

        private SlowDrain(Pipe pipe) {
            this.pipe = pipe;
        }

        @Override
        public Long call() throws IOException {
            long bytes = 0;
            byte[] chunk = new byte[16 * 1024];
            try (InputStream input = pipe.getIn()) {
                for (int count; (count = input.read(chunk)) >= 0;) {
                    bytes += count;
                    try {
                        TimeUnit.MILLISECONDS.sleep(2);
                    } catch (InterruptedException exception) {
                        Thread.currentThread().interrupt();
                        throw new IOException("load transfer interrupted", exception);
                    }
                }
            }
            return bytes;
        }

        @Override
        public void checkRoles(RoleChecker checker) throws SecurityException {
            checker.check(this, Role.UNKNOWN);
        }
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

        private static ChannelPair open(boolean singleThreaded) throws Exception {
            ExecutorService leftExecutor = singleThreaded
                ? Executors.newSingleThreadExecutor()
                : Executors.newCachedThreadPool();
            ExecutorService rightExecutor = singleThreaded
                ? Executors.newSingleThreadExecutor()
                : Executors.newCachedThreadPool();
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
