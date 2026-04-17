FROM debian:bookworm-slim

ARG DEBIAN_FRONTEND=noninteractive

USER root

# Base tools + locale
ENV LANG=C.UTF-8
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    git \
    git-lfs \
    gnupg \
    locales \
    nano \
    pciutils \
    tini \
    wget \
    extrepo \
    zip \
    unzip && \
    echo "$LANG UTF-8" > /etc/locale.gen && \
    locale-gen "$LANG" && \
    install -m 0755 -d /etc/apt/keyrings && \
    curl --version && \
    git --version && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Plastic
RUN wget -qO- https://www.plasticscm.com/plasticrepo/stable/debian/Release.key | \
        gpg --dearmor -o /etc/apt/keyrings/plasticscm-stable.gpg && \
    chmod a+r /etc/apt/keyrings/plasticscm-stable.gpg && \
    echo "deb [signed-by=/etc/apt/keyrings/plasticscm-stable.gpg] https://www.plasticscm.com/plasticrepo/stable/debian ./" \
        > /etc/apt/sources.list.d/plasticscm-stable.list && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
        plasticscm-client-core && \
    cm version && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Docker CLI
ENV DOCKER_HOST=unix:///var/run/docker.sock
RUN curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc && \
    chmod a+r /etc/apt/keyrings/docker.asc && \
    cat > /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/debian
Suites: $(. /etc/os-release && echo "$VERSION_CODENAME")
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        docker-ce-cli \
        docker-compose-plugin && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Java
ENV JAVA_VERSION=21
ENV JAVA_OPTS="-Dhudson.model.DirectoryBrowserSupport.CSP=\"\""
RUN apt-get extrepo enable zulu-openjdk && \
    apt-get update && \
    apt-get install -y --no-install-recommends zulu${JAVA_VERSION}-jre-headless && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    java --version

# Jenkins
ENV JENKINS_URL=http://jenkins:8080/
ENV JENKINS_SECRET=InsertSecretHere
ENV JENKINS_AGENT_NAME=InsertAgentNameHere
ENV JENKINS_JAVA_OPTS=-Dorg.jenkinsci.plugins.gitclient.Git.timeOut=60
WORKDIR /var
COPY --chmod=755 jenkins-agent.sh /usr/local/bin/jenkins-agent.sh
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/jenkins-agent.sh"]
