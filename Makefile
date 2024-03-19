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
	curl -sL -X GET http://localhost:7071/HealthCheck
	curl -X POST http://localhost:7071/postevents --data '{"name":"demo"}'
	curl -H "Accept: application/json" -X GET http://localhost:7071/GetEvents

clean:
	pushd $(BASEDIR)/src/api; dotnet clean; popd
	rm -rf $(BASEDIR)/src/api/bin/output/events.db
	az group delete --name $(RG)
