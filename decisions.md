# Engineering Decisions - Azure DevOps PR Review AI

## Decision 1: Programming Language & Runtime

**Question:** Which programming language should we use for the application backend?

C#/.NET with Visual Studio as it is my preferred development environment.

---

## Decision 2: LLM Service Choice

**Question:** Which Large Language Model service should we use for code analysis?

Anthropic's API with Claude 3.7 or 4 is preferred due to its strong performance on code tasks and with MCP and cost-effectiveness. Docs: https://docs.anthropic.com/en/api/overview

---

## Decision 3: Hosting Platform

**Question:** What platform should we use to host the application?

I'll host the application locally on one of our Release Servers (Windows). It will be released over Azure DevOps, but I'll build the pipelines later myself, so don't mind the releasing workflow.

---

## Decision 4: Authentication Strategy

**Question:** How should we authenticate with Azure DevOps APIs?

Primarily using Microsoft Entra ID OAuth but with the possibility to also use Personal Access Tokens (PAT) especially at the start for development.

---

## Decision 5: Database/Storage Solution

**Question:** What should we use for storing configuration, analysis history, and caching?

For the configuration use appsettings.json as it is standard for .Net applications. Use the SEQ logging system for structured logging and analysis history, locally to http://127.0.0.1:5341 and on the server to https://applog.ost.ch:5341. Caching can be done using in-memory caching for transient data and Redis for distributed caching. As we will be pulling the whole branch to make sure the AI can retrieve context fast you will be saving all files in the local file system (temporary of course).

---

## Decision 6: Context Management Strategy

**Question:** How should we handle large pull requests that exceed LLM context limits?

Implemented all options with a focus on RAG (Retrieval-Augmented Generation) techniques. As I said earlier, all files of the branch will already be locally available for context retrieval. The other options should be available.

**Ranked Options:**

### 1. **RAG with Syntax-Aware Chunking** ⭐⭐⭐⭐⭐
- **Pros:**
  - Preserves code structure and context
  - Scalable to very large PRs
  - Uses concrete syntax tree (CST) parsing
  - Maintains import statements and class definitions
- **Cons:**
  - Complex implementation
  - Requires language-specific parsers
  - Additional vector database needed
- **Context:** Essential for production use with large codebases

### 2. **File-by-File Analysis with Prioritization** ⭐⭐⭐⭐
- **Pros:**
  - Simpler to implement
  - Natural boundaries (files)
  - Can prioritize by change size/importance
- **Cons:**
  - May miss cross-file relationships
  - Less context for analysis quality
- **Context:** Good middle-ground approach for medium-complexity PRs

### 3. **Simple Truncation** ⭐⭐
- **Pros:**
  - Very simple implementation
  - Predictable behavior
- **Cons:**
  - Poor analysis quality for large PRs
  - May cut off important context
- **Context:** Only suitable for MVP or small-scale use

---

## Decision 7: .NET Claude SDK Choice

**Question:** Which unofficial C# SDK should we use for Anthropic Claude API integration?

Based on research, there are 3 main options. Please choose one:

### 1. **Anthropic.SDK (by tghamm)** ⭐⭐⭐⭐⭐
- **Pros:**
  - Most mature and feature-complete (v4.1.0)
  - Supports .NET Standard 2.0, .NET 6.0, and .NET 8.0
  - Environment variable support for API keys
  - Custom HttpClient support for retries/timeouts
  - Citations and streaming support
- **Cons:**
  - Third-party maintenance dependency
- **Context:** Most production-ready option with comprehensive features

**DECISION NEEDED:** Which SDK should we use?

We are using Anthropic.SDK (by tghamm) as it is the most mature and feature-complete option available, providing comprehensive support for our needs. Especially it's support for MCP (Multi-Context Prompting) as we will be providing access to the code base.

---

## Decision 8: Repository File Management

**Question:** How should we handle cloning and managing repository files locally?

Since we decided to pull entire branches locally, we need to decide the approach:

**Options to consider:**
1. **Git clone with shallow copy** - Clone only the PR branch with minimal history
2. **Git archive approach** - Download specific commits as archives
3. **Azure DevOps REST API file downloads** - Download individual files via API
4. **LibGit2Sharp integration** - Use .NET Git library for repository operations

**Context needed:**
- How long should we keep local files?
- Should we cache repositories between PR reviews?
- What's the cleanup strategy for disk space?

**DECISION NEEDED:** Repository file management approach?

Decision: Use LibGit2Sharp integration for repository operations. The main operations will be git diff between the PR branch and the target branch. That will be the initial input for the AI. Then there has to be a mechanism for the AI to retrieve whole files as context. Also files that havn't been changed at all. 
Repositories should be kept locally as mostly multiple PR will be made for the same repository in a short amount of time. But there has to be a cleanup strategy for old/unused repositories. Also the disk space should be observed to make sure we don't run out of space.
---

## Decision 9: Webhook Security and Validation

**Question:** How should we implement webhook security and validation?

**Options:**
1. **Signature validation only** - Validate Azure DevOps webhook signatures
2. **IP allowlisting + signatures** - Also restrict to Azure DevOps IP ranges  
3. **Custom authentication headers** - Additional secret headers
4. **Certificate-based authentication** - Use certificates for enhanced security

**Context:** Azure DevOps webhooks support signature validation, but additional security may be needed for production.

**DECISION NEEDED:** Webhook security approach?
1. Signature validation only - will be enough for now as the webhook URL will be hard to guess and only Azure DevOps will know it. In the future IP allowlisting can be added if needed.
---

## Decision 10: Error Handling and Retry Strategy  

**Question:** How should we handle failures and implement retry policies?

**Areas needing retry policies:**
1. **Claude API failures** - Rate limits, timeouts, service errors
2. **Azure DevOps API failures** - Authentication expiry, rate limits
3. **Repository cloning failures** - Network issues, access problems
4. **Comment posting failures** - Permissions, API rate limits

**Approaches:**
1. **Polly with exponential backoff** - Industry standard for .NET
2. **Simple retry loops** - Basic implementation
3. **Circuit breaker pattern** - Prevent cascading failures
4. **Dead letter queues** - Queue failed operations for later processing

**DECISION NEEDED:** Retry and error handling strategy?
1. Polly with exponential backoff and circuit breaker pattern for critical operations like Claude API calls and Azure DevOps API interactions. Simple retry loops can be used for less critical operations like repository cloning.
---

## Decision 11: Configuration Management Detail

**Question:** How should we structure the appsettings.json configuration?

**Configuration areas:**
- Azure DevOps connection settings
- Claude API settings  
- Repository-specific review rules
- Caching settings
- Logging levels
- Webhook validation settings

**Approaches:**
1. **Single appsettings.json** - All configuration in one file
2. **Environment-specific files** - appsettings.Development.json, appsettings.Production.json
3. **User Secrets for development** - Sensitive data in user secrets
4. **External configuration providers** - Azure App Configuration, etc.

**DECISION NEEDED:** Configuration structure approach?
2 & 3 Use environment-specific files (appsettings.Development.json, appsettings.Production.json) for different settings in development and production environments and User Secrets for sensitive data during development.
---

## Decision 12: Testing Strategy

**Question:** What testing approach should we implement?

**Testing types needed:**
1. **Unit tests** - Business logic, services, utilities
2. **Integration tests** - API integrations, database operations  
3. **End-to-end tests** - Full workflow from webhook to comment posting
4. **Mock strategy** - How to mock external services (Claude API, Azure DevOps)

**Frameworks available:**
- xUnit (already decided)
- Moq for mocking
- TestContainers for integration testing
- WebApplicationFactory for API testing

**DECISION NEEDED:** Testing strategy and mock approach?
Use xUnit for unit tests, Moq for mocking.
---
