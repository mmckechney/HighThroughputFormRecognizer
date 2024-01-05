
param location string = resourceGroup().location
param formStorageAcct string
param funcStorageAcct string
param myPublicIp string
param subnetIds array




resource formStorageAccount 'Microsoft.Storage/storageAccounts@2021-06-01' = {
  name: formStorageAcct
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    networkAcls: {
      defaultAction: 'Deny'
      ipRules: [
        {
          value: myPublicIp
          action: 'Allow'
        }
      ]
      virtualNetworkRules:[for subnetId in subnetIds: {
        
          id: subnetId
          action: 'Allow'
        }
     ]
      
    }

  }
}

resource formBlobService 'Microsoft.Storage/storageAccounts/blobServices@2021-06-01' = {
  name: 'default'
  parent: formStorageAccount
}

resource formIncomingStorageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-06-01' = {
  name: 'incoming'
  parent: formBlobService

}

resource formProcessedStorageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-06-01' = {
  name: 'processed'
  parent: formBlobService
}

resource formOutputstorageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-06-01' = {
  name: 'output'
  parent: formBlobService
}

resource funcStorageAccount 'Microsoft.Storage/storageAccounts@2021-06-01' = {
  name: funcStorageAcct
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

output storageAccountId string = formStorageAccount.id
