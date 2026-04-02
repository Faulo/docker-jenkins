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
    rm -rf /var/lib/apt/lists/*
RUN dotnet --version

# DocFX
ENV FrameworkPathOverride="/usr/lib/mono/4.7.1-api/"
RUN apt-get update && \
    apt-get install -y --no-install-recommends mono-devel && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    dotnet tool update -g docfx

# Blender
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        libxkbcommon-x11-0 \
        jq \
        tar \
        xz-utils \
        libglu1-mesa 
        libxi6 \
        libxrender1 \
        libxrandr2 \
        libxcursor1 \
        libxinerama1 \
        libxxf86vm1 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    mkdir /blender && \
  if [ -z ${BLENDER_VERSION+x} ]; then \
    BLENDER_VERSION=$(curl -s https://projects.blender.org/api/v1/repos/blender/blender/tags \
      | jq -r '.[] | select(.name | contains("-rc") | not) | .name' \
      | sed 's|^v||g' | sort -rV | head -1); \
  fi && \
  BLENDER_FOLDER=$(echo "Blender${BLENDER_VERSION}" | sed -r 's|(Blender[0-9]*\.[0-9]*)\.[0-9]*|\1|') && \
  curl -o \
    /tmp/blender.tar.xz -fL \
    "https://mirror.clarkson.edu/blender/release/${BLENDER_FOLDER}/blender-${BLENDER_VERSION}-linux-x64.tar.xz" || \
    curl -o \
      /tmp/blender.tar.xz -fL \
      "https://mirrors.iu13.net/blender/release/${BLENDER_FOLDER}/blender-${BLENDER_VERSION}-linux-x64.tar.xz" && \
  tar xf \
    /tmp/blender.tar.xz -C \
    /blender/ --strip-components=1 && \
  ln -s \
    /blender/blender \
    /usr/bin/blender && \
  rm -rf \
    /tmp/* \
    /var/lib/apt/lists/* \
    /var/tmp/*
RUN blender --version

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

# Unity 2021 OpenSSL compatibility
RUN curl -fsSL http://archive.ubuntu.com/ubuntu/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2_amd64.deb -o /tmp/libssl1.1.deb && \
    dpkg -i /tmp/libssl1.1.deb && \
    rm /tmp/libssl1.1.deb

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
