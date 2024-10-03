FROM faulo/ci-agent:latest

# .NET SDK
RUN wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN rm packages-microsoft-prod.deb
RUN apt update
RUN apt install -y dotnet-sdk-7.0

# DocFX
RUN dotnet tool update -g docfx
RUN apt install -y mono-devel
ENV FrameworkPathOverride="/usr/lib/mono/4.7.1-api/"

# Unity
RUN echo "deb https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list
RUN wget https://hub.unity3d.com/linux/keys/public -O - | apt-key add -
RUN apt update
RUN apt install -y \
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
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="1"
ENV JAVA_OPTS="-Dhudson.model.DirectoryBrowserSupport.CSP=\"\""

# Unity 2021 openssl compatibility
RUN wget http://archive.ubuntu.com/ubuntu/pool/main/o/openssl/libssl1.1_1.1.1f-1ubuntu2_amd64.deb
RUN dpkg -i libssl1.1_1.1.1f-1ubuntu2_amd64.deb
RUN rm libssl1.1_1.1.1f-1ubuntu2_amd64.deb

# VNC Server
RUN apt install -y \
	xfce4 \
	xfce4-goodies \
	tightvncserver \
	dbus-x11 \
	xfonts-base
ENV USER="root"
ENV RESOLUTION="1920x1080"