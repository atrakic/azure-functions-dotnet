MAKEFLAGS += --silent
BASEDIR = $(shell git rev-parse --show-toplevel)

ifeq ($(origin APP_NAME), undefined)
APP_NAME = $(shell whoami)
export APP_NAME
endif

.PHONY: up logs infra deploy test.local test clean

up: start

start:
	pushd $(BASEDIR)/src/api; func start; popd

deploy: infra
	sleep 1
	pushd $(BASEDIR)/src/api; func azure functionapp publish $(APP_NAME); popd

logs:
	pushd $(BASEDIR)/src/api; func azure functionapp logstream $(APP_NAME); popd

infra:
	$(BASEDIR)/infra/deploy.sh

test.local:
	curl -sL -X GET http://localhost:7071/HealthCheck
	curl -X POST http://localhost:7071/postevents --data '{"name":"demo"}'
	curl -H "Accept: application/json" -X GET http://localhost:7071/GetEvents

test:
	curl -sL -X GET https://$(APP_NAME).azurewebsites.net/HealthCheck
	curl -X POST https://$(APP_NAME).azurewebsites.net/postevents --data '{"name":"demo"}'
	curl -H "Accept: application/json" -X GET https://$(APP_NAME).azurewebsites.net/GetEvents

clean:
	pushd $(BASEDIR)/src/api; dotnet clean; popd
	rm -rf $(BASEDIR)/src/api/bin/output/events.db
	az group delete --name $(RG)
