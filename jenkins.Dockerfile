FROM jenkins/jenkins:lts-jdk11

USER root

# Linux Tools
COPY machine-id /etc/machine-id
RUN apt update
RUN apt install -y apt-transport-https
RUN apt install -y wget
RUN apt install -y zip
RUN apt install -y nano

# Jenkins
RUN mkdir -m 777 /var/workspace

# Git
RUN apt install -y git

# .NET SDK
RUN wget https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb
RUN rm packages-microsoft-prod.deb
RUN apt update
RUN apt install -y dotnet-sdk-6.0
RUN apt install -y dotnet-sdk-7.0

# DocFX
RUN dotnet tool update -g docfx
RUN apt install -y mono-devel
ENV FrameworkPathOverride "/usr/lib/mono/4.7.1-api/"

# Android SDK+NDK
# RUN apt install -y android-sdk
# COPY --from=bitriseio/android-ndk /opt/android-sdk-linux /opt/android-sdk-linux
# COPY --from=bitriseio/android-ndk /opt/android-ndk /opt/android-ndk
# ENV ANDROID_SDK_ROOT /opt/android-sdk-linux
# ENV ANDROID_NDK_ROOT /opt/android-ndk

# Plastic
RUN echo "deb https://www.plasticscm.com/plasticrepo/stable/ubuntu/ ./" > /etc/apt/sources.list.d/plasticscm-stable.list
RUN wget https://www.plasticscm.com/plasticrepo/stable/ubuntu/Release.key -O - | apt-key add -
RUN apt update
RUN apt install -y plasticscm-client-core

# NodeJS
RUN curl -sL https://deb.nodesource.com/setup_18.x | bash -
RUN apt install -y nodejs

# PHP
RUN apt install -y php7.4
RUN apt install -y php-xsl
RUN apt install -y php-fileinfo
RUN apt install -y php-sockets
RUN apt install -y php-exif
RUN apt install -y php-intl
RUN apt install -y php-dom
RUN apt install -y php-zip
RUN apt install -y php-curl
RUN apt install -y php-mbstring

# Unity
RUN echo "deb https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list
RUN wget https://hub.unity3d.com/linux/keys/public -O - | apt-key add -
RUN apt update
RUN apt install -y libgbm-dev
RUN apt install -y libasound2
RUN apt install -y libgconf-2-4
RUN apt install -y libtinfo5
RUN apt install -y libc6-dev
RUN apt install -y libncurses5
RUN apt install -y xvfb
RUN apt install -y cpio
RUN apt install -y p7zip-full
RUN apt install -y blender
RUN apt install -y ffmpeg
RUN apt install -y python2
RUN apt install -y python3
RUN apt install -y unityhub

# Steam SDK
RUN dpkg --add-architecture i386
RUN apt update
RUN apt install -y lib32gcc-s1
COPY steam/steamcmd /bin
RUN chmod -R 777 /bin
RUN steamcmd --help
# COPY steam/sdk /var/steam
# RUN chmod -R 777 /var/steam
# RUN /var/steam/setup.sh --target=amd64 --release --auto-update

# Linux Tools
RUN apt upgrade -y

# itch.io
COPY itch.io/butler /bin
RUN chmod -R 777 /bin

# Farah
COPY --from=composer:2 /usr/bin/composer /usr/bin/composer
COPY unity/composer.* /var/unity/
RUN chmod -R 777 /var/unity
ENV COMPOSE_UNITY "composer -d /var/unity exec"

USER jenkins

# Farah
RUN composer -d /var/unity install --no-dev