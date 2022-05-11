#az login
param
(
	[Parameter(Mandatory=$true)]
    [string] $appName,
	[Parameter(Mandatory=$true)]
	[string] $location,
	[Parameter(Mandatory=$true)]
	[string] $myPublicIp
)

$apppNameLc = $appName.ToLower()
$resourceGroupName ="rg-$appName-demo-$location"
$serviceBusNs =  "sbns-$appName-demo-$location"
$formStorageAcct = "stor$($apppNameLc)demo$($location)"
$funcStorageAcct = "fstor$($apppNameLc)demo$($location)"
$managedIdentity =  "mgid-$appName-demo-$location"
$formRecognizer =  "frmr-$appName-demo-$location"
$vnet = "vnet-$appName-demo-$location"
$subnet = "subn-$appName-demo-$location"
$nsg =  "nsg-$appName-demo-$location"
$funcsubnet = "subn-$appName-func-demo-$location"
$funcAppPlan = "fcnplan-$appName-demo-$location"
$funcProcess = "fcn-$($appName)Process-demo-$location"
$funcMove = "fcn-$($appName)Mover-demo-$location"
$funcQueue = "fcn-$($appName)Queue-demo-$location"
$keyvault = "kv-$appName-demo-$location"
$formQueueName = "formqueue"
$processedQueueName = "processedqueue"
 

Write-Host "Creating Resource Group" -ForegroundColor DarkCyan
az group create --name $resourceGroupName  --location $location -o table

###########################
## Networking
###########################
Write-Host "Creating Network Security Group" -ForegroundColor DarkCyan
az network nsg create --resource-group $resourceGroupName --name $nsg -o table

Write-Host "Creating Virtual Network" -ForegroundColor DarkCyan
az network vnet create --resource-group $resourceGroupName --name $vnet --address-prefixes 10.10.0.0/16 --subnet-name $subnet --subnet-prefixes 10.10.0.0/24 --network-security-group $nsg -o table
az network vnet subnet update --name $subnet --resource-group $resourceGroupName --vnet-name $vnet --service-endpoints Microsoft.Storage Microsoft.Web -o table

Write-Host "Creating Function Subnet" -ForegroundColor DarkCyan
az network vnet subnet create --resource-group $resourceGroupName --name $funcsubnet --vnet-name $vnet --address-prefixes 10.10.1.0/24 --network-security-group $nsg --delegations Microsoft.Web/serverFarms --service-endpoints Microsoft.Storage Microsoft.Web -o table

###########################
## Service Bus
###########################
Write-Host "Creating Service Bus Namespace" -ForegroundColor DarkCyan
az servicebus namespace create --resource-group $resourceGroupName --name $serviceBusNs --sku Standard -o table
$serviceBusId =  az servicebus namespace show --resource-group $resourceGroupName --name $serviceBusNs -o tsv --query id	

Write-Host "Creating Service Bus Queue" -ForegroundColor DarkCyan
az servicebus queue create --resource-group $resourceGroupName --name $formQueueName --namespace-name $serviceBusNs --enable-partitioning $true --max-size 4096 -o table
az servicebus queue create --resource-group $resourceGroupName --name $processedQueueName --namespace-name $serviceBusNs --enable-partitioning $true --max-size 4096 -o table

Write-Host "Creating Service Bus Queue Authorization Rule" -ForegroundColor DarkCyan
az servicebus namespace authorization-rule create --resource-group $resourceGroupName --namespace-name $serviceBusNs  --name FormProcessFuncRule --rights Listen Send -o table
$sbConnString = az servicebus namespace authorization-rule keys list --resource-group $resourceGroupName --namespace-name $serviceBusNs  --name FormProcessFuncRule -o tsv --query primaryConnectionString

###########################
## Storage Account
###########################
Write-Host "Creating Forms Storage Account" -ForegroundColor DarkCyan
az storage account create --resource-group $resourceGroupName --name $formStorageAcct --sku Standard_LRS --kind StorageV2 --location $location --allow-blob-public-access $false --default-action Deny -o table
$storageKey =  az storage account keys list --account-name $formStorageAcct -o tsv --query [0].value
$storageId =  az storage account show --resource-group $resourceGroupName --name $formStorageAcct -o tsv --query id

Write-Host "Creating Storage VNET Network Rules" -ForegroundColor DarkCyan
az storage account network-rule add  --account-name $formStorageAcct --vnet-name $vnet --subnet $funcsubnet -o table
az storage account network-rule add  --account-name $formStorageAcct --vnet-name $vnet --subnet $subnet -o table

Write-Host "Creating Storage Local IP Network Rules" -ForegroundColor DarkCyan
az storage account network-rule add  --account-name $formStorageAcct --ip-address $myPublicIp -o table

Write-Host "Creating Storage Containers" -ForegroundColor DarkCyan
az storage container create --name "incoming" --account-name $formStorageAcct --account-key $storageKey -o table
az storage container create --name "processed" --account-name $formStorageAcct --account-key $storageKey -o table
az storage container create --name "output" --account-name $formStorageAcct --account-key $storageKey -o table


###########################
## Function storage account
###########################
Write-Host "Creating Function App Storage Account" -ForegroundColor DarkCyan
az storage account create --resource-group $resourceGroupName --name $funcStorageAcct --sku Standard_LRS --kind StorageV2 --location $location -o table

###########################
## Form Recognizer
###########################
Write-Host "Creating Formn Recognizer Account" -ForegroundColor DarkCyan
az cognitiveservices account create --kind FormRecognizer --location $location --name $formRecognizer --resource-group $resourceGroupName --sku S0 -o table
$recognizerKey =  az cognitiveservices account keys list --name $formRecognizer --resource-group $resourceGroupName -o tsv --query key1
$recognizerEndpoint = az cognitiveservices account show --name $formRecognizer --resource-group $resourceGroupName -o tsv --query properties.endpoints.FormRecognizer

az cognitiveservices account identity assign  --name $formRecognizer --resource-group $resourceGroupName 
$formRecoId = az cognitiveservices account identity show --name $formRecognizer --resource-group $resourceGroupName -o tsv --query principalId

###########################
## Key vault
###########################
Write-Host "Creating Key Vault" -ForegroundColor DarkCyan
az keyvault create --name $keyvault --resource-group $resourceGroupName --enable-purge-protection $true --enable-rbac-authorization $true -o table
$keyvaultId = az keyvault show  --name $keyvault --resource-group $resourceGroupName -o tsv --query id
$keyVaultUri  =  az keyvault show  --name $keyvault --resource-group $resourceGroupName -o tsv --query properties.vaultUri

Write-Host "Setting current user Key Vault Access Policy" -ForegroundColor DarkCyan
$currentUser = az account show -o tsv --query "user.name"
$currentUserObjectId = az ad user show --id $currentUser -o tsv --query objectId
az role assignment create --role "Key Vault Secrets Officer" --assignee $currentUserObjectId --scope $keyvaultId -o table


###########################
## Function App plan
###########################
Write-Host "Creating Function App Plan" -ForegroundColor DarkCyan
az functionapp plan create --name $funcAppPlan --resource-group $resourceGroupName --sku EP1 --max-burst 4 -o table


###########################
## Form Procesor Function
###########################
Write-Host "Creating Form Procesor Function App" -ForegroundColor DarkCyan
az functionapp create --resource-group $resourceGroupName --plan $funcAppPlan --runtime dotnet --functions-version 4  --name $funcProcess --storage-account $funcStorageAcct -o table

Write-Host "Creating Form Procesor Function App VNET Integration" -ForegroundColor DarkCyan
az functionapp vnet-integration add --name $funcProcess --resource-group $resourceGroupName --vnet $vnet --subnet $funcsubnet

Write-Host "Updating Form Procesor Function App settings" -ForegroundColor DarkCyan
az functionapp config appsettings set --name $funcProcess --resource-group $resourceGroupName -o none --settings "FORM_RECOGNIZER_SOURCE_CONTAINER_NAME=incoming" "FORM_RECOGNIZER_PROCESSED_CONTAINER_NAME=processed" "FORM_RECOGNIZER_OUTPUT_CONTAINER_NAME=output" "FORM_RECOGNIZER_STORAGE_ACCOUNT_NAME=$formStorageAcct" "FORM_RECOGNIZER_MODEL_NAME=prebuilt-document" "FORM_RECOGNIZER_ENDPOINT=""$frEndpointKvReference""" "FORM_RECOGNIZER_KEY=""$frKeyKvReference""" "SERVICE_BUS_CONNECTION=""$sbConnKvReference""" "SERVICE_BUS_PROCESSED_QUEUE_NAME=$processedQueueName" "SERVICE_BUS_NAMESPACE_NAME=$serviceBusNs"

Write-Host "Assigning Form Procesor Function App managed identity" -ForegroundColor DarkCyan
az functionapp identity assign --name $funcProcess --resource-group $resourceGroupName --identities [system] -o table
$funcProcessId = az functionapp identity show --name $funcProcess --resource-group $resourceGroupName -o tsv --query principalId

###########################
## File Mover Function
###########################
Write-Host "Creating File Mover Function App" -ForegroundColor DarkCyan
az functionapp create --resource-group $resourceGroupName --plan $funcAppPlan --runtime dotnet --functions-version 4  --name $funcMove --storage-account $funcStorageAcct -o table

Write-Host "Creating File Mover Function App VNET integration" -ForegroundColor DarkCyan
az functionapp vnet-integration add --name $funcMove --resource-group $resourceGroupName --vnet $vnet --subnet $funcsubnet

Write-Host "Updating File Mover Function App settings" -ForegroundColor DarkCyan
az functionapp config appsettings set --name $funcMove --resource-group $resourceGroupName -o none --settings "FORM_RECOGNIZER_SOURCE_CONTAINER_NAME=incoming" "FORM_RECOGNIZER_PROCESSED_CONTAINER_NAME=processed" "FORM_RECOGNIZER_STORAGE_ACCOUNT_NAME=$formStorageAcct" "SERVICE_BUS_CONNECTION=""$sbConnKvReference""" 

Write-Host "Assigning File Mover Function App managed identity" -ForegroundColor DarkCyan
az functionapp identity assign --name $funcMove --resource-group $resourceGroupName --identities [system] -o table
$funcMoveId = az functionapp identity show --name $funcMove --resource-group $resourceGroupName -o tsv --query principalId

###########################
## File Queue Function
###########################
Write-Host "Creating File Queue Function App" -ForegroundColor DarkCyan
az functionapp create --resource-group $resourceGroupName --plan $funcAppPlan --runtime dotnet --functions-version 4  --name $funcQueue --storage-account $funcStorageAcct -o table

Write-Host "Creating File Queue Function App VNET integration" -ForegroundColor DarkCyan
az functionapp vnet-integration add --name $funcQueue --resource-group $resourceGroupName --vnet $vnet --subnet $funcsubnet

Write-Host "Updating File Queue Function App settings" -ForegroundColor DarkCyan
az functionapp config appsettings set --name $funcQueue --resource-group $resourceGroupName -o none --settings "FORM_RECOGNIZER_SOURCE_CONTAINER_NAME=incoming" "FORM_RECOGNIZER_STORAGE_ACCOUNT_NAME=$formStorageAcct" "SERVICE_BUS_NAMESPACE_NAME=$serviceBusNs" "SERVICE_BUS_QUEUE_NAME=$formQueueName"

Write-Host "Assigning File Queue Function App managed identity" -ForegroundColor DarkCyan
az functionapp identity assign --name $funcQueue --resource-group $resourceGroupName --identities [system] -o table
$funcQueueId = az functionapp identity show --name $funcQueue --resource-group $resourceGroupName -o tsv --query principalId


###########################
## Role Assignments
###########################
Write-Host "Adding Role Assignments For Procesor Function" -ForegroundColor DarkCyan
az role assignment create --role "Storage Blob Data Contributor" --assignee $funcProcessId --scope $storageId  -o table
az role assignment create --role "Storage Blob Data Owner" --assignee $funcProcessId --scope $storageId  -o table
az role assignment create --role "Azure Service Bus Data Owner" --assignee $funcProcessId --scope $serviceBusId -o table
az role assignment create --role "Key Vault Secrets User" --assignee $funcProcessId --scope $keyvaultId -o table

Write-Host "Adding Role Assignments For Form Recognizer " -ForegroundColor DarkCyan
az role assignment create --role "Storage Blob Data Reader" --assignee $formRecoId --scope $storageId -o table

Write-Host "Adding Role Assignments For File Mover Function " -ForegroundColor DarkCyan
az role assignment create --role "Storage Blob Data Contributor" --assignee $funcMoveId --scope $storageId  -o table
az role assignment create --role "Storage Blob Data Reader" --assignee $funcMoveId --scope $storageId  -o table
az role assignment create --role "Azure Service Bus Data Owner" --assignee $funcMoveId --scope $serviceBusId -o table
az role assignment create --role "Key Vault Secrets User" --assignee $funcMoveId --scope $keyvaultId -o table

Write-Host "Adding Role Assignments For File Queue Function " -ForegroundColor DarkCyan
az role assignment create --role "Storage Blob Data Contributor" --assignee $funcQueueId --scope $storageId  -o table
az role assignment create --role "Storage Blob Data Reader" --assignee $funcQueueId --scope $storageId  -o table
az role assignment create --role "Azure Service Bus Data Owner" --assignee $funcQueueId --scope $serviceBusId -o table

Write-Host "Adding Role Assignments For Current User " -ForegroundColor DarkCyan
az role assignment create --role "Storage Blob Data Contributor" --assignee $currentUserObjectId --scope $storageId  -o table
az role assignment create --role "Azure Service Bus Data Owner" --assignee $currentUserObjectId --scope $serviceBusId -o table



###########################
## Key Vault Secrets
###########################

Write-Host "Adding Key Vault Secrets" -ForegroundColor DarkCyan
az keyvault secret set --name "SERVICE-BUS-CONNECTION" --value $sbConnString --vault $keyvault -o tsv --query name
az keyvault secret set --name "FORM-RECOGNIZER-KEY" --value $recognizerKey --vault $keyvault -o tsv --query name
az keyvault secret set --name "FORM-RECOGNIZER-ENDPOINT" --value $recognizerEndpoint --vault $keyvault -o tsv --query name
az keyvault secret set --name "STORAGE-KEY" --value $storageKey --vault $keyvault -o tsv --query name

$sbConnKvReference = "@Microsoft.KeyVault(SecretUri=$($keyVaultUri)secrets/SERVICE-BUS-CONNECTION/)"
$frEndpointKvReference = "@Microsoft.KeyVault(SecretUri=$($keyVaultUri)secrets/FORM-RECOGNIZER-ENDPOINT/)"
$frKeyKvReference = "@Microsoft.KeyVault(SecretUri=$($keyVaultUri)secrets/FORM-RECOGNIZER-KEY/)"

###########################
## Code Deployment
###########################

$scriptDir = Split-Path $script:MyInvocation.MyCommand.Path

Write-Host "Deploying Form Procesor Function App" -ForegroundColor DarkCyan
dotnet publish "FormProcessorFunction/"
$source = $scriptDir + "/FormProcessorFunction/bin/Debug/net6.0/publish"
$zip = $scriptDir + "build.zip"
if(Test-Path $zip) { Remove-Item $zip }
[io.compression.zipfile]::CreateFromDirectory($source,$zip)
az webapp deploy --name $funcProcess --resource-group $resourceGroupName --src-path $zip --type zip


Write-Host "Deploying File Mover Function App" -ForegroundColor DarkCyan
dotnet publish "ProcessedFileMover/"
$source = $scriptDir + "/ProcessedFileMover/bin/Debug/net6.0/publish"
if(Test-Path $zip) { Remove-Item $zip }
[io.compression.zipfile]::CreateFromDirectory($source,$zip)
az webapp deploy --name $funcMove --resource-group $resourceGroupName --src-path $zip --type zip


Write-Host "Deploying File Queue Function App" -ForegroundColor DarkCyan
dotnet publish "FormQueueFunction/"
$source = $scriptDir + "/FormQueueFunction/bin/Debug/net6.0/publish"
if(Test-Path $zip) { Remove-Item $zip }
[io.compression.zipfile]::CreateFromDirectory($source,$zip)
az webapp deploy --name $funcQueue --resource-group $resourceGroupName --src-path $zip --type zip

