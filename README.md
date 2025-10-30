# Sms Message Maintenance

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Azure](https://img.shields.io/badge/Azure-Functions-blue.svg)
![Azure SQL](https://img.shields.io/badge/Azure%20SQL-Database-lightblue.svg)
![.NET](https://img.shields.io/badge/.NET-8-purple.svg)
![C#](https://img.shields.io/badge/C%23-Language-239120.svg)
![React](https://img.shields.io/badge/React-18-blue.svg)

A scalable, cloud-native SMS message maintenance platform built on Microsoft Azure.

## Overview

This solution processes SMS messages asynchronously through a third-party API, handling 10,000+ messages daily with a 10-minute delivery SLA. The system uses Azure PaaS services for automatic scaling, high availability, and minimal operational overhead.

## Features

- **Asynchronous Processing**: Event-driven architecture with Azure Storage Queues
- **Scalable Backend**: Azure Functions with consumption-based pricing
- **Responsive Frontend**: React single-page application with search/filter and pagination
- **Comprehensive Monitoring**: Application Insights for full observability
- **API Gateway**: Azure API Management for rate limiting and security
- **High Availability**: 99.5%+ uptime with managed Azure services

## Architecture

```
Web App (React) → API Management → Azure Functions → Azure SQL Server
                                         ↓
                                  Storage Queue
                                         ↓
                                  Azure Functions → Third-Party SMS API
```

## Technology Stack

### Backend
- **Runtime**: Azure Functions (C# .NET 8)
- **Database**: Azure SQL Database
- **Queue**: Azure Storage Queue
- **API Gateway**: Azure API Management
- **Monitoring**: Application Insights

### Frontend
- **Framework**: React 18 with TypeScript
- **Styling**: CSS3 with responsive design
- **HTTP Client**: Axios
- **Hosting**: Azure Static Web Apps

## Getting Started

### Prerequisites
- [Azure subscription] (https://azure.microsoft.com/en-us/free/)
- .NET 8 SDK
- Node.js 18+
- Azure CLI
- Install azure functions core tools
- Azure Data Studio

### Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/akhilkakar/sms-message-maintenance.git
   ```
   ```bash
   cd sms-message-maintenance
   ```

2. **Deploy Azure infrastructure**
   Follow the detailed steps in `DEPLOYMENT_GUIDE.md`.

3. **Deploy backend functions**
   ```bash
   cd backend/WebApi
   ```
   ```bash
   func azure functionapp publish <your-webapi-function-app-name>
   ```
   ```bash
   cd ../MessageReader
   ```
   ```bash
   func azure functionapp publish <your-messagereader-function-app-name>
   ```
   ```bash
   cd ../ApiProcessor
   ```
   ```bash
   func azure functionapp publish <your-apiprocessor-function-app-name>
   ```

4. **Deploy frontend**
   ```bash
   cd frontend
   ```
   ```bash
   npm install
   ```
   ```bash
   npm run build
   ```
   Deploy to Azure Static Web Apps.

## Project Structure

```
sms-message-maintenance/
├── docs/
│   └── SMS_Messaging_System_Design.md    # Comprehensive architecture document
├── backend/
│   ├── WebApi/                            # HTTP-triggered functions
│   │   ├── GetMessagesFunction.cs         # Web API - GET messages
│   │   ├── CreateMessageFunction.cs       # Web API - POST messages
│   │   ├── Program.cs                     # Function app startup
│   │   └── WebApi.csproj                  # Project dependencies
│   ├── MessageReader/                     # Timer-triggered function
│   │   ├── MessageReaderFunction.cs       # Polls DB and enqueues messages
│   │   ├── Program.cs                     # Function app startup
│   │   └── MessageReader.csproj           # Project dependencies
│   ├── ApiProcessor/                      # Queue-triggered function
│   │   ├── ApiProcessorFunction.cs        # Processes messages via API
│   │   ├── Program.cs                     # Function app startup
│   │   └── ApiProcessor.csproj            # Project dependencies
├── frontend/
│   ├── src/
│   │   ├── App.tsx                        # Main React component
│   │   └── App.css                        # Styles
│   ├── public/
│   │   └── index.html 
│   └── package.json
└── DEPLOYMENT_GUIDE.md                    # Step-by-step deployment instructions
```

## Key Components

### 1. Message Reader Function
- **Trigger**: Timer (every minute)
- **Purpose**: Polls database for new messages and enqueues them
- **SLA**: Processes messages within 1 minute of creation

### 2. API Processor Function
- **Trigger**: Storage Queue
- **Purpose**: Calls third-party SMS API and updates status. (Simulated)
- **Scaling**: Auto-scales based on queue depth

### 3. Web API Functions
- **Trigger**: HTTP
- **Endpoints**: 
  - `GET /api/messages` - Search and paginate messages
  - `POST /api/messages` - Create new messages

### 4. React Frontend
- **Features**: Search, filter, sort, pagination
- **Responsive**: Mobile and desktop optimised

## Configuration

### Environment Variables (Azure Functions)

```
SqlConnectionString=<your-sql-connection-string>
AzureWebJobsStorage=<your-storage-connection-string>
ThirdPartyApiUrl=<sms-api-endpoint>
APPINSIGHTS_INSTRUMENTATIONKEY=<your-instrumentation-key>
```

### Environment Variables (React)

```
AZURE_SUBSCRIPTION_ID=<your-subscription-id>>
AZURE_TENANT_ID=<your-tenant-id>
REACT_APP_API_URL=<your-apim-or-function-url>
REACT_APP_API_KEY=<your-function-key> for Web API function
```

## Monitoring

### Key Metrics
- Messages processed per hour
- API success/failure rates
- Queue depth and processing time
- Database DTU utilization

### Alerts
- Queue depth > 1,000 messages
- Error rate > 5%
- Processing time > 12 minutes

## Database Schema

```sql
Messages Table:
- ID (BIGINT, Primary Key)
- To (BIGINT)
- From (BIGINT)
- Message (NVARCHAR(1000))
- Status (NVARCHAR(100))
- StatusReason (NVARCHAR(500))
- RetryCount (INT)
- QueuedDateTime (DATETIME2)
- ProcessedDateTime (DATETIME2)
- CreatedDateTime (DATETIME2)
- ModifiedDateTime (DATETIME2)
```

## API Endpoints

### GET /api/messages
Search and retrieve messages with pagination.

**Query Parameters:**
- `search` - Search term (min 3 characters)
- `page` - Page number (default: 1)
- `pageSize` - Results per page (default: 20, max: 100)
- `sortBy` - Sort field (id, message, status, createdDateTime, modifiedDateTime)
- `sortOrder` - Sort direction (asc, desc)

**Response:**
```json
{
  "data": [...],
  "totalCount": 150,
  "page": 1,
  "pageSize": 20,
  "totalPages": 8
}
```

### POST /api/messages
Create a new message.

**Supports:**
- JSON body
- Form data
- Query string

**Request Body:**
```json
{
  "to": "1456789012",
  "from": "2445566778",
  "message": "You are simply awesome!"
}
```

## Security

- TLS 1.2 encryption for all communications
- IP whitelisting for database access for privacy
- Transparent data encryption for Azure SQL Database
- Rate limiting via API Management
- SQL injection protection via parameterised queries

## Testing

### Backend Tests
```bash
cd backend
dotnet test
```

### Frontend Tests
```bash
cd frontend
npm test
```

## Troubleshooting

### Function Not Executing
1. Check Application Insights logs
2. Verify connection strings in configuration
3. Ensure firewall rules allow Function App IP

### Database Connection Issues
1. Verify SQL Server firewall rules
2. Check connection string format
3. Test connectivity from Azure Portal

### CORS Errors
1. Update CORS settings in Function App
2. Verify API Management policies
3. Check browser console for specific errors

## Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Create a Pull Request

## License

This project is licensed under the MIT License.

## Support

For questions or issues:
- Check the [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)
- Review Application Insights logs
- Open an issue in the repository

## Acknowledgments

- Built with Microsoft Azure cloud services
- Follows Azure Well-Architected Framework principles

---

**Version**: 1.0  
**Last Updated**: October 2025
