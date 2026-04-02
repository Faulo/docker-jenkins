FROM faulo/basic-agent:latest-linux

ARG DEBIAN_FRONTEND=noninteractive

COPY machine-id /etc/machine-id

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
COPY --chmod=755 --from=composer:2 /usr/bin/composer /usr/local/bin/composer
COPY --chmod=755 unity/composer.json /var/unity/
COPY --chmod=755 unity/config /var/unity/config/
COPY --chmod=755 unity/compose-unity /usr/local/bin/
RUN compose-unity update --no-dev

# Test
RUN compose-unity exec unity-build
