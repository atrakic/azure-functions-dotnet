MAKEFLAGS += --silent
BASEDIR = $(shell git rev-parse --show-toplevel)

ifeq ($(origin APP_NAME), undefined)
APP_NAME = $(shell whoami)
export APP_NAME
endif

.PHONY: up logs infra deploy test.local clean

up: start

start:
	pushd $(BASEDIR)/src/api; func start; popd

deploy: infra
	pushd $(BASEDIR)/src/api; func azure functionapp publish $(APP_NAME); popd

logs:
	pushd $(BASEDIR)/src/api; func azure functionapp logstream $(APP_NAME); popd

infra:
	$(BASEDIR)/infra/deploy.sh

test.local:
	curl --request POST http://localhost:7071/HttpApi --data '{"name":"Azure Rocks"}'
	curl -H "Accept: application/json" --request GET http://localhost:7071/HttpApi

clean:
	az group delete --name $(RG)
	pushd $(BASEDIR)/api/src; dotnet clean; popd
