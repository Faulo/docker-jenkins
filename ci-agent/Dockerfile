FROM faulo/basic-agent:latest

COPY machine-id /etc/machine-id

# NodeJS
RUN curl -sL https://deb.nodesource.com/setup_18.x | bash -
RUN apt install -y nodejs

# itch.io
RUN curl -L https://broth.itch.ovh/butler/linux-amd64/LATEST/archive/default -o butler.zip
RUN unzip butler.zip
RUN chmod +x butler
RUN mv butler /usr/local/bin
RUN rm butler.zip
RUN butler --help

# Steam SDK
RUN dpkg --add-architecture i386
RUN apt update
RUN apt install -y lib32gcc-s1
COPY steamcmd /usr/local/bin/
RUN chmod +x /usr/local/bin/steamcmd /usr/local/bin/linux32/steamcmd
RUN steamcmd --help

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
COPY --from=composer:2 /usr/bin/composer /usr/local/bin/composer
COPY unity/composer.json /var/unity/
COPY unity/config /var/unity/config/
COPY unity/compose-unity /usr/local/bin/
RUN chmod +x /usr/local/bin/compose-unity
RUN chmod -R 777 /var/unity
ENV COMPOSE_UNITY="composer -d /var/unity"
ENV COMPOSER_ALLOW_SUPERUSER="1"
RUN compose-unity update --no-dev
RUN compose-unity exec unity-build