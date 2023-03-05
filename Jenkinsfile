pipeline {
	agent {
		label 'docker'
	}

	options {
		disableConcurrentBuilds()
		disableResume()
	}
	
	stages {
		stage('Build') {
			steps {
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