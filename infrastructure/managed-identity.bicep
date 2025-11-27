// User Assigned Managed Identity for Expense Management System
// This identity will be used by App Service to connect to Azure SQL and Azure OpenAI

@description('Location for the managed identity')
param location string

@description('Resource name suffix for uniqueness')
param nameSuffix string

// Get current timestamp for naming (day-hour-minute format)
var timestamp = 'appmodassist'
var managedIdentityName = 'mid-${timestamp}-${nameSuffix}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: toLower(managedIdentityName)
  location: location
}

output managedIdentityId string = managedIdentity.id
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityName string = managedIdentity.name
