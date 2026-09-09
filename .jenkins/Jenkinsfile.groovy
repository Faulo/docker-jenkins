def assertValue(actual, expected, description) {
    if (actual != expected) {
        error "${description}: expected '${expected}', got '${actual}'"
    }
}

def assertContains(actual, expected, description) {
    if (!actual.contains(expected)) {
        error "${description}: expected output to contain '${expected}', got '${actual}'"
    }
}

def assertNotContains(actual, unexpected, description) {
    if (actual.contains(unexpected)) {
        error "${description}: output contained forbidden value '${unexpected}'"
    }
}

def assertOccurrences(actual, expected, count, description) {
    def occurrences = actual.count(expected)
    if (occurrences != count) {
        error "${description}: expected '${expected}' ${count} time(s), got ${occurrences} in '${actual}'"
    }
}

def exec(command) {
    if (isUnix()) {
        sh command
    } else {
        bat command
    }
}

def execStdout(command) {
    return isUnix()
        ? sh(script: command, returnStdout: true)
        : bat(script: "@${command}", returnStdout: true)
}

def candidateImage() {
    return "$DOCKER_NAMESPACE/$DOCKER_IMAGE"
}

def configPath() {
    return isUnix() ? '/tmp/jenkins-agent-test.yml' : 'C:/jenkins-agent-test.yml'
}

def environmentProbe(variable) {
    return isUnix()
        ? 'env'
        : "-Cmd \"[Environment]::GetEnvironmentVariable('${variable}')\""
}

def javaProbe() {
    return isUnix() ? '/bin/echo' : 'C:/mingit/usr/bin/echo.exe'
}

def withProjectEnvironment(Closure body) {
    def values = readProperties file: '.env'
    withEnv(values.collect { name, value -> "${name}=${value}" }, body)
}

def runContainer(arguments, environment = [], config = null, inheritEntrypoint = true) {
    def environmentArguments = environment.collect { "--env \"${it}\"" }.join(' ')
    def entrypointArgument = inheritEntrypoint ? '' : '--entrypoint=""'
    def containerId = execStdout("docker create ${entrypointArgument} ${environmentArguments} ${candidateImage()} ${arguments}").trim()
    try {
        if (config != null) {
            writeFile file: 'jenkins-agent-test.yml', text: config
            exec "docker cp jenkins-agent-test.yml ${containerId}:${configPath()}"
        }
        exec "docker start ${containerId}"
        def exitCode = execStdout("docker wait ${containerId}").trim()
        def logs = execStdout("docker logs ${containerId} 2>&1")
        return [exitCode: exitCode, logs: logs]
    } finally {
        exec "docker rm --force --volumes ${containerId}"
    }
}

def testWebSocket() {
    def baseEnvironment = [
        'JENKINS_URL=https://jenkins.example.invalid/',
        'JENKINS_SECRET=not-a-secret',
        'JENKINS_AGENT_NAME=argument-probe',
        "JENKINS_JAVA_BIN=${javaProbe()}"
    ]
    def cases = [
        [description: 'default', value: null, enabled: true],
        [description: 'empty', value: '', enabled: true],
        [description: 'whitespace', value: '   ', enabled: true],
        [description: 'one', value: '1', enabled: true],
        [description: 'true', value: 'true', enabled: true],
        [description: 'trimmed mixed-case true', value: ' TrUe ', enabled: true],
        [description: 'zero', value: '0', enabled: false],
        [description: 'false', value: 'false', enabled: false],
        [description: 'trimmed mixed-case false', value: ' FaLsE ', enabled: false]
    ]

    cases.each { testCase ->
        def environment = new ArrayList(baseEnvironment)
        if (testCase.value != null) {
            environment.add("JENKINS_WEB_SOCKET=${testCase.value}")
        }
        def result = runContainer('', environment)

        assertValue(result.exitCode, '0', "WebSocket ${testCase.description} exit code")
        assertOccurrences(
            result.logs,
            '-webSocket',
            testCase.enabled ? 1 : 0,
            "WebSocket ${testCase.description} launch arguments"
        )
    }

    ['yes', 'enabled', 'ture'].each { value ->
        def result = runContainer('', baseEnvironment + ["JENKINS_WEB_SOCKET=${value}"])

        assertValue(result.exitCode, '1', "invalid WebSocket value '${value}' exit code")
        assertContains(result.logs, 'JENKINS_WEB_SOCKET', "invalid WebSocket value '${value}' error")
        assertContains(result.logs, 'true, false, 1, or 0', "invalid WebSocket value '${value}' error")
        assertNotContains(result.logs, value, "invalid WebSocket value '${value}' error")
    }

    def explicit = runContainer('-webSocket', baseEnvironment)
    assertValue(explicit.exitCode, '0', 'explicit WebSocket exit code')
    assertOccurrences(explicit.logs, '-webSocket', 1, 'explicit WebSocket launch arguments')
}

def testEntrypoint() {
    testWebSocket()
    def selectedEnvironment = runContainer(
        environmentProbe('JENKINS_AGENT_NAME'),
        [
            "JENKINS_CONFIG_FILE=${configPath()}",
            'JENKINS_CONFIG_INDEX=selected',
            'JENKINS_AGENT_NAME=from-environment'
        ],
        '''other:
  JENKINS_AGENT_NAME: from-other-index
selected:
  JENKINS_AGENT_NAME: from-selected-index
'''
    )
    assertValue(selectedEnvironment.exitCode, '0', 'indexed environment exit code')
    assertContains(selectedEnvironment.logs, isUnix() ? 'JENKINS_AGENT_NAME=from-selected-index' : 'from-selected-index', 'indexed environment')
    assertNotContains(selectedEnvironment.logs, 'from-other-index', 'unselected environment')

    def directEnvironment = runContainer(
        environmentProbe('JENKINS_AGENT_NAME'),
        ['JENKINS_AGENT_NAME=from-direct-environment']
    )
    assertValue(directEnvironment.exitCode, '0', 'direct environment exit code')
    assertContains(directEnvironment.logs, isUnix() ? 'JENKINS_AGENT_NAME=from-direct-environment' : 'from-direct-environment', 'direct environment')

    def missingIndex = runContainer(
        '--health',
        [
            "JENKINS_CONFIG_FILE=${configPath()}",
            'JENKINS_CONFIG_INDEX=missing'
        ],
        '''selected:
  DO_NOT_LOG: highly-sensitive-value
'''
    )
    assertValue(missingIndex.exitCode, '1', 'missing index exit code')
    assertContains(missingIndex.logs, 'missing', 'missing index error')
    assertNotContains(missingIndex.logs, 'highly-sensitive-value', 'missing index error')

    def malformed = runContainer(
        '--health',
        [
            "JENKINS_CONFIG_FILE=${configPath()}",
            'JENKINS_CONFIG_INDEX=selected'
        ],
        '''selected: [
  DO_NOT_LOG: highly-sensitive-value
'''
    )
    assertValue(malformed.exitCode, '1', 'malformed YAML exit code')
    assertContains(malformed.logs, 'selected', 'malformed YAML error')
    assertNotContains(malformed.logs, 'highly-sensitive-value', 'malformed YAML error')

    def nonMapping = runContainer(
        '--health',
        [
            "JENKINS_CONFIG_FILE=${configPath()}",
            'JENKINS_CONFIG_INDEX=selected'
        ],
        '''selected: highly-sensitive-value
'''
    )
    assertValue(nonMapping.exitCode, '1', 'non-mapping exit code')
    assertContains(nonMapping.logs, 'selected', 'non-mapping error')
    assertNotContains(nonMapping.logs, 'highly-sensitive-value', 'non-mapping error')

    def missingPair = runContainer(
        '--health',
        ["JENKINS_CONFIG_FILE=${configPath()}"],
        '''selected:
  DO_NOT_LOG: highly-sensitive-value
'''
    )
    assertValue(missingPair.exitCode, '1', 'missing configuration pair exit code')
    assertContains(missingPair.logs, 'JENKINS_CONFIG_INDEX', 'missing configuration pair error')
    assertNotContains(missingPair.logs, 'highly-sensitive-value', 'missing configuration pair error')

    def healthWithoutAgent = runContainer('--health')
    assertValue(healthWithoutAgent.exitCode, '1', 'health without managed agent exit code')
}

def testHealthHook() {
    def classPath = isUnix()
        ? '/jenkins/agent-health.jar:/usr/share/jenkins/agent.jar'
        : 'C:/jenkins/agent-health.jar;C:/ProgramData/Jenkins/agent.jar'
    def monitorClass = isUnix()
        ? "'agent.health.AgentHealthHook\$Monitor'"
        : '"agent.health.AgentHealthHook$Monitor"'
    def javap = isUnix() ? 'javap' : 'javap.exe'
    def result = runContainer(
        "${javap} -c -p -classpath \"${classPath}\" ${monitorClass}",
        [],
        null,
        false
    )

    assertValue(result.exitCode, '0', 'health hook bytecode inspection exit code')
    assertContains(result.logs, 'Channel.callAsync', 'health hook control heartbeat')
    assertContains(result.logs, 'PingThread$Ping', 'health hook Remoting ping primitive')
    assertNotContains(result.logs, 'Channel.syncIO', 'health hook I/O-drain barrier')
}

def testImage() {
    testEntrypoint()
    testHealthHook()
    def scriptSuffix = isUnix() ? '' : '.cmd'
    def probes = [
        [command: 'docker --version', expected: 'Docker version 29.'],
        [command: 'git --version', expected: 'git version'],
        [command: 'cm version', expected: '11.'],
        [command: 'pwsh --version', expected: 'PowerShell 7.'],
        [command: 'node --version', expected: 'v24.'],
        [command: "npm${scriptSuffix} --version", expected: '.'],
        [command: "npx${scriptSuffix} -y hello Faulo", expected: 'Hello']
    ]
    probes.each { probe ->
        def result = runContainer(probe.command, [], null, false)
        assertValue(result.exitCode, '0', "${probe.command} exit code")
        assertContains(result.logs, probe.expected, probe.command)
    }
}

properties([
    parameters([
        choice(
            name: 'DOCKER_NAMESPACE',
            choices: ['faulo', 'tmp'],
            description: 'Docker image namespace to test'
        )
    ]),
    disableConcurrentBuilds(),
    disableResume()
])

def hosts = ['Dende', 'Garl']
def dockerNamespace = params.DOCKER_NAMESPACE ?: 'faulo'

stage('Integration Tests') {
    for (def host in hosts) {
        stage("Host: ${host}") {
            node(host) {
                deleteDir()
                checkout scm
                
                catchError(
                    message: "Integration test failed on ${host}",
                    stageResult: 'FAILURE',
                    buildResult: 'FAILURE',
                    catchInterruptions: false
                ) {
                    withEnv([
                        "DOCKER_NAMESPACE=${dockerNamespace}"
                    ]) {
                        withProjectEnvironment {
                            echo "Testing ${candidateImage()} on ${host}"
                            testImage()
                        }
                    }
                }
            }
        }
    }
}
