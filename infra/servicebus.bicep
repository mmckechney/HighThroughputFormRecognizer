
param serviceBusNs string
param formQueueName string
param processedQueueName string
param location string = resourceGroup().location


resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-06-01-preview' = {
  name: serviceBusNs
  location: location
  sku: {
    name: 'Standard'
  }
}

resource serviceBusFormQueue 'Microsoft.ServiceBus/namespaces/queues@2021-06-01-preview' = {
  name: formQueueName
  parent: serviceBusNamespace
  properties: {
    enablePartitioning: true
    maxSizeInMegabytes: 4096
  }
}

resource serviceBusProcessedQueue 'Microsoft.ServiceBus/namespaces/queues@2021-06-01-preview' = {
  name: processedQueueName
  parent: serviceBusNamespace
  properties: {
    enablePartitioning: true
    maxSizeInMegabytes: 4096
  }
}

resource serviceBusAuthorizationRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2021-06-01-preview' = {
  name: 'FormProcessFuncRule'
  parent: serviceBusNamespace
  properties: {
    rights: [
      'Listen'
      'Send'
    ]
  }
}

output serviceBusId string = serviceBusNamespace.id
output authorizationRuleName string = serviceBusAuthorizationRule.name
