#!/usr/bin/env bash

: "${RG:?'You need to configure the RG variable!'}"
: "${APP_NAME:?'You need to configure the APP_NAME variable!'}"
: "${STORAGE_NAME:?'You need to configure the STORAGE_NAME variable!'}"
: "${REGION:=westeurope}"

set -o errexit
set -o nounset
set -o pipefail


echo "You are about to create Azure resources at account: "
az account show

echo -n "Are you sure? (Press <Enter> to continue or <Ctrl+C> to abort) "
read -r _

az group create --name "$RG" --location "$REGION"
  az storage account create --name "$STORAGE_NAME" --resource-group "$RG" --location "$REGION"

az functionapp create --resource-group "$RG" \
  --consumption-plan-location "$REGION" \
  --runtime dotnet-isolated \
  --functions-version 4 \
  --name "$APP_NAME" \
  --storage-account "$STORAGE_NAME"

# : "${COSMOSDB_NAME:?'You need to configure the COSMOSDB_NAME variable!'}"
#az cosmosdb create --name "$COSMOSDB_NAME" --resource-group "$RG" \
# --kind MongoDB --locations regionName="$REGION" failoverPriority=0 isZoneRedundant=False
