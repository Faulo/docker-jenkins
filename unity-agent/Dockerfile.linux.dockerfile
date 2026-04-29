FROM debian:bookworm-slim

ARG DEBIAN_FRONTEND=noninteractive

# Jenkins
WORKDIR /var

USER root

ENV JENKINS_URL=http://jenkins:8080/
ENV JENKINS_SECRET=InsertSecretHere
ENV JENKINS_AGENT_NAME=InsertAgentNameHere
ENV JENKINS_JAVA_OPTS=-Dorg.jenkinsci.plugins.gitclient.Git.timeOut=60

COPY --chmod=755 jenkins-agent.sh /usr/local/bin/jenkins-agent.sh

ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/jenkins-agent.sh"]

# Base tools + locale
ENV LANG=C.UTF-8
ENV LC_ALL=C.UTF-8

COPY machine-id /etc/machine-id

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

# Unity 2021 OpenSSL compatibility
RUN curl -fsSL https://security.debian.org/debian-security/pool/updates/main/o/openssl/libssl1.1_1.1.1w-0+deb11u5_amd64.deb -o /tmp/libssl1.1.deb && \
    apt install -y /tmp/libssl1.1.deb && \
    rm /tmp/libssl1.1.deb

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

RUN extrepo enable zulu-openjdk && \
    apt-get update && \
    apt-get install -y --no-install-recommends zulu${JAVA_VERSION}-jre-headless && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    java --version

# NodeJS 22
RUN curl -fsSL "https://deb.nodesource.com/setup_22.x" | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# itch.io
RUN curl -fsSL "https://broth.itch.zone/butler/linux-amd64/LATEST/archive/default" -o /tmp/butler.zip && \
    unzip /tmp/butler.zip -d /tmp/butler && \
    chmod +x /tmp/butler/butler && \
    mv /tmp/butler/butler /usr/local/bin/butler && \
    rm -rf /tmp/butler /tmp/butler.zip && \
    butler --help

# Steam SDK
RUN dpkg --add-architecture i386 && \
    apt-get update && \
    apt-get install -y --no-install-recommends lib32gcc-s1 && \
    curl -fsSL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" -o /tmp/steamcmd.tar.gz && \
    tar -xzf /tmp/steamcmd.tar.gz -C /usr/local/bin && \
    ln -sf /usr/local/bin/steamcmd.sh /usr/local/bin/steamcmd && \
    rm -f /tmp/steamcmd.tar.gz && \
    steamcmd +quit && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# PHP
ENV PHP_VERSION=8.2

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        php \
        php-curl \
        php-exif \
        php-imap \
        php-intl \
        php-mbstring \
        php-sockets \
        php-xml \
        php-zip && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Farah
ENV COMPOSE_UNITY="composer -d /var/unity"
ENV COMPOSER_ALLOW_SUPERUSER="1"

ENV UNITY_LOGGING="stdin stdout stderr"
ENV UNITY_ACCELERATOR_ENDPOINT=""
ENV UNITY_NO_GRAPHICS="1"

COPY --chmod=755 --from=composer:2 /usr/bin/composer /usr/local/bin/composer
COPY --chmod=755 unity/composer.json /var/unity/
COPY --chmod=755 unity/config /var/unity/config/
COPY --chmod=755 unity/compose-unity /usr/local/bin/

RUN compose-unity update --no-interaction --no-dev --optimize-autoloader --classmap-authoritative && \
    compose-unity exec unity-build

# .NET SDK
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
ENV DOTNET_CLI_UI_LANGUAGE=en
ENV PATH="/root/.dotnet/tools:${PATH}"
RUN curl -fsSL https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb && \
    dpkg -i /tmp/packages-microsoft-prod.deb && \
    rm /tmp/packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y --no-install-recommends dotnet-sdk-8.0 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    dotnet --version

# DocFX
ENV FrameworkPathOverride="/usr/lib/mono/4.7.1-api/"
RUN apt-get update && \
    apt-get install -y --no-install-recommends mono-devel && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    dotnet tool update -g docfx

# Blender
ARG BLENDER_SERIES=4.5

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
      xz-utils \
      libxfixes3 \
      libxi6 \
      libxxf86vm1 \
      libxrender1 \
      libxkbcommon0 \
      libsm6 \
      libice6 \
      libgl1 \
      libegl1 \
      libdbus-1-3 \
      libfontconfig1 \
      libfreetype6 \
      libglib2.0-0; \
    rm -rf /var/lib/apt/lists/*; \
    arch="$(dpkg --print-architecture)"; \
    case "$arch" in \
      amd64) blender_arch="x64" ;; \
      arm64) blender_arch="arm64" ;; \
      *) echo "Unsupported architecture: $arch" >&2; exit 1 ;; \
    esac; \
    base_url="https://download.blender.org/release/Blender${BLENDER_SERIES}/"; \
    version="$(curl -fsSL "$base_url" \
      | grep -oE "blender-${BLENDER_SERIES}\.[0-9]+-linux-${blender_arch}\.tar\.xz" \
      | sed -E 's/^blender-([0-9]+\.[0-9]+\.[0-9]+)-linux-.*$/\1/' \
      | sort -V \
      | tail -n1)"; \
    test -n "$version"; \
    curl -fsSL "${base_url}blender-${version}-linux-${blender_arch}.tar.xz" -o /tmp/blender.tar.xz; \
    rm -rf /opt/blender-*; \
    tar -xJf /tmp/blender.tar.xz -C /opt; \
    ln -sfn "/opt/blender-${version}-linux-${blender_arch}/blender" /usr/local/bin/blender; \
    rm -f /tmp/blender.tar.xz; \
    blender --version

# Unity Hub
RUN install -m 0755 -d /etc/apt/keyrings && \
    curl -fsSL https://hub.unity3d.com/linux/keys/public | gpg --dearmor -o /etc/apt/keyrings/unityhub.gpg && \
    chmod a+r /etc/apt/keyrings/unityhub.gpg && \
    echo "deb [arch=amd64 signed-by=/etc/apt/keyrings/unityhub.gpg] https://hub.unity3d.com/linux/repos/deb stable main" \
        > /etc/apt/sources.list.d/unityhub.list && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
        apparmor \
        cpio \
        ffmpeg \
        libasound2 \
        libc6-dev \
        libgbm-dev \
        libgconf-2-4 \
        libglu1-mesa \
        libncurses5 \
        libtinfo5 \
        p7zip-full \
        python3 \
        unityhub=3.12.1 \
        xvfb \
        xz-utils && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# VNC Server
ENV USER="root"
ENV RESOLUTION="1920x1080"
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        dbus-x11 \
        tightvncserver \
        xfce4 \
        xfce4-goodies \
        xfonts-base && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Test
RUN compose-unity exec unity-help
