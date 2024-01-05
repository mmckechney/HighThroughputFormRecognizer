param keyvault string
param serviceBusName string
param serviceBusAuthRuleName string
param docIntelKeyArray array
param recognizerEndpoint string
param formStorageAccountName string


resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: formStorageAccountName
}

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-06-01-preview' existing = {
  name: serviceBusName
}

resource sbauthRule 'Microsoft.ServiceBus/namespaces/AuthorizationRules@2022-10-01-preview' existing = {
  name: serviceBusAuthRuleName
  parent: serviceBusNamespace

}
resource serviceBusConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: '${keyvault}/SERVICE-BUS-CONNECTION'
  properties: {
    value: sbauthRule.listKeys().primaryConnectionString
  }
}

resource formRecognizerKeySecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: '${keyvault}/FORM-RECOGNIZER-KEY'
  properties: {
    value:    join(docIntelKeyArray,'|')
  }
}

resource formRecognizerEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: '${keyvault}/FORM-RECOGNIZER-ENDPOINT'
  properties: {
    value: recognizerEndpoint
  }
}

resource storageKeySecret 'Microsoft.KeyVault/vaults/secrets@2021-06-01-preview' = {
  name: '${keyvault}/STORAGE-KEY'
  properties: {
    value: storageAccount.listKeys().keys[0].value
  }
}
