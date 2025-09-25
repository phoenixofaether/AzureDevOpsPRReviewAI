# AzureDevOpsPRReviewAI

An AI-powered Pull Request review system for Azure DevOps that automatically analyzes code changes and provides intelligent feedback to developers using Anthropic Claude.

## Features

- 🔍 **Automated PR Analysis**: Detect and analyze code changes in Azure DevOps pull requests
- 🤖 **AI-Powered Reviews**: Generate contextual feedback using Claude AI
- 🔒 **Security Scanning**: Identify potential security vulnerabilities
- ✅ **Code Quality Checks**: Enforce coding standards and best practices
- 🔗 **Seamless Integration**: Webhook-based integration with Azure DevOps
- ⚙️ **Configurable**: Customizable rules and review criteria
- 🔐 **Secure**: OAuth and PAT authentication support
- 🎨 **Web UI**: Modern React TypeScript frontend for configuration management

## Prerequisites

### Backend
- .NET 8.0 SDK
- Visual Studio 2022 (recommended)
- Azure DevOps organization and project
- Anthropic Claude API key
- Redis server (for distributed caching)
- SEQ server (for logging, optional)

### Frontend (Optional - for configuration management)
- Node.js 18+
- npm or yarn

## Quick Start

### 1. Clone the Repository

```bash
git clone <repository-url>
cd AzureDevOpsPRReviewAI
```

### 2. Configure User Secrets (Development)

Set up your development secrets using the .NET user secrets manager:

```bash
# Azure DevOps Personal Access Token
dotnet user-secrets set "AzureDevOps:PAT" "your-pat-token"

# Anthropic Claude API Key
dotnet user-secrets set "Claude:ApiKey" "your-anthropic-api-key"

# Redis Connection String
dotnet user-secrets set "Redis:ConnectionString" "localhost:6379"

# Webhook Authentication
dotnet user-secrets set "Webhook:BasicAuth:Username" "webhook-user"
dotnet user-secrets set "Webhook:BasicAuth:Password" "your-secure-password"
```

### 3. Configure Azure DevOps

#### Create Personal Access Token (PAT)
1. Go to Azure DevOps → User Settings → Personal Access Tokens
2. Create new token with scopes:
   - Code (read & write)
   - Pull requests (read & write)
   - Project and team (read)
3. Copy the token and set it in user secrets (step 2)

#### Set up Webhook
1. Go to your Azure DevOps project → Project Settings → Service Hooks
2. Create new subscription:
   - Service: Web Hooks
   - Event: Pull request created/updated
   - URL: `https://your-server/webhook/pullrequest`
   - HTTP Headers: `Authorization: Basic <base64(username:password)>`

### 4. Get Anthropic Claude API Key

1. Sign up at [Anthropic Console](https://console.anthropic.com/)
2. Create API key in your account settings
3. Set the key in user secrets (step 2)

### 5. Install Dependencies

```bash
# Restore NuGet packages
dotnet restore

# Build the solution
dotnet build
```

### 6. Run the Application

```bash
# Development
dotnet run --project AzureDevOpsPRReviewAI.WebApi

# Or use Visual Studio
# Open AzureDevOpsPRReviewAI.sln and press F5
```

The API will be available at `https://localhost:7001` (HTTPS) or `http://localhost:5000` (HTTP).

### 7. Run the Frontend (Optional)

The frontend provides a web-based interface for managing configurations:

```bash
# Navigate to frontend directory
cd frontend

# Install dependencies
npm install

# Configure environment (update API URL if needed)
cp .env.example .env

# Start development server
npm run dev
```

The frontend will be available at `http://localhost:5173`.

## Configuration

### appsettings.json Structure

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341"
        }
      }
    ]
  },
  "AzureDevOps": {
    "BaseUrl": "https://dev.azure.com/{organization}"
  },
  "Claude": {
    "Model": "claude-3-haiku-20240307",
    "MaxTokens": 200000
  },
  "Repository": {
    "LocalStoragePath": "./repos",
    "CleanupIntervalHours": 24,
    "MaxDiskUsageGB": 10
  }
}
```

### Environment-Specific Configuration

- **Development**: Uses user secrets and `appsettings.Development.json`
- **Production**: Uses environment variables or secure key vault

## Usage

### Webhook Endpoint

The application exposes a webhook endpoint that Azure DevOps calls when PR events occur:

- **URL**: `POST /webhook/pullrequest`
- **Authentication**: HTTP Basic Auth
- **Content-Type**: `application/json`

### API Endpoints

- `GET /health` - Health check endpoint
- `POST /webhook/pullrequest` - Receive PR events from Azure DevOps
- `GET /api/reviews/{pullRequestId}` - Get review history (future)

#### Configuration Management API
- `GET /api/configuration/{org}/{project}/{repo}` - Get repository configuration
- `POST /api/configuration` - Save repository configuration
- `PUT /api/configuration/{org}/{project}/{repo}` - Update repository configuration
- `DELETE /api/configuration/{org}/{project}/{repo}` - Delete repository configuration
- `POST /api/configuration/clone` - Clone configuration between repositories
- `POST /api/configuration/validate` - Validate configuration
- `GET /api/configuration/{org}/{project}/{repo}/export` - Export configuration as JSON
- `POST /api/configuration/import` - Import configuration from JSON

### Frontend Configuration UI

The React TypeScript frontend provides a comprehensive interface for managing AI review configurations:

#### Features:
- 🏠 **Repository Browser**: Search and select repositories for configuration
- ⚙️ **Configuration Editor**: Tabbed interface for all settings:
  - Basic settings (enable/disable, metadata)
  - Webhook settings (auto-review triggers, user permissions)
  - Comment settings (formatting, line comments, summaries)
  - Review strategy (processing approach, token limits)
  - Query settings (search strategy, caching, exclusions)
- 📋 **Rule Management**: Visual editors for:
  - Review rules (code quality, security, performance, etc.)
  - File exclusion patterns (glob, regex, file types)
  - Custom AI prompts with template variables
- 💾 **Import/Export**: Backup and share configurations via JSON
- 🔄 **Configuration Cloning**: Copy settings between repositories
- ✅ **Real-time Validation**: Instant feedback with helpful error messages

#### Access:
After starting both backend and frontend servers, navigate to `http://localhost:5173` to access the configuration interface.

## Development Commands

### Backend (.NET)
```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run --project AzureDevOpsPRReviewAI.WebApi

# Publish for production
dotnet publish -c Release

# Manage user secrets
dotnet user-secrets list
dotnet user-secrets set "key" "value"
dotnet user-secrets remove "key"
```

### Frontend (React TypeScript)
```bash
# Navigate to frontend directory
cd frontend

# Install dependencies
npm install

# Start development server
npm run dev

# Type checking
npm run type-check

# Build for production
npm run build

# Preview production build
npm run preview

# Lint code
npm run lint
```

## Troubleshooting

### Common Issues

1. **Authentication Errors**
   - Verify PAT token has correct permissions
   - Check token expiration date
   - Ensure OAuth configuration is correct

2. **Webhook Not Receiving Events**
   - Verify webhook URL is accessible from Azure DevOps
   - Check authentication credentials
   - Review Azure DevOps service hook configuration

3. **Claude API Errors**
   - Verify API key is valid and has credits
   - Check rate limiting
   - Review request payload size

4. **Repository Access Issues**
   - Ensure sufficient disk space
   - Check file permissions
   - Verify Git credentials

### Logging

The application uses Serilog with SEQ for structured logging:

1. Install SEQ locally: [Download SEQ](https://datalust.co/seq)
2. Run SEQ on `http://localhost:5341`
3. View logs in SEQ dashboard

### Performance

- Repository data is cached locally and cleaned up automatically
- Redis is used for distributed caching across instances
- Large PRs are processed with context window management

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

[Add your license information here]

## Support

For issues and questions:
- Create an issue in the repository
- Check the troubleshooting section
- Review logs in SEQ dashboard
