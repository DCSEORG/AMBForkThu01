// Azure OpenAI and AI Search resources for GenAI Chat UI
// Uses managed identity for authentication

@description('Location for GenAI resources - Sweden Central for GPT-4o availability')
param location string = 'swedencentral'

@description('Base name for the resources')
param baseName string = 'expensemgmt'

@description('The unique suffix for resource names')
param uniqueSuffix string = uniqueString(resourceGroup().id)

@description('The Managed Identity Principal ID for role assignments')
param managedIdentityPrincipalId string

// Generate lowercase resource names
var openAIName = toLower('oai-${baseName}-${uniqueSuffix}')
var searchName = toLower('search-${baseName}-${uniqueSuffix}')
var modelDeploymentName = 'gpt-4o'

// Azure OpenAI resource
resource openAI 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: openAIName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// GPT-4o model deployment
resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
  parent: openAI
  name: modelDeploymentName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-05-13' // Using stable version for POC - update as newer versions become available
    }
    raiPolicyName: 'Microsoft.Default'
  }
}

// Azure AI Search (Cognitive Search)
resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: 'uksouth' // Search in same region as other resources
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

// Role assignment: Cognitive Services OpenAI User for Managed Identity
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, 'Cognitive Services OpenAI User')
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd') // Cognitive Services OpenAI User
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Search Index Data Contributor for Managed Identity
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, managedIdentityPrincipalId, 'Search Index Data Contributor')
  scope: search
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7') // Search Index Data Contributor
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Search Service Contributor for Managed Identity
resource searchServiceRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, managedIdentityPrincipalId, 'Search Service Contributor')
  scope: search
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0') // Search Service Contributor
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output openAIEndpoint string = openAI.properties.endpoint
output openAIName string = openAI.name
output openAIModelName string = modelDeploymentName
output searchEndpoint string = 'https://${search.name}.search.windows.net'
output searchName string = search.name
