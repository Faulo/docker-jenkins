setlocal
cd %~dp0
set DOCKER_CONTEXT=dende
set DOCKER_IMAGE=inbound-agent
for /f %%i in ('docker --context %DOCKER_CONTEXT% info --format "{{.OSType}}"') do SET DOCKER_OS=%%i
pushd %DOCKER_IMAGE%
call docker --context %DOCKER_CONTEXT% build -t tmp/%DOCKER_IMAGE%:latest . -f Dockerfile.%DOCKER_OS%.dockerfile
popd
endlocal
pause