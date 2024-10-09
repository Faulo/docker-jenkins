FROM debian:bookworm-slim

ARG DEBIAN_FRONTEND=noninteractive

USER root

# Linux Tools
RUN apt update && \
    apt upgrade -y && \
    apt install -y \
    apt-transport-https \
    wget \
    zip \
    nano \
    gnupg2 \
    ca-certificates \
    software-properties-common \
    curl \
	tini \
    pciutils

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
RUN echo "deb https://www.plasticscm.com/plasticrepo/stable/ubuntu/ ./" > /etc/apt/sources.list.d/plasticscm-stable.list && \
    wget https://www.plasticscm.com/plasticrepo/stable/ubuntu/Release.key -O - | apt-key add - && \
    apt update && \
    apt install -y plasticscm-client-core

# Jenkins
WORKDIR /var
COPY --chmod=755 jenkins-agent.sh /usr/local/bin/jenkins-agent.sh
ENTRYPOINT ["/usr/bin/tini", "--", "/usr/local/bin/jenkins-agent.sh"]

# Java
ENV JAVA_VERSION=17
ENV JAVA_OPTS="-Dhudson.model.DirectoryBrowserSupport.CSP=\"\""
RUN apt install -y openjdk-$JAVA_VERSION-jre

# Test
RUN java --version
