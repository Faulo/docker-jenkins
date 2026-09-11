FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build

WORKDIR /solution

COPY . .

RUN dotnet publish \
        --configuration Release \
        --runtime linux-x64 \
        --self-contained true \
        --property:PublishSingleFile=true \
        --property:UseAppHost=true \
        --property:DebugType=None

FROM debian:trixie-slim

ARG DEBIAN_FRONTEND=noninteractive

# Package sources
RUN apt-get update && \
    apt-get install -y --no-install-recommends ca-certificates && \
    install -m 0755 -d /etc/apt/keyrings && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

ADD https://packages.microsoft.com/config/debian/13/packages-microsoft-prod.deb /tmp/packages-microsoft-prod.deb
ADD https://download.docker.com/linux/debian/gpg /etc/apt/keyrings/docker.asc
ADD https://deb.nodesource.com/setup_current.x /tmp/nodesource-setup

RUN dpkg -i /tmp/packages-microsoft-prod.deb && \
    chmod a+r /etc/apt/keyrings/docker.asc && \
    echo 'deb [arch=amd64 signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian trixie stable' \
      > /etc/apt/sources.list.d/docker.list && \
    echo 'deb [trusted=yes allow-insecure=yes] https://www.plasticscm.com/plasticrepo/stable/debian ./' \
      > /etc/apt/sources.list.d/plasticscm.list && \
    bash /tmp/nodesource-setup && \
    rm -f /tmp/packages-microsoft-prod.deb /tmp/nodesource-setup

# Runtime tools
COPY linux/jenkins-agent.packages /tmp/jenkins-agent.packages

RUN apt-get update -o Acquire::AllowInsecureRepositories=true && \
    xargs apt-get install -y --no-install-recommends < /tmp/jenkins-agent.packages && \
    git lfs install --system && \
    rm -f /tmp/jenkins-agent.packages && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    java -version && \
    git --version && \
    git lfs version && \
    pwsh --version && \
    node --version && \
    npm --version && \
    npx --version && \
    docker --version && \
    cm version

# The hook uses reflection so it remains compatible with the controller-supplied Remoting JAR.
COPY common/AgentHealthHook/AgentHealthHook.java common/AgentHealthHook/MANIFEST.MF /tmp/agent-health/

RUN mkdir -p /tmp/agent-health/classes /jenkins && \
    javac -d /tmp/agent-health/classes /tmp/agent-health/AgentHealthHook.java && \
    jar cfm /jenkins/agent-health.jar /tmp/agent-health/MANIFEST.MF \
      -C /tmp/agent-health/classes . && \
    rm -rf /tmp/agent-health

# Jenkins
USER root
ENV USER=root \
    HOME=/root \
    LANG=C.UTF-8 \
    JAVA_HOME=/usr/lib/jvm/java-21-openjdk-amd64 \
    AGENT_WORKDIR=/jenkins \
    JENKINS_AGENT_WORKDIR=/jenkins \
    JAVA_OPTS="-Dorg.jenkinsci.plugins.gitclient.Git.timeOut=60" \
    DOCKER_HOST=unix:///var/run/docker.sock
WORKDIR /jenkins
RUN mkdir -p /jenkins/workspace && \
    git config --global --add safe.directory '*'
VOLUME ["/jenkins/workspace"]

COPY --from=build --chmod=755 /solution/common/Agent/bin/Release/net9.0/linux-x64/publish/agent /jenkins/agent

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["/jenkins/agent", "--health"]

ENTRYPOINT ["/jenkins/agent"]
