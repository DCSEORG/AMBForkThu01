# Azure Services Architecture

This document describes the Azure services deployed by this solution and how they connect.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                            Azure Resource Group                                  │
│                           (rg-expensemgmt-demo)                                 │
│                                                                                  │
│  ┌──────────────────────────────────────────────────────────────────────────┐   │
│  │                        App Service (S1 SKU)                               │   │
│  │                      (app-expensemgmt-xxxx)                               │   │
│  │                                                                           │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐          │   │
│  │  │   Razor Pages   │  │   REST APIs     │  │   Chat Service  │          │   │
│  │  │   (Dashboard,   │  │   (Swagger)     │  │   (AI Chat UI)  │          │   │
│  │  │   Expenses,     │  │                 │  │                 │          │   │
│  │  │   Approvals)    │  │                 │  │                 │          │   │
│  │  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘          │   │
│  │           │                    │                    │                    │   │
│  │           └────────────────────┼────────────────────┘                    │   │
│  │                                │                                          │   │
│  │                    ┌───────────▼───────────┐                             │   │
│  │                    │  User-Assigned        │                             │   │
│  │                    │  Managed Identity     │                             │   │
│  │                    │  (mid-appmodassist-x) │                             │   │
│  │                    └───────────┬───────────┘                             │   │
│  │                                │                                          │   │
│  └────────────────────────────────┼──────────────────────────────────────────┘   │
│                                   │                                              │
│               ┌───────────────────┼───────────────────┐                         │
│               │                   │                   │                         │
│               ▼                   ▼                   ▼                         │
│  ┌────────────────────┐  ┌────────────────────┐  ┌────────────────────┐        │
│  │   Azure SQL        │  │   Azure OpenAI     │  │   Azure AI Search  │        │
│  │   (Basic SKU)      │  │   (S0 SKU)         │  │   (Basic SKU)      │        │
│  │                    │  │   Sweden Central   │  │                    │        │
│  │  ┌──────────────┐  │  │                    │  │                    │        │
│  │  │  Northwind   │  │  │  ┌──────────────┐  │  │  ┌──────────────┐  │        │
│  │  │  Database    │  │  │  │   GPT-4o     │  │  │  │   Search     │  │        │
│  │  │              │  │  │  │   Model      │  │  │  │   Index      │  │        │
│  │  │  - Expenses  │  │  │  └──────────────┘  │  │  └──────────────┘  │        │
│  │  │  - Users     │  │  │                    │  │                    │        │
│  │  │  - Categories│  │  └────────────────────┘  └────────────────────┘        │
│  │  │  - Statuses  │  │                                                        │
│  │  └──────────────┘  │        (Optional - deploy-with-chat.sh)                │
│  │                    │                                                         │
│  └────────────────────┘                                                         │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

## Component Details

### App Service (UK South)
- **SKU**: Standard S1 (avoids cold starts)
- **Runtime**: .NET 8.0 (LTS)
- **Features**: 
  - Always On enabled
  - HTTPS only
  - User-Assigned Managed Identity attached

### User-Assigned Managed Identity
- Used for passwordless authentication to:
  - Azure SQL Database
  - Azure OpenAI
  - Azure AI Search
- Avoids storing secrets in configuration

### Azure SQL Database (UK South)
- **SKU**: Basic (development tier)
- **Authentication**: Entra ID (Azure AD) only
- **Database**: Northwind
- **Tables**: Expenses, Users, Categories, Statuses, Roles
- **Stored Procedures**: All data access via stored procedures

### Azure OpenAI (Sweden Central)
- **SKU**: S0
- **Model**: GPT-4o (capacity: 8)
- **Location**: Sweden Central (for model availability)
- **Authentication**: Managed Identity with Cognitive Services OpenAI User role

### Azure AI Search (UK South)
- **SKU**: Basic
- **Purpose**: Future RAG (Retrieval-Augmented Generation) support
- **Authentication**: Managed Identity with Search Index Data Contributor role

## Data Flow

1. **User Request** → App Service receives HTTP request
2. **App Service** → Uses Managed Identity to authenticate
3. **Database Access** → Calls stored procedures via Managed Identity auth
4. **AI Chat** → Sends prompts to Azure OpenAI with function calling
5. **Function Execution** → AI calls expense APIs to perform operations
6. **Response** → Returns formatted response to user

## Deployment Options

### Basic Deployment (deploy.sh)
Deploys:
- App Service
- Managed Identity
- Azure SQL Database
- Application Code

### Full Deployment (deploy-with-chat.sh)
Deploys everything above plus:
- Azure OpenAI
- Azure AI Search
- Configures GenAI endpoints in App Service
