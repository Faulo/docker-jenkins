pipeline {
	agent any

	options {
		disableConcurrentBuilds()
		disableResume()
	}
	
	stages {
		stage('Build') {
			steps {
				callShell "docker-machine start default"
				callShell "docker-compose build --pull"
			}
		}
		stage('Deploy') {
			when {
				branch 'main'
			}
			steps {
				callShell "docker version"
			}
		}
    }
}