FROM openjdk:21-bookworm

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

# Git
RUN apt install -y \
	git \
	git-lfs

# Plastic
RUN echo "deb https://www.plasticscm.com/plasticrepo/stable/ubuntu/ ./" > /etc/apt/sources.list.d/plasticscm-stable.list
RUN wget https://www.plasticscm.com/plasticrepo/stable/ubuntu/Release.key -O - | apt-key add -
RUN apt update
RUN apt install -y plasticscm-client-core

# Linux Tools
RUN apt upgrade -y

# Jenkins
WORKDIR /var
COPY jenkins-agent /usr/local/bin/jenkins-agent
RUN chmod +x /usr/local/bin/jenkins-agent
ENTRYPOINT ["/usr/local/bin/jenkins-agent"]