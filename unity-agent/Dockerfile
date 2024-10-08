FROM faulo/ci-agent:latest

ARG DEBIAN_FRONTEND=noninteractive

# .NET SDK
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="1"
RUN wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt update && \
    apt install -y dotnet-sdk-7.0 && \
    dotnet --version

# DocFX
ENV FrameworkPathOverride="/usr/lib/mono/4.7.1-api/"
RUN apt install -y mono-devel && \
	dotnet tool update -g docfx

# Unity
RUN echo "deb https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list && \
    wget https://hub.unity3d.com/linux/keys/public -O - | apt-key add - && \
    apt update && \
    apt install -y \
    libgbm-dev \
    libasound2 \
    libgconf-2-4 \
    libtinfo5 \
    libc6-dev \
    libncurses5 \
    libglu1 \
    xvfb \
    cpio \
    p7zip-full \
    blender \
    ffmpeg \
    python3 \
    apparmor \
    xz-utils \
    unityhub

# Unity 2021 openssl compatibility
RUN wget http://archive.ubuntu.com/ubuntu/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2_amd64.deb && \
    dpkg -i libssl1.1_1.1.1f-1ubuntu2_amd64.deb && \
    rm libssl1.1_1.1.1f-1ubuntu2_amd64.deb

# VNC Server
ENV USER="root"
ENV RESOLUTION="1920x1080"
RUN apt install -y \
    xfce4 \
    xfce4-goodies \
    tightvncserver \
    dbus-x11 \
    xfonts-base

# Test
RUN compose-unity exec unity-help
