param funcAppPlan string
param location string = resourceGroup().location
param processFunctionName string
param functionSubnetId string
param functionStorageAcctName string
param keyVaultUri string
param processedQueueName string
param serviceBusNs string
param formStorageAcctName string
param moveFunctionName string
param queueFunctionName string
param formQueueName string

var sbConnKvReference = '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/SERVICE-BUS-CONNECTION/)'
var frEndpointKvReference = '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/FORM-RECOGNIZER-ENDPOINT/)'
var frKeyKvReference = '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/FORM-RECOGNIZER-KEY/)'

resource functionAppPlan 'Microsoft.Web/serverfarms@2021-01-01' = {
  name: funcAppPlan
  location: location
  sku: {
    name: 'EP1'
    capacity: 4 
  }
  properties: {
    reserved: false 
  }
}

resource funcStorageAcct 'Microsoft.Storage/storageAccounts@2021-04-01'existing = {
  name: functionStorageAcctName
}
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${funcStorageAcct.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${funcStorageAcct.listKeys().keys[0].value}'

resource processFunction 'Microsoft.Web/sites@2021-01-01' = {
  name: processFunctionName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    virtualNetworkSubnetId: functionSubnetId
    serverFarmId: functionAppPlan.id
    siteConfig: {
      cors: {
        allowedOrigins: ['https://portal.azure.com']
      }
      use32BitWorkerProcess: false
      netFrameworkVersion: 'v8.0'
      remoteDebuggingEnabled: true
      appSettings: [
        {
          name: 'FORM_RECOGNIZER_SOURCE_CONTAINER_NAME'
          value: 'incoming'
        }
        {
          name: 'FORM_RECOGNIZER_PROCESSED_CONTAINER_NAME'
          value: 'processed'
        }
        {
          name: 'FORM_RECOGNIZER_OUTPUT_CONTAINER_NAME'
          value: 'output'
        }
        {
          name: 'FORM_RECOGNIZER_STORAGE_ACCOUNT_NAME'
          value: formStorageAcctName
        }
        {
          name: 'FORM_RECOGNIZER_MODEL_NAME'
          value: 'prebuilt-document'
        }
        {
          name: 'FORM_RECOGNIZER_ENDPOINT'
          value: frEndpointKvReference
        }
        {
          name: 'FORM_RECOGNIZER_KEY'
          value: frKeyKvReference
        }
        {
          name: 'SERVICE_BUS_CONNECTION'
          value: sbConnKvReference
        }
        {
          name: 'SERVICE_BUS_PROCESSED_QUEUE_NAME'
          value: processedQueueName
        }
        {
          name: 'SERVICE_BUS_NAMESPACE_NAME'
          value: serviceBusNs
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: storageConnectionString
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

resource moveFunction 'Microsoft.Web/sites@2021-01-01' = {
  name: moveFunctionName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    virtualNetworkSubnetId: functionSubnetId
    serverFarmId: functionAppPlan.id
    siteConfig: {
      cors: {
        allowedOrigins: ['https://portal.azure.com']
      }
      use32BitWorkerProcess: false
      netFrameworkVersion: 'v8.0'
      remoteDebuggingEnabled: true
      appSettings: [
        {
          name: 'FORM_RECOGNIZER_SOURCE_CONTAINER_NAME'
          value: 'incoming'
        }
        {
          name: 'FORM_RECOGNIZER_PROCESSED_CONTAINER_NAME'
          value: 'processed'
        }
        {
          name: 'FORM_RECOGNIZER_STORAGE_ACCOUNT_NAME'
          value: formStorageAcctName
        }
        {
          name: 'SERVICE_BUS_CONNECTION'
          value: sbConnKvReference
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: storageConnectionString
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'SERVICE_BUS_PROCESSED_QUEUE_NAME'
          value: processedQueueName
        }
        {
          name: 'SERVICE_BUS_MOVE_QUEUE_NAME'
          value: moveFunctionName
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

resource queueFunction 'Microsoft.Web/sites@2021-01-01' = {
  name: queueFunctionName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    virtualNetworkSubnetId: functionSubnetId
    serverFarmId: functionAppPlan.id
    siteConfig: {
      cors: {
        allowedOrigins: ['https://portal.azure.com']
      }
      use32BitWorkerProcess: false
      netFrameworkVersion: 'v8.0'
      remoteDebuggingEnabled: true
      appSettings: [
        {
          name: 'FORM_RECOGNIZER_SOURCE_CONTAINER_NAME'
          value: 'incoming'
        }
        {
          name: 'FORM_RECOGNIZER_RAWFILES_CONTAINER_NAME'
          value: 'rawfiles'
        }
        {
          name: 'FORM_RECOGNIZER_STORAGE_ACCOUNT_NAME'
          value: formStorageAcctName
        }
        {
          name: 'SERVICE_BUS_NAMESPACE_NAME'
          value: serviceBusNs
        }
        {
          name: 'SERVICE_BUS_QUEUE_NAME'
          value: formQueueName
        }
        {
          name: 'SERVICE_BUS_RAW_QUEUE_NAME'
          value: 'rawqueue'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: storageConnectionString
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}


output queueFunctionId string = queueFunction.identity.principalId
output moveFunctionId string = moveFunction.identity.principalId
output processFunctionId string = processFunction.identity.principalId
