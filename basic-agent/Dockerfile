FROM debian:bookworm-slim

ARG DEBIAN_FRONTEND=noninteractive

USER root

# Linux Tools
RUN apt update
RUN apt install -y \
    apt-transport-https \
    wget \
    zip \
    nano \
    gnupg2 \
    ca-certificates \
    software-properties-common \
    curl
RUN apt upgrade -y

# UTF-8
ENV LANG=C.UTF-8
RUN apt install -y locales && \
    echo "$LANG UTF-8" > /etc/locale.gen && \
    locale-gen $LANG

# Git
RUN apt install -y \
    git \
    git-lfs

# Plastic
RUN echo "deb https://www.plasticscm.com/plasticrepo/stable/ubuntu/ ./" > /etc/apt/sources.list.d/plasticscm-stable.list
RUN wget https://www.plasticscm.com/plasticrepo/stable/ubuntu/Release.key -O - | apt-key add -
RUN apt update
RUN apt install -y plasticscm-client-core

# Jenkins
WORKDIR /var
COPY jenkins-agent /usr/local/bin/jenkins-agent
RUN chmod +x /usr/local/bin/jenkins-agent
ENTRYPOINT ["/usr/local/bin/jenkins-agent"]

# Java
ENV JAVA_VERSION=17
RUN apt install -y openjdk-$JAVA_VERSION-jre

# Test
RUN java --version
