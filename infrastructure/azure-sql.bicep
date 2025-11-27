// Azure SQL Database for Expense Management System
// Uses Entra ID (Azure AD) only authentication as required by MCAPS governance policy

@description('Location for the SQL Server')
param location string

@description('Resource name suffix for uniqueness')
param nameSuffix string

@description('Admin Object ID for Entra ID authentication')
param adminObjectId string

@description('Admin User Principal Name (email) for Entra ID authentication')
param adminLogin string

@description('Managed Identity Principal ID for database access')
param managedIdentityPrincipalId string

@description('Managed Identity Name for database user creation')
param managedIdentityName string

var sqlServerName = 'sql-expensemgmt-${nameSuffix}'
var databaseName = 'Northwind'

// SQL Server with Entra ID only authentication
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: toLower(sqlServerName)
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Allow Azure services to access the server
resource firewallRuleAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Database with Basic tier for development
resource database 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2GB
  }
}

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
output managedIdentityNameForDb string = managedIdentityName
