curl !JENKINS_URL!jnlpJars/agent.jar -o agent.jar
java !JENKINS_JAVA_OPTS! -jar agent.jar -url !JENKINS_URL! -secret !JENKINS_SECRET! -name "!JENKINS_AGENT_NAME!" -workDir "!JENKINS_AGENT_WORKDIR!"