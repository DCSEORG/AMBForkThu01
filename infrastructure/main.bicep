// Main Bicep template for Expense Management System
// Deploys App Service, Azure SQL, and optionally GenAI resources

@description('Location for all resources')
param location string = 'uksouth'

@description('Base name for the resources')
param baseName string = 'expensemgmt'

@description('The Azure AD admin login (User Principal Name)')
param adminLogin string

@description('The Azure AD admin Object ID')
param adminObjectId string

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

// Generate unique suffix
var uniqueSuffix = uniqueString(resourceGroup().id)

// Deploy App Service and Managed Identity
module appService 'app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
  }
}

// Deploy Azure SQL Database
module azureSql 'azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    adminLogin: adminLogin
    adminObjectId: adminObjectId
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Conditionally deploy GenAI resources
module genai 'genai.bicep' = if (deployGenAI) {
  name: 'genaiDeployment'
  params: {
    location: 'swedencentral' // GPT-4o availability
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output appServiceName string = appService.outputs.appServiceName
output appServiceHostName string = appService.outputs.appServiceHostName
output appServiceUrl string = 'https://${appService.outputs.appServiceHostName}'
output managedIdentityName string = appService.outputs.managedIdentityName
output managedIdentityClientId string = appService.outputs.managedIdentityClientId
output managedIdentityPrincipalId string = appService.outputs.managedIdentityPrincipalId
output managedIdentityResourceId string = appService.outputs.managedIdentityResourceId
output sqlServerName string = azureSql.outputs.sqlServerName
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn
output sqlDatabaseName string = azureSql.outputs.sqlDatabaseName
output connectionString string = azureSql.outputs.connectionString

// GenAI outputs (conditionally available)
output openAIEndpoint string = deployGenAI ? genai.outputs.openAIEndpoint : ''
output openAIName string = deployGenAI ? genai.outputs.openAIName : ''
output openAIModelName string = deployGenAI ? genai.outputs.openAIModelName : ''
output searchEndpoint string = deployGenAI ? genai.outputs.searchEndpoint : ''
output searchName string = deployGenAI ? genai.outputs.searchName : ''
