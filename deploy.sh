#!/bin/bash

# ============================================
# Expense Management System - Deployment Script
# ============================================
# This script deploys:
# - App Service with Managed Identity
# - Azure SQL Database with Entra ID authentication
# - Database schema and stored procedures
# - Application code
#
# For GenAI (Chat UI) deployment, use deploy-with-chat.sh
# ============================================

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}============================================${NC}"
echo -e "${GREEN}Expense Management System - Deployment${NC}"
echo -e "${GREEN}============================================${NC}"

# Configuration - Update these values
RESOURCE_GROUP=${RESOURCE_GROUP:-"rg-expensemgmt-demo"}
LOCATION=${LOCATION:-"uksouth"}
ADMIN_OBJECT_ID=${ADMIN_OBJECT_ID:-""}
ADMIN_LOGIN=${ADMIN_LOGIN:-""}

# Check required parameters
if [ -z "$ADMIN_OBJECT_ID" ]; then
    echo -e "${YELLOW}Getting current user's Object ID...${NC}"
    ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv 2>/dev/null || echo "")
    if [ -z "$ADMIN_OBJECT_ID" ]; then
        echo -e "${RED}Error: Could not get admin Object ID. Please set ADMIN_OBJECT_ID environment variable.${NC}"
        exit 1
    fi
fi

if [ -z "$ADMIN_LOGIN" ]; then
    echo -e "${YELLOW}Getting current user's email...${NC}"
    ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv 2>/dev/null || echo "")
    if [ -z "$ADMIN_LOGIN" ]; then
        echo -e "${RED}Error: Could not get admin login. Please set ADMIN_LOGIN environment variable.${NC}"
        exit 1
    fi
fi

echo -e "${GREEN}Using:${NC}"
echo -e "  Resource Group: ${RESOURCE_GROUP}"
echo -e "  Location: ${LOCATION}"
echo -e "  Admin Login: ${ADMIN_LOGIN}"
echo -e "  Admin Object ID: ${ADMIN_OBJECT_ID}"

# Step 1: Create Resource Group
echo -e "\n${GREEN}Step 1: Creating Resource Group...${NC}"
az group create --name $RESOURCE_GROUP --location $LOCATION --output none

# Step 2: Deploy Infrastructure
echo -e "\n${GREEN}Step 2: Deploying Infrastructure (App Service, Managed Identity, SQL Database)...${NC}"
DEPLOYMENT_OUTPUT=$(az deployment group create \
    --resource-group $RESOURCE_GROUP \
    --template-file infrastructure/main.bicep \
    --parameters adminObjectId=$ADMIN_OBJECT_ID adminLogin=$ADMIN_LOGIN deployGenAI=false \
    --query "properties.outputs" \
    --output json)

# Extract outputs
APP_SERVICE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceName.value')
APP_SERVICE_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceUrl.value')
SQL_SERVER_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerName.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.databaseName.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')

echo -e "${GREEN}Deployment outputs:${NC}"
echo -e "  App Service: ${APP_SERVICE_NAME}"
echo -e "  App URL: ${APP_SERVICE_URL}"
echo -e "  SQL Server: ${SQL_SERVER_NAME}"
echo -e "  SQL FQDN: ${SQL_SERVER_FQDN}"
echo -e "  Database: ${DATABASE_NAME}"
echo -e "  Managed Identity: ${MANAGED_IDENTITY_NAME}"
echo -e "  Managed Identity Client ID: ${MANAGED_IDENTITY_CLIENT_ID}"

# Step 3: Configure App Service Connection String
echo -e "\n${GREEN}Step 3: Configuring App Service settings...${NC}"
CONNECTION_STRING="Server=tcp:${SQL_SERVER_FQDN},1433;Initial Catalog=${DATABASE_NAME};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=${MANAGED_IDENTITY_CLIENT_ID};"

az webapp config connection-string set \
    --resource-group $RESOURCE_GROUP \
    --name $APP_SERVICE_NAME \
    --connection-string-type SQLAzure \
    --settings DefaultConnection="$CONNECTION_STRING" \
    --output none

az webapp config appsettings set \
    --resource-group $RESOURCE_GROUP \
    --name $APP_SERVICE_NAME \
    --settings "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
    --output none

# Step 4: Wait for SQL Server to be ready
echo -e "\n${GREEN}Step 4: Waiting 30 seconds for SQL Server to be fully ready...${NC}"
sleep 30

# Step 5: Add local IP to firewall
echo -e "\n${GREEN}Step 5: Adding local IP to SQL Server firewall...${NC}"
LOCAL_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
    --resource-group $RESOURCE_GROUP \
    --server $SQL_SERVER_NAME \
    --name "LocalDeployment" \
    --start-ip-address $LOCAL_IP \
    --end-ip-address $LOCAL_IP \
    --output none 2>/dev/null || echo "Firewall rule may already exist"

# Step 6: Update Python scripts with actual values
echo -e "\n${GREEN}Step 6: Updating Python scripts with database connection info...${NC}"

# Update run-sql.py
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/${SQL_SERVER_FQDN}/g" run-sql.py && rm -f run-sql.py.bak

# Update run-sql-dbrole.py
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/${SQL_SERVER_FQDN}/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak

# Update run-sql-stored-procs.py
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/${SQL_SERVER_FQDN}/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak

# Update script.sql with managed identity name
sed -i.bak "s/MANAGED-IDENTITY-NAME/${MANAGED_IDENTITY_NAME}/g" script.sql && rm -f script.sql.bak

# Step 7: Install Python dependencies
echo -e "\n${GREEN}Step 7: Installing Python dependencies...${NC}"
pip3 install --quiet pyodbc azure-identity

# Step 8: Import database schema
echo -e "\n${GREEN}Step 8: Importing database schema...${NC}"
python3 run-sql.py

# Step 9: Configure database roles for managed identity
echo -e "\n${GREEN}Step 9: Configuring database roles for managed identity...${NC}"
python3 run-sql-dbrole.py

# Step 10: Create stored procedures
echo -e "\n${GREEN}Step 10: Creating stored procedures...${NC}"
# Update stored procedures script to use correct file
sed -i.bak 's/SQL_SCRIPT_FILE = "script.sql"/SQL_SCRIPT_FILE = "stored-procedures.sql"/g' run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
python3 run-sql-stored-procs.py

# Step 11: Build and publish application
echo -e "\n${GREEN}Step 11: Building application...${NC}"
cd src/ExpenseManagement
dotnet publish -c Release -o ./publish --nologo

# Step 12: Create deployment zip
echo -e "\n${GREEN}Step 12: Creating deployment zip...${NC}"
cd publish
zip -r ../../../app.zip . -x "*.pdb"
cd ../../..

# Step 13: Deploy application code
echo -e "\n${GREEN}Step 13: Deploying application code to Azure...${NC}"
az webapp deploy \
    --resource-group $RESOURCE_GROUP \
    --name $APP_SERVICE_NAME \
    --src-path ./app.zip \
    --type zip

# Cleanup temp files
rm -f app.zip

echo -e "\n${GREEN}============================================${NC}"
echo -e "${GREEN}Deployment Complete!${NC}"
echo -e "${GREEN}============================================${NC}"
echo -e "\nApplication URL: ${APP_SERVICE_URL}/Index"
echo -e "\nTo access the API documentation: ${APP_SERVICE_URL}/swagger"
echo -e "\n${YELLOW}Note: The app is available at /Index, not the root URL${NC}"
echo -e "\nFor GenAI Chat features, run: ./deploy-with-chat.sh"
