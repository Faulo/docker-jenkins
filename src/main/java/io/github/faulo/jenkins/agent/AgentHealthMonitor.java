package io.github.faulo.jenkins.agent;

import java.io.IOException;
import java.lang.management.ManagementFactory;
import java.lang.reflect.Field;
import java.lang.reflect.InvocationTargetException;
import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.nio.charset.StandardCharsets;
import java.nio.file.AtomicMoveNotSupportedException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.nio.file.StandardOpenOption;
import java.time.Instant;
import java.util.Map;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;
import java.util.concurrent.atomic.AtomicBoolean;

public final class AgentHealthMonitor {
    private static final int DEFAULT_INTERVAL_SECONDS = 10;
    private static final int DEFAULT_TIMEOUT_SECONDS = 5;

    private AgentHealthMonitor() {
    }

    @SuppressWarnings("unused")
    public static void premain(String arguments) throws IOException {
        Monitor monitor = new Monitor();
        try {
            monitor.writeStatus();
        } catch (IOException exception) {
            throw new IOException("failed to initialize Jenkins Remoting health status", exception);
        }
        Thread thread = new Thread(monitor, "jenkins-remoting-health-monitor");
        thread.setDaemon(true);
        thread.start();
    }

    private static final class Monitor implements Runnable {
        private final Path statusFile;
        private final int intervalSeconds;
        private final int timeoutSeconds;
        private final long pid;
        private final long processStart;
        private final AtomicBoolean roundTripRunning = new AtomicBoolean();
        private String state = "starting";
        private String diagnostic = "waiting-for-channel";
        private long stateSince = System.currentTimeMillis();
        private long lastSuccess;
        private Object currentChannel;
        private boolean sawChannel;
        private ExecutorService roundTripExecutor;

        private Monitor() {
            String configuredFile = System.getenv(AgentHealth.HEALTH_FILE);
            statusFile = Path.of(configuredFile == null || configuredFile.isBlank()
                ? isWindows() ? "C:/jenkins/agent-health.status" : "/jenkins/agent-health.status"
                : configuredFile);
            intervalSeconds = readPositiveInteger("JENKINS_HEALTH_INTERVAL_SECONDS", DEFAULT_INTERVAL_SECONDS);
            timeoutSeconds = readPositiveInteger("JENKINS_HEALTH_TIMEOUT_SECONDS", DEFAULT_TIMEOUT_SECONDS);
            pid = ProcessHandle.current().pid();
            processStart = ProcessHandle.current().info().startInstant()
                .orElse(Instant.ofEpochMilli(ManagementFactory.getRuntimeMXBean().getStartTime()))
                .toEpochMilli();
        }

        @Override
        public void run() {
            try {
                while (!Thread.currentThread().isInterrupted()) {
                    try {
                        monitor();
                    } catch (ClassNotFoundException exception) {
                        diagnostic = "waiting-for-remoting";
                        writeStatusIgnoringFailure();
                    }
                    TimeUnit.SECONDS.sleep(currentChannel == null ? 1 : intervalSeconds);
                }
            } catch (InterruptedException exception) {
                Thread.currentThread().interrupt();
                transition("terminal", "monitor-interrupted");
                writeStatusIgnoringFailure();
            } catch (ReflectiveOperationException | RuntimeException exception) {
                transition("terminal", "remoting-hook-incompatible");
                writeStatusIgnoringFailure();
            }
        }

        private void monitor() throws ReflectiveOperationException, InterruptedException {
            Object channel = findActiveChannel();
            if (channel == null) {
                if (currentChannel != null) {
                    closeRoundTripExecutor();
                    currentChannel = null;
                    transition("reconnecting", "waiting-for-channel");
                } else if (sawChannel && !state.equals("reconnecting")) {
                    transition("reconnecting", "waiting-for-channel");
                }
                writeStatusIgnoringFailure();
                return;
            }
            if (channel != currentChannel) {
                closeRoundTripExecutor();
                currentChannel = channel;
                sawChannel = true;
                transition("connected", "round-trip-pending");
                roundTripExecutor = Executors.newSingleThreadExecutor(task -> {
                    Thread thread = new Thread(task, "jenkins-remoting-health-round-trip");
                    thread.setDaemon(true);
                    return thread;
                });
            }
            performRoundTrip(channel);
            writeStatusIgnoringFailure();
        }

        private void performRoundTrip(Object channel) throws InterruptedException, NoSuchMethodException {
            if (!roundTripRunning.compareAndSet(false, true) || roundTripExecutor == null) {
                diagnostic = "round-trip-pending";
                return;
            }
            Method syncIo = channel.getClass().getMethod("syncIO");
            AtomicBoolean roundTripStarted = new AtomicBoolean();
            Future<?> future = roundTripExecutor.submit(() -> {
                roundTripStarted.set(true);
                try {
                    syncIo.invoke(channel);
                } catch (IllegalAccessException | InvocationTargetException exception) {
                    throw new IllegalStateException("Remoting round trip failed", exception);
                } finally {
                    roundTripRunning.set(false);
                }
                return null;
            });
            try {
                future.get(timeoutSeconds, TimeUnit.SECONDS);
                lastSuccess = System.currentTimeMillis();
                transition("healthy", "none");
            } catch (TimeoutException exception) {
                future.cancel(true);
                if (!roundTripStarted.get()) {
                    roundTripRunning.set(false);
                }
                transition("connected", "round-trip-timeout");
            } catch (ExecutionException exception) {
                transition("connected", "round-trip-failed");
            }
        }

        private void closeRoundTripExecutor() {
            if (roundTripExecutor != null) {
                roundTripExecutor.shutdownNow();
                roundTripExecutor = null;
            }
            roundTripRunning.set(false);
        }

        private void transition(String newState, String newDiagnostic) {
            if (!state.equals(newState)) {
                state = newState;
                stateSince = System.currentTimeMillis();
            }
            diagnostic = newDiagnostic;
        }

        private synchronized void writeStatus() throws IOException {
            String content = "version=1\n"
                + "pid=" + pid + "\n"
                + "processStart=" + processStart + "\n"
                + "state=" + state + "\n"
                + "stateSince=" + stateSince + "\n"
                + "lastSuccess=" + lastSuccess + "\n"
                + "updated=" + System.currentTimeMillis() + "\n"
                + "diagnostic=" + diagnostic + "\n";
            Path absoluteFile = statusFile.toAbsolutePath();
            Files.createDirectories(absoluteFile.getParent());
            Path temporaryFile = absoluteFile.resolveSibling(absoluteFile.getFileName() + ".tmp-" + pid);
            Files.writeString(temporaryFile, content, StandardCharsets.UTF_8, StandardOpenOption.CREATE,
                StandardOpenOption.TRUNCATE_EXISTING, StandardOpenOption.WRITE);
            try {
                Files.move(temporaryFile, absoluteFile, StandardCopyOption.ATOMIC_MOVE,
                    StandardCopyOption.REPLACE_EXISTING);
            } catch (AtomicMoveNotSupportedException exception) {
                Files.move(temporaryFile, absoluteFile, StandardCopyOption.REPLACE_EXISTING);
            }
        }

        private void writeStatusIgnoringFailure() {
            try {
                writeStatus();
            } catch (IOException exception) {
                // A missing heartbeat makes the external health check fail closed.
            }
        }
    }

    private static Object findActiveChannel() throws ReflectiveOperationException {
        Class<?> channelClass = Class.forName("hudson.remoting.Channel");
        Field registry = findChannelRegistry(channelClass);
        registry.setAccessible(true);
        Object registryValue = registry.get(null);
        if (!(registryValue instanceof Map<?, ?> activeChannels)) {
            throw new IllegalStateException("unexpected Remoting channel registry");
        }
        Method isClosingOrClosed = channelClass.getMethod("isClosingOrClosed");
        // Remoting protects its weak registry with the registry object's monitor.
        //noinspection SynchronizationOnLocalVariableOrMethodParameter
        synchronized (activeChannels) {
            for (Object candidate : activeChannels.keySet()) {
                if (channelClass.isInstance(candidate) && !((Boolean) isClosingOrClosed.invoke(candidate))) {
                    return candidate;
                }
            }
        }
        return null;
    }

    private static Field findChannelRegistry(Class<?> channelClass) throws NoSuchFieldException {
        try {
            return channelClass.getDeclaredField("ACTIVE_CHANNELS");
        } catch (NoSuchFieldException exception) {
            for (Field field : channelClass.getDeclaredFields()) {
                if (Modifier.isStatic(field.getModifiers()) && Map.class.isAssignableFrom(field.getType())) {
                    return field;
                }
            }
            throw exception;
        }
    }

    private static int readPositiveInteger(String name, int defaultValue) {
        String value = System.getenv(name);
        if (value == null || value.isBlank()) {
            return defaultValue;
        }
        try {
            int parsed = Integer.parseInt(value);
            if (parsed > 0) {
                return parsed;
            }
        } catch (NumberFormatException exception) {
            // Report only the variable name so configuration values cannot leak.
        }
        throw new IllegalArgumentException("environment variable " + name + " must be a positive integer");
    }

    private static boolean isWindows() {
        return System.getProperty("os.name", "").startsWith("Windows");
    }
}
