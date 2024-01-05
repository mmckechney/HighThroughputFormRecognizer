
targetScope = 'subscription'

param appName string
param location string
param myPublicIp string = ''
param docIntelligenceInstanceCount int = 1
param currentUserObjectId string

var appNameLc = toLower(appName)
var resourceGroupName = 'rg-${appName}-demo-${location}'
var serviceBusNs = 'sbns-${appName}-demo-${location}'
var formStorageAcct = 'stor${appNameLc}demo${location}'
var funcStorageAcct = 'fstor${appNameLc}demo${location}'
var formRecognizer = 'docintel-${appName}-demo-${location}'
var vnet = 'vnet-${appName}-demo-${location}'
var subnet = 'subn-${appName}-demo-${location}'
var nsg = 'nsg-${appName}-demo-${location}'
var funcsubnet = 'subn-${appName}-func-demo-${location}'
var funcAppPlan = 'fcnplan-${appName}-demo-${location}'
var funcProcess = 'fcn-${appName}Process-demo-${location}'
var funcMove = 'fcn-${appName}Mover-demo-${location}'
var funcQueue = 'fcn-${appName}Queue-demo-${location}'
var keyvaultName = 'kv-${appName}-demo-${location}'
var formQueueName = 'formqueue'
var processedQueueName = 'processedqueue'

	resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
		name: resourceGroupName
		location: location
	}

	module networking 'networking.bicep' = {
		name: 'networking'
		scope: resourceGroup(resourceGroupName)
		params: {
			vnet: vnet
			subnet: subnet
			nsg: nsg
			funcsubnet: funcsubnet
			location: location
			myPublicIp: myPublicIp
		}
		dependsOn: [
			rg
		]
	}

module storage 'storage.bicep' = {
	name: 'storage'
	scope: resourceGroup(resourceGroupName)
	params: {
		formStorageAcct: formStorageAcct
		funcStorageAcct: funcStorageAcct
		myPublicIp: myPublicIp
		location: location
		subnetIds: networking.outputs.subnetIds
	}
	dependsOn: [
		rg
		networking
	]
}

module aiservices 'aiservices.bicep' = {
	name: 'aiservices'
	scope: resourceGroup(resourceGroupName)
	params: {
		docIntelligenceName: formRecognizer
		docIntelligenceInstanceCount: docIntelligenceInstanceCount
		location: location
	}
	dependsOn: [
		rg
		networking
	]
}

module servicebus 'servicebus.bicep' = {
	name: 'serviceBus'
	scope: resourceGroup(resourceGroupName)
	params: {
		serviceBusNs: serviceBusNs
		location: location
		formQueueName: formQueueName
		processedQueueName: processedQueueName
	}
	dependsOn: [
		rg
		networking
	]
}

module keyvault 'keyvault.bicep' = {
	name: 'keyvault'
	scope: resourceGroup(resourceGroupName)
	params: {
		keyVaultName: keyvaultName
		currentUserObjectId: currentUserObjectId
		location: location
	}
	dependsOn: [
		rg
		networking
	]
}

module functions 'functions.bicep' = {
	name: 'functions'
	scope: resourceGroup(resourceGroupName)
	params: {
		funcAppPlan: funcAppPlan
		processFunctionName: funcProcess
		moveFunctionName: funcMove
		queueFunctionName: funcQueue
		formStorageAcctName: formStorageAcct
		functionStorageAcctName: funcStorageAcct
		processedQueueName: processedQueueName
		serviceBusNs: serviceBusNs
		functionSubnetId: networking.outputs.functionSubnetId
		keyVaultUri: keyvault.outputs.keyVaultUri
		location: location
		formQueueName: formQueueName
	}
	dependsOn: [
		rg
		networking
		storage
		keyvault
		servicebus
	]
}

module roleAssigments 'roleassignments.bicep' = {
	name: 'roleAssigments'
	scope: resourceGroup(resourceGroupName)
	params: {
		docIntelligencePrincipalIds: aiservices.outputs.docIntelligencePrincipalIds
		storageAccountName: formStorageAcct
		moveFunctionId: functions.outputs.moveFunctionId
		processFunctionId: functions.outputs.processFunctionId
		queueFunctionId: functions.outputs.queueFunctionId
	}
	dependsOn: [
		rg
		keyvault
		storage
		aiservices
		servicebus
		functions
		networking
	]
}

module keyvaultSecrets 'keyvaultkeys.bicep' = {
	name: 'keyvaultSecrets'
	scope: resourceGroup(resourceGroupName)
	params: {
		keyvault: keyvaultName
		docIntelKeyArray: aiservices.outputs.docIntellKeyArray
		formStorageAccountName: formStorageAcct
		recognizerEndpoint: aiservices.outputs.docIntellEndpoint
		serviceBusAuthRuleName: servicebus.outputs.authorizationRuleName
		serviceBusName: serviceBusNs

	}
	dependsOn: [
		rg
		keyvault
		aiservices
		storage
		servicebus
		networking
	]
}
