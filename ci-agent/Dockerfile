FROM faulo/basic-agent:latest

ARG DEBIAN_FRONTEND=noninteractive

COPY machine-id /etc/machine-id

# NodeJS
RUN curl -sL https://deb.nodesource.com/setup_18.x | bash - && \
    apt install -y nodejs

# itch.io
RUN curl -L https://broth.itch.ovh/butler/linux-amd64/LATEST/archive/default -o butler.zip && \
    unzip butler.zip && \
    chmod +x butler && \
    mv butler /usr/local/bin && \
    rm butler.zip && \
    butler --help

# Steam SDK
COPY --chmod=755 steamcmd /usr/local/bin/
RUN dpkg --add-architecture i386 && \
    apt update && \
    apt install -y lib32gcc-s1 && \
    steamcmd --help

# PHP
RUN apt install -y \
    php \
    php-xsl \
    php-fileinfo \
    php-sockets \
    php-exif \
    php-intl \
    php-dom \
    php-zip \
    php-curl \
    php-mbstring

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
