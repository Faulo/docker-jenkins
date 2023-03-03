pipeline {
	agent any

	options {
		disableConcurrentBuilds()
		disableResume()
	}
	
	stages {
		stage('Build') {
			steps {
				bat "docker-compose build --pull"
			}
		}
		stage('Deploy') {
			when {
				branch 'main'
			}
			steps {
				bat "docker version"
			}
		}
    }
}