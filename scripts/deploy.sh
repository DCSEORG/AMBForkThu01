#!/bin/bash
# Deploy script for Expense Management System (without GenAI services)
# This script deploys App Service, SQL Database, and the application code
# For GenAI chat functionality, use deploy-with-chat.sh instead

set -e

echo "=========================================="
echo "Expense Management System - Deployment"
echo "=========================================="

# Configuration - Update these values before running
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"
ADMIN_LOGIN=""      # Your Azure AD User Principal Name (e.g., user@domain.com)
ADMIN_OBJECT_ID=""  # Your Azure AD Object ID (get with: az ad signed-in-user show --query id -o tsv)

# Validate required parameters
if [ -z "$ADMIN_LOGIN" ] || [ -z "$ADMIN_OBJECT_ID" ]; then
    echo "ERROR: Please set ADMIN_LOGIN and ADMIN_OBJECT_ID in this script"
    echo ""
    echo "To get your values, run:"
    echo "  az ad signed-in-user show --query userPrincipalName -o tsv"
    echo "  az ad signed-in-user show --query id -o tsv"
    exit 1
fi

echo ""
echo "Configuration:"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Location: $LOCATION"
echo "  Admin Login: $ADMIN_LOGIN"
echo ""

# Step 1: Create Resource Group
echo "Step 1: Creating resource group..."
az group create --name $RESOURCE_GROUP --location $LOCATION --output none

# Step 2: Deploy infrastructure (App Service, SQL, Managed Identity)
echo "Step 2: Deploying infrastructure..."
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group $RESOURCE_GROUP \
    --template-file infrastructure/main.bicep \
    --parameters adminLogin="$ADMIN_LOGIN" adminObjectId="$ADMIN_OBJECT_ID" deployGenAI=false \
    --query "properties.outputs" -o json)

# Extract values from deployment
APP_SERVICE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceName.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
SQL_DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlDatabaseName.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
APP_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceUrl.value')

echo "  App Service: $APP_SERVICE_NAME"
echo "  SQL Server: $SQL_SERVER_FQDN"
echo "  Database: $SQL_DATABASE_NAME"
echo "  Managed Identity: $MANAGED_IDENTITY_NAME"

# Step 3: Configure App Service settings
echo "Step 3: Configuring App Service settings..."
CONNECTION_STRING="Server=tcp:$SQL_SERVER_FQDN,1433;Database=$SQL_DATABASE_NAME;Authentication=Active Directory Managed Identity;User Id=$MANAGED_IDENTITY_CLIENT_ID;"

az webapp config connection-string set \
    --name $APP_SERVICE_NAME \
    --resource-group $RESOURCE_GROUP \
    --connection-string-type SQLAzure \
    --settings DefaultConnection="$CONNECTION_STRING" \
    --output none

az webapp config appsettings set \
    --name $APP_SERVICE_NAME \
    --resource-group $RESOURCE_GROUP \
    --settings "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
    --output none

# Step 4: Wait for SQL Server to be ready
echo "Step 4: Waiting 30 seconds for SQL Server to be ready..."
sleep 30

# Step 5: Add current IP to SQL firewall
echo "Step 5: Adding current IP to SQL firewall..."
MY_IP=$(curl -s https://api.ipify.org)
SQL_SERVER_NAME=$(echo $SQL_SERVER_FQDN | cut -d'.' -f1)
az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name "DeploymentIP" \
    --start-ip-address $MY_IP \
    --end-ip-address $MY_IP \
    --output none

# Step 6: Install Python dependencies and run SQL scripts
echo "Step 6: Setting up database..."
pip3 install --quiet pyodbc azure-identity

# Update Python scripts with actual server names
sed -i.bak "s/sql-expensemgmt-UNIQUESUFFIX.database.windows.net/$SQL_SERVER_FQDN/g" scripts/run-sql.py && rm -f scripts/run-sql.py.bak
sed -i.bak "s/sql-expensemgmt-UNIQUESUFFIX.database.windows.net/$SQL_SERVER_FQDN/g" scripts/run-sql-dbrole.py && rm -f scripts/run-sql-dbrole.py.bak
sed -i.bak "s/sql-expensemgmt-UNIQUESUFFIX.database.windows.net/$SQL_SERVER_FQDN/g" scripts/run-sql-stored-procs.py && rm -f scripts/run-sql-stored-procs.py.bak
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" scripts/script.sql && rm -f scripts/script.sql.bak

# Run database setup scripts
echo "  Importing database schema..."
python3 scripts/run-sql.py

echo "  Configuring managed identity access..."
python3 scripts/run-sql-dbrole.py

echo "  Creating stored procedures..."
python3 scripts/run-sql-stored-procs.py

# Step 7: Build and deploy application
echo "Step 7: Building and deploying application..."
cd app/ExpenseManagement
dotnet restore
dotnet publish -c Release -o ./publish

# Create zip file with files at root level (not in subdirectory)
cd publish
zip -r ../../../app.zip ./*
cd ../../..

# Deploy to Azure
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $APP_SERVICE_NAME \
    --src-path ./app.zip \
    --type zip \
    --output none

echo ""
echo "=========================================="
echo "Deployment Complete!"
echo "=========================================="
echo ""
echo "Application URL: $APP_URL/Index"
echo ""
echo "NOTE: Navigate to $APP_URL/Index to view the app"
echo "      The root URL redirects to /Index"
echo ""
echo "API Documentation: $APP_URL/swagger"
echo ""
echo "To enable AI Chat, run: ./scripts/deploy-with-chat.sh"
echo ""
