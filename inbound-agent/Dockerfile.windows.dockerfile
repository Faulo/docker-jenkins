FROM jenkins/inbound-agent:jdk17-windowsservercore-ltsc2019

SHELL ["C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell", "-NonInteractive", "-NoProfile", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Jenkins
USER ContainerAdministrator
WORKDIR C:\\jenkins
ENV JAVA_OPTS="-Dhudson.model.DirectoryBrowserSupport.CSP=\"\""

# Git
RUN git config --global --add safe.directory *

# Docker
ENV DOCKER_VERSION=29.4.0
ENV DOCKER_HOST=npipe:////./pipe/docker_engine

RUN curl.exe -fsSL "https://download.docker.com/win/static/stable/x86_64/docker-$env:DOCKER_VERSION.zip" -o docker.zip; \
    Expand-Archive -Path docker.zip -DestinationPath . -Force; \
    Remove-Item docker.zip -Force; \
    "[Environment]::SetEnvironmentVariable('PATH', 'C:\\jenkins\\docker;'+$env:PATH, [EnvironmentVariableTarget]::Machine)"

# Plastic
ARG PLASTIC_URL=https://www.plasticscm.com/download/downloadinstaller/last/plasticscm/windows/cloudedition

RUN curl.exe -fsSL $env:PLASTIC_URL -o plastic-installer.exe; \
    Start-Process \
      -FilePath plastic-installer.exe \
      -ArgumentList '--mode unattended --unattendedmodeui none' \
      -Wait -NoNewWindow; \
    Remove-Item plastic-installer.exe -Force
