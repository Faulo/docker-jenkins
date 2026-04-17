FROM jenkins/inbound-agent:latest-jdk21

ARG DEBIAN_FRONTEND=noninteractive

# Jenkins
USER root
WORKDIR /var
ENV JAVA_OPTS="-Dhudson.model.DirectoryBrowserSupport.CSP=\"\""

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
        zip \
        unzip \
        apt-transport-https && \
    echo "$LANG UTF-8" > /etc/locale.gen && \
    locale-gen "$LANG" && \
    install -m 0755 -d /etc/apt/keyrings && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Plastic
RUN echo 'deb [trusted=yes allow-insecure=yes] https://www.plasticscm.com/plasticrepo/stable/debian ./' \
      > /etc/apt/sources.list.d/plasticscm-stable.list && \
    apt-get update -o Acquire::AllowInsecureRepositories=true && \
    apt-get install -y --no-install-recommends --allow-unauthenticated plasticscm-client-core && \
    rm -f /etc/apt/sources.list.d/plasticscm-stable.list && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    cm version

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
    rm -rf /var/lib/apt/lists/* && \
    docker --version
