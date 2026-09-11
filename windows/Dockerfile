ARG OS_BASE=ltsc2019

FROM mcr.microsoft.com/dotnet/sdk:9.0-nanoserver-1809 AS build

WORKDIR C:/solution

ENV NUGET_PACKAGES=C:/nuget/packages

COPY . .

RUN dotnet publish \
        --configuration Release \
        --runtime win-x64 \
        --self-contained true \
        --property:PublishSingleFile=true \
        --property:UseAppHost=true \
        --property:DebugType=None

FROM mcr.microsoft.com/dotnet/framework/runtime:4.8-windowsservercore-${OS_BASE}

SHELL ["C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell", "-NonInteractive", "-NoProfile", "-Command", "$ErrorActionPreference = 'Stop'; $ProgressPreference = 'SilentlyContinue';"]

# Runtime tools
RUN Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1')); \
    if (-not (Get-Command choco -ErrorAction SilentlyContinue)) { throw 'Chocolatey installation failed' }

COPY windows/jenkins-agent.nuspec C:/build/jenkins-agent.nuspec
COPY windows/tools/ C:/build/tools/

RUN choco pack C:/build/jenkins-agent.nuspec --output-directory C:/build --limit-output; \
    if ($LASTEXITCODE -ne 0) { throw ('choco pack jenkins-agent failed with exit code {0}' -f $LASTEXITCODE) }; \
    choco install jenkins-agent --yes --no-progress --limit-output --source 'C:/build;https://community.chocolatey.org/api/v2/'; \
    if ($LASTEXITCODE -notin 0, 1641, 3010) { throw ('choco install jenkins-agent failed with exit code {0}' -f $LASTEXITCODE) }; \
    Remove-Item -Recurse -Force C:/build

RUN java.exe -version; \
    if ($LASTEXITCODE -ne 0 -or -not $env:JAVA_HOME) { throw 'Java smoke check failed' }; \
    git.exe --version; \
    if ($LASTEXITCODE -ne 0) { throw 'Git smoke check failed' }; \
    git.exe lfs version; \
    if ($LASTEXITCODE -ne 0) { throw 'Git LFS smoke check failed' }; \
    pwsh.exe --version; \
    if ($LASTEXITCODE -ne 0) { throw 'PowerShell smoke check failed' }; \
    node.exe --version; \
    if ($LASTEXITCODE -ne 0) { throw 'Node.js smoke check failed' }; \
    npm.cmd --version; \
    if ($LASTEXITCODE -ne 0) { throw 'npm smoke check failed' }; \
    npx.cmd --version; \
    if ($LASTEXITCODE -ne 0) { throw 'npx smoke check failed' }; \
    docker.exe --version; \
    if ($LASTEXITCODE -ne 0) { throw 'Docker CLI smoke check failed' }; \
    cm.exe version; \
    if ($LASTEXITCODE -ne 0) { throw 'Unity Version Control smoke check failed' }

# The hook uses reflection so it remains compatible with the controller-supplied Remoting JAR.
COPY common/AgentHealthHook/AgentHealthHook.java common/AgentHealthHook/MANIFEST.MF C:/build/agent-health/

RUN New-Item -ItemType Directory -Path C:/build/agent-health/classes, C:/jenkins -Force | Out-Null; \
    javac.exe -d C:/build/agent-health/classes C:/build/agent-health/AgentHealthHook.java; \
    if ($LASTEXITCODE -ne 0) { throw 'Failed to compile Jenkins Remoting health hook' }; \
    jar.exe cfm C:/jenkins/agent-health.jar C:/build/agent-health/MANIFEST.MF \
      -C C:/build/agent-health/classes .; \
    if ($LASTEXITCODE -ne 0) { throw 'Failed to package Jenkins Remoting health hook' }; \
    Remove-Item -LiteralPath C:/build/agent-health -Recurse -Force

# Jenkins
USER ContainerAdministrator
ENV USER=ContainerAdministrator \
    HOME=C:/Users/ContainerAdministrator \
    AGENT_WORKDIR=C:/jenkins \
    JENKINS_AGENT_WORKDIR=C:/jenkins \
    JAVA_OPTS="-Dorg.jenkinsci.plugins.gitclient.Git.timeOut=60" \
    DOCKER_HOST=npipe:////./pipe/docker_engine
WORKDIR C:/jenkins
RUN New-Item -ItemType Directory -Path C:/jenkins/workspace -Force | Out-Null; \
    git config --global --add safe.directory *
VOLUME ["C:/jenkins/workspace"]

COPY --from=build C:/solution/common/Agent/bin/Release/net9.0/win-x64/publish/agent.exe C:/jenkins/agent.exe

HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD ["C:/jenkins/agent.exe", "--health"]

ENTRYPOINT ["C:/jenkins/agent.exe"]
