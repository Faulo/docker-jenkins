@echo off
setlocal

rem Standard: Werte aus ENV verwenden
set "EFFECTIVE_JENKINS_SECRET=%JENKINS_SECRET%"
set "EFFECTIVE_JENKINS_AGENT_NAME=%JENKINS_AGENT_NAME%"
set "EFFECTIVE_JENKINS_URL=%JENKINS_URL%"

rem Optionale positionsbasierte Overrides:
rem %1 = JENKINS_SECRET
rem %2 = JENKINS_AGENT_NAME
rem %3 = JENKINS_URL

if not "%~1"=="" set "EFFECTIVE_JENKINS_SECRET=%~1"
if not "%~2"=="" set "EFFECTIVE_JENKINS_AGENT_NAME=%~2"
if not "%~3"=="" set "EFFECTIVE_JENKINS_URL=%~3"

echo Downloading agent.jar from %EFFECTIVE_JENKINS_URL%
curl.exe -fsSL "%EFFECTIVE_JENKINS_URL%jnlpJars/agent.jar" -o agent.jar
if errorlevel 1 exit /b %errorlevel%

echo Starting Jenkins agent "%EFFECTIVE_JENKINS_AGENT_NAME%"
java %JENKINS_JAVA_OPTS% -jar agent.jar -url "%EFFECTIVE_JENKINS_URL%" -secret "%EFFECTIVE_JENKINS_SECRET%" -name "%EFFECTIVE_JENKINS_AGENT_NAME%" -workDir "%JENKINS_AGENT_WORKDIR%"

exit /b %errorlevel%