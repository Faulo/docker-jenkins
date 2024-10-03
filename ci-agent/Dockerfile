FROM faulo/basic-agent:latest

# NodeJS
RUN curl -sL https://deb.nodesource.com/setup_18.x | bash -
RUN apt install -y nodejs

# itch.io
RUN curl -L https://broth.itch.ovh/butler/linux-amd64/LATEST/archive/default -o butler.zip
RUN unzip butler.zip
RUN chmod +x butler
RUN mv butler /usr/local/bin
RUN rm butler.zip

# Steam SDK
RUN dpkg --add-architecture i386
RUN apt update
RUN apt install -y lib32gcc-s1
COPY steam/steamcmd /usr/local/bin
RUN chmod +x /usr/local/bin/steamcmd
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
COPY --from=composer:2 /usr/bin/composer /usr/bin/composer
COPY unity/composer.* /var/unity/
RUN chmod -R 777 /var/unity
ENV COMPOSE_UNITY="composer -d /var/unity"
ENV COMPOSER_ALLOW_SUPERUSER="1"

# Farah
RUN composer -d /var/unity update --no-dev