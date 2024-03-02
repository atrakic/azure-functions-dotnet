MAKEFLAGS += --silent
BASEDIR = $(shell git rev-parse --show-toplevel)

ifeq ($(origin APP_NAME), undefined)
APP_NAME = $(shell whoami)
export APP_NAME
endif

.PHONY: up logs infra deploy test.local clean

up: start

start:
	func start

deploy: infra
	func azure functionapp publish $(APP_NAME)

logs:
	func azure functionapp logstream $(APP_NAME)

infra:
	$(BASEDIR)/infra/deploy.sh

test.local:
	curl --request POST http://localhost:7071/api/HttpApi --data '{"name":"Azure Rocks"}'
	curl -H "Accept: application/json" --request GET http://localhost:7071/api/HttpApi

clean:
	az group delete --name $(RG)
