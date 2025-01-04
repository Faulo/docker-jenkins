FROM debian:bookworm-slim

ARG DEBIAN_FRONTEND=noninteractive

USER root

# Linux Tools
RUN apt update && \
    apt upgrade -y && \
    apt install -y \
    apt-transport-https \
    wget \
    zip \
    nano \
    gnupg2 \
    ca-certificates \
    software-properties-common \
    curl \
	tini \
    pciutils && \
    apt clean && rm -rf /var/lib/apt/lists/*

# UTF-8
ENV LANG=C.UTF-8
RUN apt update && \
    apt install -y locales && \
    echo "$LANG UTF-8" > /etc/locale.gen && \
    locale-gen $LANG && \
    apt clean && rm -rf /var/lib/apt/lists/*

# Git
RUN apt update && \
    apt install -y \
    git \
    git-lfs && \
    apt clean && rm -rf /var/lib/apt/lists/*

# Plastic
RUN echo "deb https://www.plasticscm.com/plasticrepo/stable/ubuntu/ ./" > /etc/apt/sources.list.d/plasticscm-stable.list && \
    wget https://www.plasticscm.com/plasticrepo/stable/ubuntu/Release.key -O - | apt-key add - && \
    apt update && \
    apt install -y plasticscm-client-core && \
    apt clean && rm -rf /var/lib/apt/lists/*

# Docker
ENV DOCKER_HOST=unix:///var/run/docker.sock
RUN curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /usr/share/keyrings/docker-archive-keyring.gpg && \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/usr/share/keyrings/docker-archive-keyring.gpg] https://download.docker.com/linux/debian $(lsb_release -cs) stable" > /etc/apt/sources.list.d/docker.list && \
    apt update && apt install -y \
    docker-ce-cli \
    docker-compose-plugin && \
    apt clean && rm -rf /var/lib/apt/lists/*

# Jenkins
WORKDIR /var
COPY --chmod=755 jenkins-agent.sh /usr/local/bin/jenkins-agent.sh
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/jenkins-agent.sh"]

# Java
ENV JAVA_VERSION=17
ENV JAVA_OPTS="-Dhudson.model.DirectoryBrowserSupport.CSP=\"\""
RUN apt update && \
    apt install -y \
    openjdk-$JAVA_VERSION-jre && \
    apt clean && rm -rf /var/lib/apt/lists/*

# Test
RUN java --version
