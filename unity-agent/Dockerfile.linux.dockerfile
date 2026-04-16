FROM faulo/ci-agent:latest-linux

ARG DEBIAN_FRONTEND=noninteractive

# .NET SDK
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="1"
ENV DOTNET_CLI_UI_LANGUAGE="en"
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
