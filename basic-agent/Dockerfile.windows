FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019

SHELL ["powershell", "-NoProfile", "-Command", "$ErrorActionPreference = 'Stop';"]

# Chocolatey
RUN [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; \
    iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))

# Windows Tools
RUN choco install -y --no-progress \
    zip \
    nano \
    curl
RUN curl.exe --version

# Git
RUN choco install -y --no-progress \
    git
RUN git config --global --add safe.directory *

# Plastic
# TODO

# Docker
ENV DOCKER_HOST=npipe:////./pipe/docker_engine
RUN choco install -y --no-progress \
    docker-cli

# Jenkins
ENV JENKINS_URL=http://jenkins:8080/
ENV JENKINS_SECRET=InsertSecretHere
ENV JENKINS_AGENT_NAME=InsertAgentNameHere
ENV JENKINS_JAVA_OPTS=-Dorg.jenkinsci.plugins.gitclient.Git.timeOut=60
WORKDIR C:\\jenkins
COPY jenkins-agent.bat .
ENTRYPOINT ["cmd", "/c", "C:\\jenkins\\jenkins-agent.bat"]

# Java
ENV JAVA_VERSION=17
ENV JAVA_OPTS="-Dhudson.model.DirectoryBrowserSupport.CSP=\"\""
RUN choco install -y --no-progress \
    openjdk$env:JAVA_VERSION
RUN java --version
