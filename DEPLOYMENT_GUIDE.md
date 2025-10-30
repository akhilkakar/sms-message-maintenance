# Azure Deployment Guide - Sms Message Maintenance

## Prerequisites

Before starting, ensure you have:
- Azure subscription with Owner or Contributor role
- Azure CLI installed (https://docs.microsoft.com/cli/azure/install-azure-cli)
- .NET 8 SDK installed
- Node.js 18+ and npm installed
- Visual Studio Code or Visual Studio 2022
- Git installed

## Part 1: Azure Infrastructure Setup

### Step 1: Login to Azure

```bash
# Login to your Azure account
az login

# Set your subscription (if you have multiple)
az account set --subscription "Your-Subscription-Name"

# Verify you're in the correct subscription
az account show
```

### Step 2: Create Resource Group

```bash
# Set variables
RESOURCE_GROUP="rg-sms-messaging"
LOCATION="australiasoutheast"  # Change to your preferred region

# Create resource group
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

### Step 3: Create Azure SQL Database

```bash
# Set SQL variables
SQL_SERVER_NAME="sqlserver-sms-messaging"  # Must be globally unique
SQL_DB_NAME="sqldb-messages"
SQL_ADMIN_USER="sqladmin"
SQL_ADMIN_PASSWORD="YourStrongPassword123!"  # Change this!

# Create SQL Server
az sql server create \
  --name $SQL_SERVER_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user $SQL_ADMIN_USER \
  --admin-password $SQL_ADMIN_PASSWORD

# Create SQL Database (Standard S2 tier)
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name $SQL_DB_NAME \
  --edition GeneralPurpose \
  --family Gen5 \
  --compute-model Serverless \
  --auto-pause-delay 60 \
  --max-size 32GB \
  --capacity 1 \
  --backup-storage-redundancy Local

# Allow Azure services to access the server
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# (Optional) Add your client IP to access from your machine
MY_IP=$(curl -s https://ifconfig.me)
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowMyIP \
  --start-ip-address $MY_IP \
  --end-ip-address $MY_IP
```

### Step 4: Create Database Schema

Connect to your database using Azure Data Studio, SQL Server Management Studio, or Azure Portal Query Editor:

```sql
-- Connect to your database and run this script

-- Create Messages table with enhanced schema
CREATE TABLE [dbo].[Messages] (
    [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [To] BIGINT NOT NULL,
    [From] BIGINT NOT NULL,
    [Message] NVARCHAR(1000) NOT NULL,
    [Status] NVARCHAR(100) NULL,
    [StatusReason] NVARCHAR(500) NULL,
    [RetryCount] INT NOT NULL DEFAULT 0,
    [QueuedDateTime] DATETIME2 NULL,
    [ProcessedDateTime] DATETIME2 NULL,
    [CreatedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ModifiedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Create indexes for performance
CREATE INDEX IX_Status_Created 
ON [dbo].[Messages] (Status, CreatedDateTime);

CREATE INDEX IX_ProcessedDateTime 
ON [dbo].[Messages] (ProcessedDateTime) 
WHERE ProcessedDateTime IS NOT NULL;

-- Insert sample data for testing
INSERT INTO [dbo].[Messages] 
    ([To], [From], [Message], [Status], [CreatedDateTime], [ModifiedDateTime])
VALUES 
    ('0412345678', '0498765432', 'Hello! This is a test message.', 'Successfully Sent', GETUTCDATE(), GETUTCDATE()),
    ('0412345679', '0498765432', 'Your appointment is confirmed for tomorrow.', 'Successfully Sent', GETUTCDATE(), GETUTCDATE()),
    ('0412345680', '0498765432', 'Your order #12345 has shipped!', 'Pending', GETUTCDATE(), GETUTCDATE()),
    ('0412345681', '0498765432', 'Invalid phone number test', 'Not Sent - Not a valid phone', GETUTCDATE(), GETUTCDATE()),
    ('0412345682', '0498765432', 'This is a test of timezone validation', 'Not Sent - Not valid by Time zone', GETUTCDATE(), GETUTCDATE()),
    ('0412345683', '0498765432', 'Another test message', 'Queued', GETUTCDATE(), GETUTCDATE()),
    ('0412345684', '0498765432', 'Password reset request received', 'Processing', GETUTCDATE(), GETUTCDATE()),
    ('0412345685', '0498765432', 'Your verification code is 123456', 'Successfully Sent', GETUTCDATE(), GETUTCDATE()),
    ('0412345686', '0498765432', 'Thank you for your purchase!', 'Successfully Sent', GETUTCDATE(), GETUTCDATE()),
    ('0412345687', '0498765432', 'Your subscription expires soon', 'Pending', GETUTCDATE(), GETUTCDATE());

-- Verify data
SELECT COUNT(*) as TotalMessages FROM [dbo].[Messages];
SELECT TOP 10 * FROM [dbo].[Messages] ORDER BY CreatedDateTime DESC;
```

### Step 5: Create Storage Account

```bash
# Create storage account (name must be globally unique, lowercase, no hyphens)
STORAGE_ACCOUNT_NAME="sasmsmessaging$RANDOM"
echo "Storage Account Name: $STORAGE_ACCOUNT_NAME"  # Save this!

az storage account create \
  --name $STORAGE_ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2

# Get storage connection string
STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
  --name $STORAGE_ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --query connectionString \
  --output tsv)

echo "Storage Connection String: $STORAGE_CONNECTION_STRING"  # Save this!

# Create queue
az storage queue create \
  --name message-processing \
  --account-name $STORAGE_ACCOUNT_NAME \
  --connection-string "$STORAGE_CONNECTION_STRING"
```

### Step 6: Create Application Insights

```bash
# Create Log Analytics Workspace first
LOG_WORKSPACE_NAME="log-sms-messaging"
az monitor log-analytics workspace create \
  --resource-group $RESOURCE_GROUP \
  --workspace-name $LOG_WORKSPACE_NAME \
  --location $LOCATION

# Get workspace ID
WORKSPACE_ID=$(az monitor log-analytics workspace show \
  --resource-group $RESOURCE_GROUP \
  --workspace-name $LOG_WORKSPACE_NAME \
  --query id \
  --output tsv)

# Create Application Insights
APP_INSIGHTS_NAME="appi-sms-messaging"
az monitor app-insights component create \
  --app $APP_INSIGHTS_NAME \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --workspace $WORKSPACE_ID

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app $APP_INSIGHTS_NAME \
  --resource-group $RESOURCE_GROUP \
  --query instrumentationKey \
  --output tsv)

echo "Instrumentation Key: $INSTRUMENTATION_KEY"  # Save this!
```

### Step 7: Create Azure Functions

```bash
# Create Function App for Web API
FUNCTION_APP_NAME_API="func-web-api-$RANDOM"
echo "Web API Function App Name: $FUNCTION_APP_NAME_API"  # Save this!

az functionapp create \
  --resource-group $RESOURCE_GROUP \
  --consumption-plan-location $LOCATION \
  --runtime dotnet \
  --runtime-version 8 \
  --functions-version 4 \
  --name $FUNCTION_APP_NAME_API \
  --storage-account $STORAGE_ACCOUNT_NAME \
  --app-insights $APP_INSIGHTS_NAME

# Create Function App for Message Reader
FUNCTION_APP_NAME_READER="func-message-reader-$RANDOM"
echo "Message Reader Function App Name: $FUNCTION_APP_NAME_READER"  # Save this!

az functionapp create \
  --resource-group $RESOURCE_GROUP \
  --consumption-plan-location $LOCATION \
  --runtime dotnet \
  --runtime-version 8 \
  --functions-version 4 \
  --name $FUNCTION_APP_NAME_READER \
  --storage-account $STORAGE_ACCOUNT_NAME \
  --app-insights $APP_INSIGHTS_NAME

# Create Function App for API Processor
FUNCTION_APP_NAME_PROCESSOR="func-api-processor-$RANDOM"
echo "API Processor Function App Name: $FUNCTION_APP_NAME_PROCESSOR"  # Save this!

az functionapp create \
  --resource-group $RESOURCE_GROUP \
  --consumption-plan-location $LOCATION \
  --runtime dotnet \
  --runtime-version 8 \
  --functions-version 4 \
  --name $FUNCTION_APP_NAME_PROCESSOR \
  --storage-account $STORAGE_ACCOUNT_NAME \
  --app-insights $APP_INSIGHTS_NAME
```

### Step 8: Configure Function App Settings

```bash
# Get SQL connection string
SQL_CONNECTION_STRING="Server=tcp:${SQL_SERVER_NAME}.database.windows.net,1433;Initial Catalog=${SQL_DB_NAME};Persist Security Info=False;User ID=${SQL_ADMIN_USER};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# Configure Web API Function
az functionapp config appsettings set \
  --name $FUNCTION_APP_NAME_API \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "SqlConnectionString=$SQL_CONNECTION_STRING" \
    "APPINSIGHTS_INSTRUMENTATIONKEY=$INSTRUMENTATION_KEY"

# Configure Message Reader Function
az functionapp config appsettings set \
  --name $FUNCTION_APP_NAME_READER \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "SqlConnectionString=$SQL_CONNECTION_STRING" \
    "APPINSIGHTS_INSTRUMENTATIONKEY=$INSTRUMENTATION_KEY"

# Configure API Processor Function
az functionapp config appsettings set \
  --name $FUNCTION_APP_NAME_PROCESSOR \
  --resource-group $RESOURCE_GROUP \
  --settings \
    "SqlConnectionString=$SQL_CONNECTION_STRING" \
    "ThirdPartyApiUrl=https://api.sms-provider.com/send" \
    "APPINSIGHTS_INSTRUMENTATIONKEY=$INSTRUMENTATION_KEY"

# Enable CORS for Web API Function (for local testing)
az functionapp cors add \
  --name $FUNCTION_APP_NAME_API \
  --resource-group $RESOURCE_GROUP \
  --allowed-origins "*"
```

### Step 9: Create API Management (Optional but Recommended)

```bash
# Note: APIM takes 30-45 minutes to provision
APIM_NAME="apim-sms-messaging"
APIM_PUBLISHER_EMAIL="akhil.recreation+smsmessaging@gmail.com"  # Change this!
APIM_PUBLISHER_NAME="Akhil Kakar"  # Change this!

# Create API Management (Consumption tier for cost savings)
az apim create \
  --resource-group $RESOURCE_GROUP \
  --name $APIM_NAME \
  --publisher-email $APIM_PUBLISHER_EMAIL \
  --publisher-name "$APIM_PUBLISHER_NAME" \
  --sku-name Consumption \
  --location $LOCATION

echo "API Management is being created. This takes 30-45 minutes..."
echo "You can continue with other steps and come back to configure APIM later."
```

## Part 2: Deploy Backend Code

### Step 1: Prepare Your Function Projects

Create three separate Function projects or use the provided code files.

**Project Structure:**
```
backend/
├── WebApi/
│   ├── GetMessagesFunction.cs
│   ├── CreateMessageFunction.cs
│   └── WebApi.csproj
├── MessageReader/
│   ├── MessageReaderFunction.cs
│   └── MessageReader.csproj
└── ApiProcessor/
    ├── ApiProcessorFunction.cs
    └── ApiProcessor.csproj
```

### Step 2: Create .csproj Files

**WebApi.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Sdk.Functions" Version="4.4.0" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.1.5" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

**MessageReader.csproj and ApiProcessor.csproj:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Sdk.Functions" Version="4.4.0" />
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.1.5" />
    <PackageReference Include="Azure.Storage.Queues" Version="12.18.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

### Step 3: Deploy Functions

```bash
# Navigate to each project and deploy

# Deploy Web API
cd backend/WebApi
func azure functionapp publish $FUNCTION_APP_NAME_API

# Deploy Message Reader
cd ../MessageReader
func azure functionapp publish $FUNCTION_APP_NAME_READER

# Deploy API Processor
cd ../ApiProcessor
func azure functionapp publish $FUNCTION_APP_NAME_PROCESSOR

cd ../..
```

### Step 4: Test Your Functions

```bash
# Get Web API Function URL
WEB_API_URL=$(az functionapp function show \
  --name $FUNCTION_APP_NAME_API \
  --resource-group $RESOURCE_GROUP \
  --function-name GetMessages \
  --query invokeUrlTemplate \
  --output tsv)

# Get Function Key
 

# Test GET endpoint
curl "${WEB_API_URL}?code=${FUNCTION_KEY}&page=1&pageSize=10"

# Test POST endpoint (create message)
curl -X POST "${WEB_API_URL}?code=${FUNCTION_KEY}" \
  -H "Content-Type: application/json" \
  -d '{"to":"0411111111","from":"0422222222","message":"Test message from API"}'
```

## Part 3: Deploy Frontend

### Step 1: Create React App (if starting fresh)

```bash

# Install dependencies
npm install
npm install axios

# Copy the provided App.tsx and App.css files to src/
```

### Step 2: Configure Environment Variables

Create `.env` file in your React project root:

```env
REACT_APP_API_URL=https://your-function-app.azurewebsites.net/api
REACT_APP_FUNCTION_KEY=<subscription-key-from-APIM>
```

### Step 3: Create Azure Static Web App

```bash
# Install Static Web Apps CLI
npm install -g @azure/static-web-apps-cli

# Create Static Web App
STATIC_WEB_APP_NAME="swa-sms-messaging"


# Deploy manually
npm run build

# Login to Azure Static Web Apps
swa login

swa deploy ./build \
  --app-name $STATIC_WEB_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --env development
```

### Step 4: Configure CORS for Production

```bash
# Get Static Web App URL
SWA_URL=$(az staticwebapp show \
  --name $STATIC_WEB_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query defaultHostname \
  --output tsv)

# Update CORS on Function App
az functionapp cors remove \
  --name $FUNCTION_APP_NAME_API \
  --resource-group $RESOURCE_GROUP \
  --allowed-origins "*"

az functionapp cors add \
  --name $FUNCTION_APP_NAME_API \
  --resource-group $RESOURCE_GROUP \
  --allowed-origins "https://${SWA_URL}"
```

## Part 4: Configure API Management (If Created)

### Step 1: Import Function API to APIM

```bash
# Get Function App URL
FUNCTION_APP_URL="https://${FUNCTION_APP_NAME_API}.azurewebsites.net"

# Import API (do this via Azure Portal for easier configuration)
echo "Navigate to Azure Portal > API Management > APIs > Add API"
echo "Select 'Function App' and import: $FUNCTION_APP_URL"
```

### Step 2: Configure APIM Policies

In Azure Portal, add these policies to your API:

**Inbound Policy:**
```xml
<policies>
    <inbound>
        <base />
        <rate-limit-by-key calls="1000" renewal-period="3600" counter-key="@(context.Request.IpAddress)" />
        <cors allow-credentials="true">
            <allowed-origins>
                <origin>https://YOUR-STATIC-WEB-APP-URL</origin>
            </allowed-origins>
            <allowed-methods>
                <method>GET</method>
                <method>POST</method>
            </allowed-methods>
            <allowed-headers>
                <header>*</header>
            </allowed-headers>
        </cors>
        <set-header name="X-Forwarded-For" exists-action="override">
            <value>@(context.Request.IpAddress)</value>
        </set-header>
    </inbound>
    <backend>
        <base />
    </backend>
    <outbound>
        <base />
    </outbound>
    <on-error>
        <base />
    </on-error>
</policies>
```

## Part 5: Monitoring Setup

### Step 1: Create Dashboard

```bash

cd backend
# Create a basic monitoring dashboard
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file ./dashboard-template.json
```

### Step 2: Configure Alerts

```bash
# Create action group for notifications
az monitor action-group create \
  --resource-group $RESOURCE_GROUP \
  --name "SMS-Alerts" \
  --short-name "SMSAlerts" \
  --email-receiver name=AlertEmail address=your-email@example.com

# Create alert for high queue depth
# (Do this via Azure Portal for easier configuration)
```

## Part 6: Verification and Testing

### Step 1: Verify All Components

```bash
# Check all resources
az resource list \
  --resource-group $RESOURCE_GROUP \
  --output table

# Verify Function Apps are running
az functionapp list \
  --resource-group $RESOURCE_GROUP \
  --query "[].{Name:name, State:state}" \
  --output table

# Check database connectivity
az sql db show \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name $SQL_DB_NAME \
  --query "{Name:name, Status:status}" \
  --output table
```

### Step 2: End-to-End Test

1. **Create a test message** via POST API
2. **Wait 1-2 minutes** for MessageReader to process
3. **Check queue** has message
4. **Wait 3-5 seconds** for ApiProcessor to complete
5. **Query database** to verify status updated
6. **View in frontend** to see the message

### Step 3: Monitor Application Insights

Go to Azure Portal > Application Insights > Live Metrics to see real-time data.

## Troubleshooting

### Functions Not Executing

```bash
# Check function logs
az functionapp log tail \
  --name $FUNCTION_APP_NAME_API \
  --resource-group $RESOURCE_GROUP

# Restart function app
az functionapp restart \
  --name $FUNCTION_APP_NAME_API \
  --resource-group $RESOURCE_GROUP
```

### Database Connection Issues

```bash
# Verify firewall rules
az sql server firewall-rule list \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --output table

# Test connection from Function App
# Enable connection logging in Function App configuration
```

### CORS Issues

```bash
# Update CORS settings
az functionapp cors add \
  --name $FUNCTION_APP_NAME_API \
  --resource-group $RESOURCE_GROUP \
  --allowed-origins "https://your-frontend-url.azurestaticapps.net"
```

## Cost Management

### Monitor Costs

```bash
# Get cost analysis
az consumption usage list \
  --start-date 2025-10-01 \
  --end-date 2025-10-31 \
  --query "[?contains(instanceName, 'sms-messaging')]"
```

### Set Budget Alert

```bash
# Create budget
az consumption budget create \
  --resource-group $RESOURCE_GROUP \
  --budget-name "SMS-Messaging-Budget" \
  --amount 150 \
  --time-grain Monthly \
  --start-date 2025-10-01 \
  --end-date 2026-10-01
```

## Cleanup (When Testing Complete)

```bash
# Delete entire resource group
az group delete \
  --name $RESOURCE_GROUP \
  --yes \
  --no-wait
```

## Next Steps

1. **Configure CI/CD** with GitHub Actions or Azure DevOps
2. **Add authentication** to APIs using Azure AD
3. **Enable private endpoints** for enhanced security
4. **Set up staging environment** for testing
5. **Configure backup and disaster recovery**

## Support

For issues, check:
- Application Insights logs
- Function App logs in Azure Portal
- SQL Database query performance
- Storage Queue metrics

---
