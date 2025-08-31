# TODO List - Azure DevOps PR Review AI

## Phase 1: Research & Discovery ✅
- [x] **Azure DevOps API Research**
  - [x] Study Azure DevOps REST API documentation
  - [x] Understand webhook events for pull requests  
  - [x] Research authentication methods (PAT vs OAuth)
  - [x] Identify required permissions and scopes
  - [x] Test API endpoints with sample requests

- [x] **AI/LLM Integration Research**
  - [x] ✅ **DECIDED:** Using Anthropic Claude API (Claude 3.7/4)
  - [x] Research Anthropic Claude API documentation and SDKs
  - [x] Study Claude's code analysis capabilities and best practices
  - [x] Investigate Claude's context window (200K tokens) for large PRs
  - [x] Research .NET HTTP client libraries for Anthropic API
  - [x] Evaluate Claude API pricing and rate limits

- [x] **Technology Stack Decision**
  - [x] ✅ **DECIDED:** C#/.NET with Visual Studio
  - [x] ✅ **DECIDED:** Local hosting on Windows Release Servers
  - [x] ✅ **DECIDED:** appsettings.json + SEQ logging + Redis + Local filesystem
  - [x] ✅ **DECIDED:** Microsoft Entra ID OAuth + PAT for development
  - [x] Research .NET 8.0 Web API project template and structure
  - [x] Research ASP.NET Core hosting on Windows Server

## Phase 2: Project Setup & Foundation ✅
- [x] **Project Structure (.NET)**
  - [x] Create ASP.NET Core Web API project
  - [x] Set up solution structure with multiple projects (API, Core, Infrastructure)
  - [x] Configure appsettings.json for development and production
  - [x] Set up Visual Studio development environment
  - [x] Initialize git repository and .gitignore for .NET

- [x] **Core Dependencies (.NET NuGet Packages)**
  - [x] Install Microsoft.AspNetCore.Authentication.JwtBearer
  - [x] Add Microsoft.TeamFoundationServer.Client (Azure DevOps REST API)
  - [x] Install Serilog.AspNetCore + Serilog.Sinks.Seq (SEQ logging)
  - [x] Add Microsoft.Extensions.Caching.StackExchangeRedis (Redis)
  - [x] Install Anthropic.SDK for Claude API calls
  - [x] Add LibGit2Sharp for repository operations
  - [x] Install Polly for retry policies and circuit breakers
  - [x] Add Microsoft.Identity.Web for OAuth authentication
  - [x] Set up User Secrets for sensitive configuration

## Phase 3: Azure DevOps Integration ✅
- [x] **Authentication Setup (.NET)**
  - [x] Implement Personal Access Token authentication for development
  - [x] Configure Microsoft Entra ID OAuth using Microsoft.Identity.Web
  - [x] Store credentials securely in appsettings.json (development) and user secrets
  - [x] Create authentication service with both PAT and OAuth support
  - [x] Test authentication with Azure DevOps REST API using .NET client

- [x] **Webhook Implementation**
  - [x] Create webhook endpoint to receive PR events
  - [x] Implement webhook signature validation
  - [x] Handle different PR event types (created, updated, etc.)
  - [x] Add error handling and logging for webhook events
  - [x] Test webhook with Azure DevOps configuration

- [x] **Pull Request Data Extraction**
  - [x] Fetch PR metadata (title, description, author)
  - [x] Retrieve changed files and diff information
  - [x] Extract code changes with context
  - [x] Handle binary files and large changes appropriately
  - [x] Cache PR data to avoid redundant API calls

## Phase 4: AI Analysis Engine (Placeholder Implementation) ✅
- [x] **Claude API Integration (.NET)**
  - [x] Create HttpClient service for Anthropic Claude API (placeholder)
  - [x] Implement Claude API request/response models
  - [x] Create prompts optimized for Claude's code analysis capabilities
  - [x] Add Polly for retry policies and circuit breakers
  - [x] Handle Claude API errors and implement graceful fallbacks

- [x] **Code Analysis Logic**
  - [x] Develop prompts for different code review aspects
    - [x] Code quality and best practices
    - [x] Security vulnerability detection
    - [x] Performance considerations
    - [x] Documentation and comments
    - [x] Testing coverage suggestions
  - [x] Implement analysis result parsing
  - [x] Create confidence scoring system
  - [x] Add filtering for low-confidence suggestions

## Phase 4.5: Repository Management & Context System ✅
**CRITICAL GAP:** Previously the AI analysis worked with placeholder data only. This phase implements the core repository cloning, diff generation, and RAG system that makes the AI analysis meaningful and contextually aware.

- [x] **Repository Operations**
  - [x] Implement LibGit2Sharp integration for repository cloning
  - [x] Create repository management service with cleanup strategy
  - [x] Implement git diff generation between PR branch and target branch
  - [x] Add disk space monitoring and repository cleanup policies
  - [x] Handle repository authentication and permissions
  - [x] Implement branch switching and file retrieval system

- [x] **File Access & Context Retrieval**
  - [x] Create file retrieval service for unchanged files
  - [x] Implement caching strategy for repository files
  - [x] Add support for binary file detection and handling
  - [x] Create file filtering system (exclude patterns, file size limits)
  - [x] Implement file content encoding detection

- [x] **RAG with Syntax-Aware Chunking**
  - [x] Implement Roslyn analyzers for C# syntax parsing
  - [x] Create syntax-aware code chunking preserving structure
  - [ ] Build vector database integration for code embeddings
  - [x] Implement semantic search for relevant code context (placeholder)
  - [x] Add cross-file relationship analysis
  - [x] Create context prioritization based on relevance

- [x] **Context Management**
  - [x] Implement token counting and context window management
  - [x] Create intelligent context selection algorithms
  - [x] Add support for importing related files and dependencies
  - [x] Implement context caching and reuse strategies
  - [x] Handle large PR context splitting and prioritization

## Phase 5: Review Comment Generation ✅
- [x] **Comment Formatting**
  - [x] Create structured comment templates
  - [x] Implement line-specific comment positioning
  - [x] Add severity levels (info, warning, error)
  - [x] Support markdown formatting in comments
  - [x] Include links to documentation/resources

- [x] **Comment Posting**
  - [x] Implement Azure DevOps PR comment API integration
  - [x] Handle comment threading and replies
  - [x] Add mechanism to update/delete previous comments
  - [x] Implement comment deduplication
  - [x] Test comment visibility and permissions

## Phase 6: Configuration & Customization
- [ ] **Repository Configuration**
  - [ ] Create configuration schema for review rules
  - [ ] Implement per-repository settings storage
  - [ ] Add file/folder exclusion patterns
  - [ ] Support custom prompts and review criteria
  - [ ] Create configuration validation

- [ ] **User Management**
  - [ ] Implement user/organization onboarding
  - [ ] Add configuration UI (web interface)
  - [ ] Support team-based configuration inheritance
  - [ ] Add role-based access control
  - [ ] Implement configuration backup/restore

## Phase 7: Advanced Features
- [ ] **Security Scanning**
  - [ ] Integrate security-focused analysis prompts
  - [ ] Add common vulnerability pattern detection
  - [ ] Implement secret scanning capabilities
  - [ ] Create security severity classification
  - [ ] Add compliance checking features

- [ ] **Performance & Scalability (.NET)**
  - [ ] Implement IMemoryCache for transient data and Redis for distributed caching
  - [ ] Add Hangfire for background job processing of large PRs
  - [ ] Use HttpClient with connection pooling for optimal API performance
  - [ ] Implement rate limiting using AspNetCoreRateLimit
  - [ ] Add support for multiple server instances with Redis as shared cache

## Phase 8: Testing & Quality Assurance
- [ ] **Unit Testing**
  - [ ] Write tests for webhook processing
  - [ ] Test Azure DevOps API integration
  - [ ] Mock LLM responses for consistent testing
  - [ ] Test configuration management logic
  - [ ] Add edge case and error handling tests

- [ ] **Integration Testing**
  - [ ] Create end-to-end test scenarios
  - [ ] Test with real Azure DevOps repositories
  - [ ] Validate comment posting and formatting
  - [ ] Test with various PR sizes and types
  - [ ] Performance testing with concurrent requests

## Phase 9: Deployment & Operations
- [ ] **Production Setup**
  - [ ] Configure production hosting environment
  - [ ] Set up CI/CD pipeline
  - [ ] Implement production logging and monitoring
  - [ ] Configure alerts and error tracking
  - [ ] Set up backup and disaster recovery

- [ ] **Documentation**
  - [ ] Write user setup and configuration guide
  - [ ] Create developer documentation
  - [ ] Document API endpoints and webhooks
  - [ ] Prepare troubleshooting guides
  - [ ] Create security and privacy documentation

## Phase 10: Launch & Iteration
- [ ] **Beta Testing**
  - [ ] Deploy to staging environment
  - [ ] Onboard initial test repositories
  - [ ] Collect user feedback and metrics
  - [ ] Iterate on review quality and accuracy
  - [ ] Fix bugs and performance issues

- [ ] **Production Launch**
  - [ ] Deploy to production
  - [ ] Monitor system performance and reliability
  - [ ] Collect usage analytics and metrics
  - [ ] Plan feature roadmap based on feedback
  - [ ] Set up customer support processes

## Future Enhancements
- [ ] Machine learning for custom review pattern detection
- [ ] Integration with other Azure DevOps features (work items, etc.)
- [ ] Support for other version control systems (GitHub, GitLab)
- [ ] Advanced metrics and analytics dashboard
- [ ] Plugin system for custom analyzers
- [ ] Multi-language specialized review capabilities