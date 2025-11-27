// Main Bicep deployment for Expense Management System
// Deploys App Service, Managed Identity, Azure SQL Database, and optionally GenAI resources

@description('Location for all resources')
param location string = 'uksouth'

@description('Resource name suffix for uniqueness')
param nameSuffix string = uniqueString(resourceGroup().id)

@description('Deploy GenAI resources for Chat UI')
param deployGenAI bool = false

@description('Admin Object ID for SQL Server Entra ID authentication')
param adminObjectId string

@description('Admin User Principal Name (email) for SQL Server')
param adminLogin string

// Deploy Managed Identity first
module managedIdentity 'managed-identity.bicep' = {
  name: 'managedIdentityDeployment'
  params: {
    location: location
    nameSuffix: nameSuffix
  }
}

// Deploy App Service with Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    nameSuffix: nameSuffix
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    managedIdentityClientId: managedIdentity.outputs.managedIdentityClientId
  }
}

// Deploy Azure SQL Database
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    nameSuffix: nameSuffix
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
    managedIdentityName: managedIdentity.outputs.managedIdentityName
  }
}

// Conditionally deploy GenAI resources
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeployment'
  params: {
    location: location
    nameSuffix: nameSuffix
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceUrl string = appService.outputs.appServiceUrl
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output databaseName string = azureSql.outputs.databaseName
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = managedIdentity.outputs.managedIdentityPrincipalId

// Conditional GenAI outputs with null-safe operators
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output openAIName string = deployGenAI ? genai.outputs.openAIName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
output searchName string = deployGenAI ? genai.outputs.searchName : ''
