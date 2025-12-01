// Azure SQL Database with Entra ID (Azure AD) Only Authentication
// Complies with MCAPS governance policy for SQL DB security

@description('Location for all resources')
param location string = 'uksouth'

@description('Base name for the resources')
param baseName string = 'expensemgmt'

@description('The unique suffix for resource names')
param uniqueSuffix string = uniqueString(resourceGroup().id)

@description('The Azure AD admin login (User Principal Name)')
param adminLogin string

@description('The Azure AD admin Object ID')
param adminObjectId string

@description('The Managed Identity Principal ID for database access')
param managedIdentityPrincipalId string

// Generate lowercase resource names
var sqlServerName = toLower('sql-${baseName}-${uniqueSuffix}')
var sqlDatabaseName = 'Northwind'

// SQL Server with Entra ID Only Authentication
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

// SQL Database - Basic tier for development
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648 // 2GB
  }
}

// Firewall rule to allow Azure services
resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Outputs
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output connectionString string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
