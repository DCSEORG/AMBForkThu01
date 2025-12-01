// App Service with User-Assigned Managed Identity
// Deploys to UKSOUTH with Standard S1 SKU to avoid cold start issues

@description('Location for all resources')
param location string = 'uksouth'

@description('Base name for the resources')
param baseName string = 'expensemgmt'

@description('The unique suffix for resource names')
param uniqueSuffix string = uniqueString(resourceGroup().id)

// Generate lowercase resource names
var appServicePlanName = toLower('asp-${baseName}-${uniqueSuffix}')
var appServiceName = toLower('app-${baseName}-${uniqueSuffix}')
var managedIdentityName = toLower('mid-appmodassist-${uniqueSuffix}')

// User-Assigned Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

// App Service Plan with S1 SKU
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
    capacity: 1
  }
  properties: {
    reserved: false // Windows
  }
}

// App Service
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
    }
  }
}

// Outputs
output appServiceName string = appService.name
output appServiceHostName string = appService.properties.defaultHostName
output managedIdentityName string = managedIdentity.name
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityResourceId string = managedIdentity.id
