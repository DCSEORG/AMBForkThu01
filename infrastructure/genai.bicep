// GenAI Resources for Expense Management Chat UI
// Deploys Azure OpenAI (in Sweden Central for quota) and AI Search

@description('Location for AI Search (main region)')
param location string

@description('Resource name suffix for uniqueness')
param nameSuffix string

@description('Managed Identity Principal ID for role assignments')
param managedIdentityPrincipalId string

// Azure OpenAI must be deployed to Sweden Central for GPT-4o availability and quota
var openAILocation = 'swedencentral'
var openAIName = 'aoai-expensemgmt-${nameSuffix}'
var searchName = 'search-expensemgmt-${nameSuffix}'
var modelDeploymentName = 'gpt-4o'

// Azure OpenAI Account
resource openAI 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: toLower(openAIName)
  location: openAILocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: toLower(openAIName)
    publicNetworkAccess: 'Enabled'
  }
}

// GPT-4o Model Deployment
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
      version: '2024-08-06'
    }
  }
}

// AI Search for RAG pattern
resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: toLower(searchName)
  location: location
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
resource openAIUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, 'Cognitive Services OpenAI User')
  scope: openAI
  properties: {
    principalId: managedIdentityPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd') // Cognitive Services OpenAI User
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Search Index Data Contributor for Managed Identity
resource searchContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, managedIdentityPrincipalId, 'Search Index Data Contributor')
  scope: aiSearch
  properties: {
    principalId: managedIdentityPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7') // Search Index Data Contributor
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Search Service Contributor for Managed Identity
resource searchServiceContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, managedIdentityPrincipalId, 'Search Service Contributor')
  scope: aiSearch
  properties: {
    principalId: managedIdentityPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7ca78c08-252a-4471-8644-bb5ff32d4ba0') // Search Service Contributor
    principalType: 'ServicePrincipal'
  }
}

output openAIEndpoint string = openAI.properties.endpoint
output openAIName string = openAI.name
output openAIModelName string = gpt4oDeployment.name
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'
output searchName string = aiSearch.name
