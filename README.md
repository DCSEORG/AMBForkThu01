![Header image](https://github.com/DougChisholm/App-Mod-Booster/blob/main/repo-header-booster.png)

# Expense Management System

A modern ASP.NET 8 Razor Pages application for managing expenses, with AI-powered chat functionality using Azure OpenAI.

## Features

- **Dashboard**: View expense summaries, pending approvals, and recent expenses
- **Expense Management**: Create, view, and submit expenses for approval
- **Approval Workflow**: Managers can approve or reject submitted expenses
- **AI Chat Assistant**: Natural language interface to interact with the expense system (requires GenAI deployment)
- **REST APIs**: Full API access with Swagger documentation

## Quick Start

### Prerequisites

1. Azure CLI installed and logged in (`az login`)
2. Azure subscription with permissions to create resources
3. .NET 8 SDK (for local development)

### Deployment

1. **Clone this repository**:
   ```bash
   git clone <repo-url>
   cd <repo-folder>
   ```

2. **Configure deployment settings** in `scripts/deploy.sh`:
   ```bash
   ADMIN_LOGIN="your@email.com"  # Your Azure AD User Principal Name
   ADMIN_OBJECT_ID="your-object-id"  # Run: az ad signed-in-user show --query id -o tsv
   ```

3. **Deploy the application** (without GenAI):
   ```bash
   ./scripts/deploy.sh
   ```

   Or **deploy with AI Chat** (includes Azure OpenAI and AI Search):
   ```bash
   ./scripts/deploy-with-chat.sh
   ```

4. **Access the application** at `https://<app-name>.azurewebsites.net/Index`

### Local Development

1. Update `appsettings.json` with connection string:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=tcp:<server>.database.windows.net;Database=Northwind;Authentication=Active Directory Default;"
     }
   }
   ```

2. Run `az login` to authenticate

3. Run the application:
   ```bash
   cd app/ExpenseManagement
   dotnet run
   ```

## Architecture

See [docs/AZURE-ARCHITECTURE.md](docs/AZURE-ARCHITECTURE.md) for the full architecture diagram.

### Resources Deployed

| Resource | SKU | Purpose |
|----------|-----|---------|
| App Service | S1 Standard | Hosts the web application |
| Azure SQL | Basic | Expense data storage |
| Managed Identity | - | Passwordless authentication |
| Azure OpenAI* | S0 | AI chat functionality |
| Azure AI Search* | Basic | RAG support |

*Only deployed with `deploy-with-chat.sh`

## API Documentation

Access Swagger UI at `https://<app-url>/swagger` to explore and test the REST APIs.

## Project Structure

```
├── app/ExpenseManagement/     # ASP.NET 8 Razor Pages application
│   ├── Controllers/           # API controllers
│   ├── Models/               # Data models
│   ├── Pages/                # Razor pages
│   └── Services/             # Business logic
├── Database-Schema/           # SQL schema and stored procedures
├── docs/                      # Documentation
├── infrastructure/            # Bicep templates
└── scripts/                   # Deployment scripts
```

## Legacy App Modernization

This project demonstrates how to modernize legacy applications using:

1. **Screenshots of legacy UI** in `Legacy-Screenshots/`
2. **Database schema** in `Database-Schema/database_schema.sql`
3. **AI-powered code generation** using GitHub Copilot coding agent

To modernize your own app:
1. Fork this repo
2. Replace screenshots and SQL schema with your own
3. Update prompts in `prompts/` folder as needed
4. Run the coding agent with "modernise my app"

