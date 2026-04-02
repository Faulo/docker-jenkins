setlocal
cd %~dp0
call load-env
pushd basic-agent
call docker build --isolation=hyperv . -f Dockerfile.%DOCKER_OS_TYPE%.dockerfile -t faulo/basic-agent:latest-%DOCKER_OS_TYPE%
popd
pushd ci-agent
call docker build --isolation=hyperv . -f Dockerfile.%DOCKER_OS_TYPE%.dockerfile -t faulo/ci-agent:latest-%DOCKER_OS_TYPE%
popd
pushd unity-agent
call docker build --isolation=hyperv . -f Dockerfile.%DOCKER_OS_TYPE%.dockerfile -t faulo/unity-agent:latest-%DOCKER_OS_TYPE%
popd
endlocal
pause