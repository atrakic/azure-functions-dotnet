#!/usr/bin/env bash

: "${RG:?'You need to configure the RG variable!'}"
: "${APP_NAME:?'You need to configure the APP_NAME variable!'}"
: "${STORAGE_NAME:?'You need to configure the STORAGE_NAME variable!'}"
: "${LOCATION:=westeurope}"

set -x
set -o errexit
#set -o nounset
#set -o pipefail

premiumPlan="$APP_NAME-PremiumPlan"
skuStorage="Standard_LRS" # Allowed values: Standard_LRS, Standard_GRS, Standard_RAGRS, Standard_ZRS, Premium_LRS, Premium_ZRS, Standard_GZRS, Standard_RAGZRS
functionsVersion="4"
skuPlan="EP1"

# shellcheck disable=SC2046
if [ $(az group exists --name "$RG") = false ]; then

  echo "You are about to create Azure resources on account: "
  az account show

  if [[ -n "${FORCE}" ]]; then
    echo -n "Are you sure? Press <Enter> to continue or <Ctrl+C> to abort "
    read -r _
  fi

  ## https://docs.microsoft.com/azure/azure-resource-manager/resource-group-create-service-principal-portal
  if [[ -n "${CREATE_AZURE_SP}" ]]; then
    # shellcheck disable=SC2219
    let "randomIdentifier=$RANDOM*$RANDOM"
    servicePrincipalName="$APP_NAME-$randomIdentifier"
    subscriptionId=$(az account show --query id --output tsv)

    az ad sp create-for-rbac --name "$servicePrincipalName" --role contributor \
      --scopes /subscriptions/"$subscriptionId"/resourceGroups/"$APP_NAME"/providers/Microsoft.Web/sites/"$APP_NAME" \
      --json-auth | tee .credenials.json
  fi

  az group create --name "$RG" --location "$LOCATION"

  az storage account create --name "$STORAGE_NAME" \
    --sku "$skuStorage" \
    --allow-blob-public-access false \
    --resource-group "$RG" --location "$LOCATION"

  if [ -n "${PREMIUM_PLAN}" ]; then
    az functionapp plan create \
      --name "$premiumPlan" \
      --sku "$skuPlan" \
      --resource-group "$RG" --location "$LOCATION"

    az functionapp create --name "$APP_NAME" \
      --runtime dotnet-isolated \
      --plan "$premiumPlan" \
      --functions-version "$functionsVersion" \
      --storage-account "$STORAGE_NAME" \
      --resource-group "$RG"
  else
    az functionapp create --name "$APP_NAME" \
      --runtime dotnet-isolated \
      --functions-version "$functionsVersion" \
      --storage-account "$STORAGE_NAME" \
      --resource-group "$RG" \
      --consumption-plan-location "$LOCATION"
  fi

  if [[ -n "${COSMOSDB_NAME}" ]]; then
    az cosmosdb create --name "$COSMOSDB_NAME" \
      --kind MongoDB failoverPriority=0 isZoneRedundant=False \
      --resource-group "$RG" --location "$LOCATION"
  fi

else
  echo "Resource group: $RG already exists"
  #[[ -n "${DEBUG}" ]] && az functionapp show --name "$APP_NAME" --resource-group "$RG"
fi
