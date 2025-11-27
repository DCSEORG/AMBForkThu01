# Azure Services Architecture

This diagram shows how the Azure services in this repository connect to each other when deployed.

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                              Azure Resource Group                                    │
│                              (rg-expensemgmt-demo)                                   │
│                                                                                      │
│  ┌───────────────────────────────────────────────────────────────────────────────┐  │
│  │                         User Assigned Managed Identity                         │  │
│  │                        (mid-appmodassist-xxxxxxx)                              │  │
│  │                                                                                │  │
│  │  • Provides authentication for App Service to access other Azure services     │  │
│  │  • No passwords or secrets needed - uses Azure AD tokens                      │  │
│  └───────────────────────────────────────────────────────────────────────────────┘  │
│                                       │                                              │
│                    ┌──────────────────┼──────────────────┐                          │
│                    │                  │                  │                          │
│                    ▼                  ▼                  ▼                          │
│  ┌────────────────────┐  ┌───────────────────┐  ┌────────────────────┐             │
│  │   Azure App Service │  │   Azure SQL DB    │  │  Azure OpenAI      │             │
│  │  (app-expensemgmt-) │  │ (sql-expensemgmt-)│  │ (aoai-expensemgmt-)│             │
│  │                     │  │                   │  │                    │             │
│  │  • Hosts ASP.NET    │  │  • Northwind DB   │  │  • GPT-4o Model    │             │
│  │    Razor Pages App  │  │  • Expenses data  │  │  • Sweden Central  │             │
│  │  • S1 Standard SKU  │  │  • Basic tier     │  │  • S0 SKU          │             │
│  │  • HTTPS only       │  │  • Entra ID auth  │  │  • Chat completion │             │
│  │                     │  │    only           │  │                    │             │
│  └─────────┬───────────┘  └────────┬──────────┘  └─────────┬──────────┘             │
│            │                       │                       │                         │
│            │                       │                       │                         │
│            │  ┌────────────────────┘                       │                         │
│            │  │                                            │                         │
│            ▼  ▼                                            ▼                         │
│  ┌─────────────────────────────────────────────┐  ┌────────────────────┐            │
│  │              Data Flow                       │  │   AI Search        │            │
│  │                                              │  │ (search-expensemgmt)│           │
│  │  1. User accesses App Service                │  │                    │            │
│  │  2. App uses Managed Identity to get token   │  │  • RAG pattern     │            │
│  │  3. App calls SQL via stored procedures      │  │  • Basic tier      │            │
│  │  4. App calls OpenAI for chat                │  │  • Index data      │            │
│  │  5. OpenAI uses function calling to          │  │                    │            │
│  │     interact with SQL via App Service APIs   │  │                    │            │
│  └─────────────────────────────────────────────┘  └────────────────────┘            │
│                                                                                      │
└─────────────────────────────────────────────────────────────────────────────────────┘

                                    │
                                    │ HTTPS (Port 443)
                                    ▼
                    ┌───────────────────────────────┐
                    │           Users               │
                    │                               │
                    │  • Web browser access         │
                    │  • /Index for main app        │
                    │  • /swagger for API docs      │
                    │  • Chat UI for AI assistant   │
                    └───────────────────────────────┘
```

## Deployment Options

### Basic Deployment (`deploy.sh`)
Deploys:
- App Service with Managed Identity
- Azure SQL Database
- Database schema and stored procedures
- Application code

Chat UI will show demo responses.

### Full Deployment with Chat (`deploy-with-chat.sh`)
Deploys everything above PLUS:
- Azure OpenAI with GPT-4o model
- AI Search service
- Full AI-powered chat functionality

## Security Features

1. **No SQL Authentication** - Entra ID only (MCAPS compliant)
2. **Managed Identity** - No secrets in code or config
3. **HTTPS Only** - All traffic encrypted
4. **Function Calling** - AI interacts with data via secure APIs

## Authentication Flow

```
┌────────┐         ┌─────────────┐         ┌─────────────┐
│  User  │ ──────▶ │ App Service │ ──────▶ │   Azure AD  │
└────────┘         └─────────────┘         └─────────────┘
                          │                       │
                          │ Token Request         │ Token
                          │ (Managed Identity)    │
                          ▼                       ▼
                   ┌─────────────┐         ┌─────────────┐
                   │   SQL DB    │ ◀────── │  Token Auth │
                   └─────────────┘         └─────────────┘
```
