![Header image](https://github.com/DougChisholm/App-Mod-Booster/blob/main/repo-header-booster.png)

# Expense Management System

A modernized cloud-native Expense Management application built with ASP.NET 8, Azure SQL, and Azure OpenAI.

## Features

- **Expense Management**: Create, view, edit, and submit expenses
- **Approval Workflow**: Managers can approve or reject submitted expenses
- **Modern UI**: Clean, responsive design with status badges and filters
- **AI Chat Assistant**: Natural language interface to interact with expenses (requires GenAI deployment)
- **RESTful APIs**: Full API documentation with Swagger
- **Azure Best Practices**: Managed Identity authentication, Entra ID only SQL access

## Quick Start

### Prerequisites
- Azure CLI installed and logged in (`az login`)
- .NET 8 SDK
- Python 3.x with pip
- ODBC Driver 18 for SQL Server

### Basic Deployment (Without Chat AI)

```bash
# Set your Azure subscription
az account set --subscription "Your Subscription Name"

# Run the deployment script
./deploy.sh
```

### Full Deployment (With AI Chat)

```bash
# Deploys Azure OpenAI and AI Search in addition to basic resources
./deploy-with-chat.sh
```

### Access the Application

After deployment:
- **Application URL**: `https://<app-service-name>.azurewebsites.net/Index`
- **API Documentation**: `https://<app-service-name>.azurewebsites.net/swagger`

> **Note**: Navigate to `/Index`, not the root URL

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for a detailed diagram of the Azure services and their connections.

### Azure Resources Created

| Resource | Purpose | SKU |
|----------|---------|-----|
| App Service | Hosts the ASP.NET application | S1 Standard |
| User Assigned Managed Identity | Secure authentication | - |
| Azure SQL Database | Northwind database | Basic |
| Azure OpenAI | AI chat capabilities | S0 (Sweden Central) |
| AI Search | RAG pattern support | Basic |

## Local Development

1. Clone the repository
2. Update `appsettings.Development.json` with your SQL Server connection
3. Run `az login` for local authentication
4. Use connection string with `Authentication=Active Directory Default`

```bash
cd src/ExpenseManagement
dotnet run
```

## Project Structure

```
├── infrastructure/          # Bicep templates for Azure resources
│   ├── main.bicep          # Main deployment template
│   ├── app-service.bicep   # App Service configuration
│   ├── azure-sql.bicep     # Azure SQL Database
│   ├── managed-identity.bicep
│   └── genai.bicep         # Azure OpenAI & AI Search
├── src/ExpenseManagement/  # ASP.NET 8 Razor Pages application
│   ├── Models/             # Data models
│   ├── Services/           # ExpenseService, ChatService
│   ├── Pages/              # Razor Pages (Index, Expenses, AddExpense, Approve)
│   └── Program.cs          # API endpoints configuration
├── Database-Schema/        # SQL schema file
├── deploy.sh               # Basic deployment script
├── deploy-with-chat.sh     # Full deployment with GenAI
├── run-sql.py              # Database schema import script
├── run-sql-dbrole.py       # Managed identity role configuration
├── run-sql-stored-procs.py # Stored procedures deployment
└── stored-procedures.sql   # All stored procedures
```

## Security

- **No SQL Authentication**: Entra ID only (MCAPS compliant)
- **Managed Identity**: No secrets in code or configuration
- **Stored Procedures**: All database operations via parameterized procedures
- **HTTPS Only**: All traffic encrypted

## License

See [LICENSE](LICENSE) file.
