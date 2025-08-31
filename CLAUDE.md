# Claude.md - Azure DevOps PR Review AI

## Project Overview
This project aims to create an AI-powered Pull Request review system for Azure DevOps that automatically analyzes code changes and provides intelligent feedback to developers.

## Project Goals
- Automatically review pull requests in Azure DevOps repositories
- Provide intelligent, contextual feedback on code quality, security, and best practices
- Integrate seamlessly with existing Azure DevOps workflows
- Reduce manual review time while maintaining code quality standards

## Key Features to Implement
- **PR Analysis**: Automatically detect and analyze code changes in pull requests
- **AI-Powered Reviews**: Generate meaningful comments using LLM technology
- **Security Scanning**: Identify potential security vulnerabilities
- **Code Quality Checks**: Enforce coding standards and best practices
- **Integration**: Webhook-based integration with Azure DevOps
- **Configuration**: Customizable rules and review criteria
- **Authentication**: Secure access to Azure DevOps repositories

## Finalized Technology Stack
- **Backend**: ASP.NET Core Web API (.NET 8)
- **IDE**: Visual Studio
- **Azure DevOps Integration**: Microsoft.VisualStudio.Services.WebApi + Personal Access Tokens
- **AI/LLM Integration**: Anthropic Claude API via Anthropic.SDK (by tghamm)
- **Repository Operations**: LibGit2Sharp for git diff and file retrieval
- **Hosting**: Local Windows Release Servers
- **Logging**: Serilog + SEQ (dev: localhost:5341, prod: https://applog.ost.ch:5341)
- **Caching**: In-memory + Redis distributed caching
- **Configuration**: appsettings.json + User Secrets (development)
- **Error Handling**: Polly retry policies with circuit breakers
- **Testing**: xUnit + Moq
- **Security**: Webhook signature validation

## Development Phases
1. **Research & Planning**: Understand APIs, choose technology stack
2. **MVP Implementation**: Basic PR detection and simple AI reviews
3. **Core Features**: Full review capabilities with customization
4. **Advanced Features**: Security scanning, integration improvements
5. **Production Deployment**: Monitoring, logging, error handling

## Authentication & Security Notes
- **Webhook Authentication**: HTTP Basic Authentication for incoming webhooks from Azure DevOps
- **Azure DevOps API Authentication**: Personal Access Tokens (PATs) for outbound API calls
- Store all credentials securely using User Secrets (development) and secure storage (production)
- Ensure code analysis data is handled securely
- Consider RBAC for multi-tenant scenarios

## Configuration Management
- Repository-specific review rules
- Configurable AI prompts and review criteria
- Integration settings per project/organization
- User preferences and notification settings

## Commands to Run
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run`
- Publish: `dotnet publish -c Release`
- User Secrets: `dotnet user-secrets set "key" "value"`

## API Endpoints (Future)
- POST /webhook/pullrequest - Receive PR events from Azure DevOps
- GET /config/{repo} - Get repository configuration
- POST /config/{repo} - Update repository configuration
- GET /reviews/{pullRequestId} - Get review history

## Configuration Structure

### User Secrets (Development)
```json
{
  "AzureDevOps:PAT": "your-pat-token",
  "Claude:ApiKey": "your-anthropic-api-key",
  "Redis:ConnectionString": "localhost:6379",
  "Webhook:BasicAuth:Username": "webhook-user",
  "Webhook:BasicAuth:Password": "your-webhook-password"
}
```

### appsettings.json
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
  },
  "Webhook": {
    "BasicAuth": {
      "Username": "webhook-user",
      "Password": "change-this-in-production"
    }
  }
}
```
- TODOs accross questions are inside the File @TODO.md make sure to follow them and after completing them mark them as completed