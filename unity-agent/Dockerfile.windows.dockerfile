FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-ltsc2019

SHELL ["C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell", "-NonInteractive", "-NoProfile", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Jenkins
WORKDIR C:\\jenkins

ENV JENKINS_URL=http://jenkins:8080/
ENV JENKINS_SECRET=InsertSecretHere
ENV JENKINS_AGENT_NAME=InsertAgentNameHere
ENV JENKINS_JAVA_OPTS=-Dorg.jenkinsci.plugins.gitclient.Git.timeOut=60

COPY jenkins-agent.bat .

ENTRYPOINT ["cmd", "/c", "C:\\jenkins\\jenkins-agent.bat"]

# Chocolatey
RUN [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; \
    iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))

# Tools
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT="1"
ENV DOTNET_CLI_UI_LANGUAGE="en"
ENV DOCKER_HOST=npipe:////./pipe/docker_engine
ENV JAVA_VERSION=21
ENV JAVA_OPTS="-Dhudson.model.DirectoryBrowserSupport.CSP=\"\""

COPY unity-agent.nuspec unity-agent.nuspec

RUN choco pack unity-agent.nuspec; \
    choco install unity-agent --no-progress --yes --ignore-checksums --source '.;https://community.chocolatey.org/api/v2/'; \
    Remove-Item -Force *.nuspec; \
    Remove-Item -Force *.nupkg

# PHP
COPY php.ini C:\\tools\\php82\\custom.ini
RUN Get-Content C:\\tools\\php82\\custom.ini | Add-Content -Path C:\\tools\\php82\\php.ini

# GPU support
COPY system32/* C:\\Windows\\System32\\

# Test
RUN git config --global --add safe.directory *; \
    curl.exe --version; \
    git --version; \    
    php --version; \
    composer --version; \
    butler --version; \
    npm --version; \
    dotnet --version; \
    docker --version; \
    java --version

# Plastic
ARG PLASTIC_URL=https://www.plasticscm.com/download/downloadinstaller/last/plasticscm/windows/cloudedition

RUN curl.exe -fsSL $env:PLASTIC_URL -o plastic-installer.exe; \
    Start-Process \
      -FilePath plastic-installer.exe \
      -ArgumentList '--mode unattended --unattendedmodeui none' \
      -Wait -NoNewWindow; \
    Remove-Item plastic-installer.exe -Force

# Farah
ENV COMPOSE_UNITY="composer -d C:\\unity"
ENV COMPOSER_ALLOW_SUPERUSER="1"
COPY unity/composer.json C:\\unity\\
COPY unity/config C:\\unity\\config\\
COPY unity/compose-unity.bat C:\\Windows\\
RUN compose-unity update --no-interaction --no-dev --optimize-autoloader --classmap-authoritative; \
    compose-unity exec unity-build
