// App Service for Expense Management System
// Uses Standard S1 SKU to avoid cold start issues

@description('Location for the App Service')
param location string

@description('Resource name suffix for uniqueness')
param nameSuffix string

@description('User Assigned Managed Identity Resource ID')
param managedIdentityId string

@description('User Assigned Managed Identity Client ID')
param managedIdentityClientId string

var appServicePlanName = 'asp-expensemgmt-${nameSuffix}'
var appServiceName = 'app-expensemgmt-${nameSuffix}'

// App Service Plan with S1 SKU
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: toLower(appServicePlanName)
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
    size: 'S1'
    family: 'S'
    capacity: 1
  }
  properties: {
    reserved: false // Windows
  }
}

// App Service
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: toLower(appServiceName)
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      use32BitWorkerProcess: false
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentityClientId
        }
        {
          name: 'ManagedIdentityClientId'
          value: managedIdentityClientId
        }
      ]
    }
  }
}

output appServiceName string = appService.name
output appServiceUrl string = 'https://${appService.properties.defaultHostName}'
output appServicePlanName string = appServicePlan.name
