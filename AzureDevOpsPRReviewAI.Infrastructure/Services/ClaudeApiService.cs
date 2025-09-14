namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using System.Diagnostics;
    using System.Text;
    using System.Text.Json;
    using Anthropic.SDK;
    using Anthropic.SDK.Messaging;
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class ClaudeApiService : IClaudeApiService
    {
        private readonly AnthropicClient anthropicClient;
        private readonly ILogger<ClaudeApiService> logger;
        private readonly IRepositoryService repositoryService;
        private readonly ICodeContextService codeContextService;
        private readonly IAuthenticationService authenticationService;
        private readonly string defaultModel;
        private readonly int maxTokens;

        public ClaudeApiService(
            IConfiguration configuration, 
            ILogger<ClaudeApiService> logger,
            IRepositoryService repositoryService,
            ICodeContextService codeContextService,
            IAuthenticationService authenticationService)
        {
            this.logger = logger;
            this.repositoryService = repositoryService;
            this.codeContextService = codeContextService;
            this.authenticationService = authenticationService;

            var apiKey = configuration["Claude:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("Claude API key is not configured. Please set Claude:ApiKey in configuration.");
            }

            this.anthropicClient = new AnthropicClient(apiKey);
            this.defaultModel = configuration["Claude:Model"] ?? "claude-3-haiku-20240307";
            this.maxTokens = int.TryParse(configuration["Claude:MaxTokens"], out var tokens) ? tokens : 4000;

            this.logger.LogInformation("Claude API service initialized with model: {Model}, max tokens: {MaxTokens}", this.defaultModel, this.maxTokens);
        }

        public async Task<CodeAnalysisResult> AnalyzeCodeAsync(CodeAnalysisRequest request, CancellationToken cancellationToken = default)
        {
            // Use default configuration for backward compatibility
            var defaultConfig = new RepositoryConfiguration
            {
                Id = "default",
                Organization = request.Organization,
                Project = request.Project,
                Repository = request.Repository,
                ReviewStrategySettings = new ReviewStrategySettings()
            };
            
            return await this.AnalyzeCodeAsync(request, defaultConfig, cancellationToken);
        }

        public async Task<CodeAnalysisResult> AnalyzeCodeAsync(CodeAnalysisRequest request, RepositoryConfiguration repositoryConfig, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();

            this.logger.LogInformation(
                "Starting code analysis for PR {PullRequestId} in {Organization}/{Project}/{Repository} using strategy: {Strategy}",
                request.PullRequestId,
                request.Organization,
                request.Project,
                request.Repository,
                repositoryConfig.ReviewStrategySettings.Strategy);

            try
            {
                // Step 1: Clone the repository and get diff context
                var accessToken = await this.authenticationService.GetAccessTokenAsync();
                if (string.IsNullOrEmpty(accessToken))
                {
                    return this.CreateErrorResult(requestId, request.PullRequestId, "No access token available", stopwatch.Elapsed);
                }

                var cloneResult = await this.repositoryService.CloneRepositoryAsync(
                    request.Organization, 
                    request.Project, 
                    request.Repository, 
                    accessToken);

                if (!cloneResult.IsSuccessful)
                {
                    return this.CreateErrorResult(requestId, request.PullRequestId, 
                        $"Failed to clone repository: {cloneResult.ErrorMessage}", stopwatch.Elapsed);
                }

                // Step 2: Build comprehensive analysis context
                var analysisRequest = new PullRequestAnalysisRequest
                {
                    RepositoryPath = cloneResult.LocalPath,
                    SourceBranch = request.SourceBranch,
                    TargetBranch = request.TargetBranch,
                    ChangedFiles = request.ChangedFiles,
                    MaxContextTokens = repositoryConfig.ReviewStrategySettings.MaxTokensPerRequest,
                    Command = request.Command
                };

                var contextResult = await this.codeContextService.BuildAnalysisContextAsync(cloneResult.LocalPath, analysisRequest);

                this.logger.LogInformation(
                    "Built analysis context: {ChunkCount} chunks, {FileCount} files, {TokenCount} tokens",
                    contextResult.RelevantChunks.Count,
                    contextResult.RelevantFiles.Count,
                    contextResult.TotalTokens);

                // Step 3: Execute analysis based on configured strategy
                var result = await this.ExecuteAnalysisStrategyAsync(
                    requestId, 
                    request, 
                    contextResult, 
                    repositoryConfig.ReviewStrategySettings, 
                    cancellationToken);

                stopwatch.Stop();
                result.Metadata.ProcessingTime = stopwatch.Elapsed;

                this.logger.LogInformation(
                    "Code analysis completed for PR {PullRequestId}. Generated {CommentCount} comments in {ElapsedTime}ms using strategy: {Strategy}",
                    request.PullRequestId,
                    result.Comments.Count,
                    stopwatch.ElapsedMilliseconds,
                    repositoryConfig.ReviewStrategySettings.Strategy);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                this.logger.LogError(ex, "Failed to analyze code for PR {PullRequestId}: {ErrorMessage}", request.PullRequestId, ex.Message);
                return this.CreateErrorResult(requestId, request.PullRequestId, ex.Message, stopwatch.Elapsed);
            }
        }

        private async Task<CodeAnalysisResult> ExecuteAnalysisStrategyAsync(
            string requestId,
            CodeAnalysisRequest request, 
            ContextResult contextResult, 
            ReviewStrategySettings strategySettings, 
            CancellationToken cancellationToken)
        {
            return strategySettings.Strategy switch
            {
                ReviewStrategy.SingleRequest => await this.ExecuteSingleRequestStrategyAsync(requestId, request, contextResult, strategySettings, cancellationToken),
                ReviewStrategy.MultipleRequestsPerFile => await this.ExecuteMultipleRequestsPerFileStrategyAsync(requestId, request, contextResult, strategySettings, cancellationToken),
                ReviewStrategy.MultipleRequestsByTokenSize => await this.ExecuteMultipleRequestsByTokenSizeStrategyAsync(requestId, request, contextResult, strategySettings, cancellationToken),
                ReviewStrategy.HybridStrategy => await this.ExecuteHybridStrategyAsync(requestId, request, contextResult, strategySettings, cancellationToken),
                _ => await this.ExecuteSingleRequestStrategyAsync(requestId, request, contextResult, strategySettings, cancellationToken)
            };
        }

        private async Task<CodeAnalysisResult> ExecuteSingleRequestStrategyAsync(
            string requestId,
            CodeAnalysisRequest request, 
            ContextResult contextResult, 
            ReviewStrategySettings strategySettings, 
            CancellationToken cancellationToken)
        {
            this.logger.LogDebug("Executing single request strategy for PR {PullRequestId}", request.PullRequestId);

            // Build single comprehensive prompt
            var prompt = await this.BuildAnalysisPromptWithContextAsync(request, contextResult);
            
            // Truncate if necessary to fit within token limits
            if (prompt.Length > strategySettings.MaxTokensPerRequest * 4) // Rough estimate: 1 token ≈ 4 characters
            {
                prompt = prompt.Substring(0, strategySettings.MaxTokensPerRequest * 4);
                prompt += "\n\n**Note: Prompt was truncated due to token limits.**";
                this.logger.LogWarning("Single request prompt was truncated for PR {PullRequestId}", request.PullRequestId);
            }

            var response = await this.CallClaudeApiAsync(prompt, cancellationToken);
            
            if (string.IsNullOrEmpty(response?.Content))
            {
                return this.CreateErrorResult(requestId, request.PullRequestId, "Claude API returned empty response", TimeSpan.Zero);
            }

            var result = this.ParseClaudeResponse(requestId, request.PullRequestId, response.Content, TimeSpan.Zero);
            
            // Add metadata
            result.Metadata.TokensUsed = response.InputTokens + response.OutputTokens;
            result.Metadata.ModelUsed = this.defaultModel;
            result.Metadata.FilesAnalyzed = contextResult.RelevantFiles.Count;
            result.Metadata.ContextTokens = contextResult.TotalTokens;
            result.Metadata.IsContextTruncated = contextResult.IsContextTruncated;
            result.Metadata.RequestsProcessed = 1;

            return result;
        }

        private async Task<CodeAnalysisResult> ExecuteMultipleRequestsPerFileStrategyAsync(
            string requestId,
            CodeAnalysisRequest request, 
            ContextResult contextResult, 
            ReviewStrategySettings strategySettings, 
            CancellationToken cancellationToken)
        {
            this.logger.LogDebug("Executing multiple requests per file strategy for PR {PullRequestId}", request.PullRequestId);

            var combinedResult = new CodeAnalysisResult
            {
                RequestId = requestId,
                PullRequestId = request.PullRequestId,
                IsSuccessful = true,
                Metadata = { ModelUsed = this.defaultModel }
            };

            var changedFiles = contextResult.PrimaryDiff?.ChangedFiles?.Take(strategySettings.MaxFilesPerRequest).ToList() ?? new List<FileDiff>();
            var tasks = new List<Task<CodeAnalysisResult>>();

            foreach (var file in changedFiles)
            {
                if (!file.IsBinary)
                {
                    var task = this.ProcessSingleFileAsync(requestId, request, file, contextResult, strategySettings, cancellationToken);
                    tasks.Add(task);

                    // Limit concurrent requests
                    if (tasks.Count >= strategySettings.MaxConcurrentRequests)
                    {
                        var completedTask = await Task.WhenAny(tasks);
                        tasks.Remove(completedTask);

                        var result = await completedTask;
                        this.MergeAnalysisResults(combinedResult, result);
                    }
                }
            }

            // Process remaining tasks
            var remainingResults = await Task.WhenAll(tasks);
            foreach (var result in remainingResults)
            {
                this.MergeAnalysisResults(combinedResult, result);
            }

            // Add summary comment if requested
            if (strategySettings.IncludeSummaryWhenSplit && combinedResult.Comments.Count > 0)
            {
                var summary = this.GenerateSummaryComment(combinedResult.Comments);
                combinedResult.Comments.Insert(0, summary);
            }

            this.logger.LogInformation(
                "Completed multiple requests per file strategy for PR {PullRequestId}: {FileCount} files processed, {CommentCount} comments generated",
                request.PullRequestId,
                changedFiles.Count,
                combinedResult.Comments.Count);

            return combinedResult;
        }

        private async Task<CodeAnalysisResult> ExecuteMultipleRequestsByTokenSizeStrategyAsync(
            string requestId,
            CodeAnalysisRequest request, 
            ContextResult contextResult, 
            ReviewStrategySettings strategySettings, 
            CancellationToken cancellationToken)
        {
            this.logger.LogDebug("Executing multiple requests by token size strategy for PR {PullRequestId}", request.PullRequestId);

            var combinedResult = new CodeAnalysisResult
            {
                RequestId = requestId,
                PullRequestId = request.PullRequestId,
                IsSuccessful = true,
                Metadata = { ModelUsed = this.defaultModel }
            };

            // Split context into manageable chunks by token size
            var chunks = this.SplitContextByTokenSize(contextResult, strategySettings.MaxTokensPerRequest);
            var tasks = new List<Task<CodeAnalysisResult>>();

            foreach (var chunk in chunks)
            {
                var task = this.ProcessContextChunkAsync(requestId, request, chunk, strategySettings, cancellationToken);
                tasks.Add(task);

                // Limit concurrent requests
                if (tasks.Count >= strategySettings.MaxConcurrentRequests)
                {
                    var completedTask = await Task.WhenAny(tasks);
                    tasks.Remove(completedTask);

                    var result = await completedTask;
                    this.MergeAnalysisResults(combinedResult, result);
                }
            }

            // Process remaining tasks
            var remainingResults = await Task.WhenAll(tasks);
            foreach (var result in remainingResults)
            {
                this.MergeAnalysisResults(combinedResult, result);
            }

            // Add summary comment if requested
            if (strategySettings.IncludeSummaryWhenSplit && combinedResult.Comments.Count > 0)
            {
                var summary = this.GenerateSummaryComment(combinedResult.Comments);
                combinedResult.Comments.Insert(0, summary);
            }

            return combinedResult;
        }

        private async Task<CodeAnalysisResult> ExecuteHybridStrategyAsync(
            string requestId,
            CodeAnalysisRequest request, 
            ContextResult contextResult, 
            ReviewStrategySettings strategySettings, 
            CancellationToken cancellationToken)
        {
            this.logger.LogDebug("Executing hybrid strategy for PR {PullRequestId}", request.PullRequestId);

            // Decide strategy based on context size and file count
            var estimatedTokens = contextResult.TotalTokens;
            var fileCount = contextResult.PrimaryDiff?.ChangedFiles?.Count ?? 0;

            if (estimatedTokens <= strategySettings.MaxTokensPerRequest && fileCount <= 5)
            {
                this.logger.LogDebug("Hybrid strategy choosing single request for PR {PullRequestId}", request.PullRequestId);
                return await this.ExecuteSingleRequestStrategyAsync(requestId, request, contextResult, strategySettings, cancellationToken);
            }
            else if (fileCount <= strategySettings.MaxFilesPerRequest)
            {
                this.logger.LogDebug("Hybrid strategy choosing per-file requests for PR {PullRequestId}", request.PullRequestId);
                return await this.ExecuteMultipleRequestsPerFileStrategyAsync(requestId, request, contextResult, strategySettings, cancellationToken);
            }
            else
            {
                this.logger.LogDebug("Hybrid strategy choosing token-based splitting for PR {PullRequestId}", request.PullRequestId);
                return await this.ExecuteMultipleRequestsByTokenSizeStrategyAsync(requestId, request, contextResult, strategySettings, cancellationToken);
            }
        }

        private async Task<CodeAnalysisResult> ProcessSingleFileAsync(
            string requestId,
            CodeAnalysisRequest request,
            FileDiff file,
            ContextResult contextResult,
            ReviewStrategySettings strategySettings,
            CancellationToken cancellationToken)
        {
            var fileSpecificContext = this.CreateFileSpecificContext(file, contextResult);
            var prompt = await this.BuildFileAnalysisPromptAsync(request, file, fileSpecificContext, strategySettings.MaxTokensPerFile);
            
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(strategySettings.RequestTimeout);
            
            var response = await this.CallClaudeApiAsync(prompt, timeoutCts.Token);
            
            if (string.IsNullOrEmpty(response?.Content))
            {
                return this.CreateErrorResult(requestId, request.PullRequestId, $"Claude API returned empty response for file {file.FilePath}", TimeSpan.Zero);
            }

            var result = this.ParseClaudeResponse(requestId, request.PullRequestId, response.Content, TimeSpan.Zero);
            result.Metadata.TokensUsed = response.InputTokens + response.OutputTokens;
            result.Metadata.ModelUsed = this.defaultModel;
            result.Metadata.FilesAnalyzed = 1;
            
            return result;
        }

        private async Task<CodeAnalysisResult> ProcessContextChunkAsync(
            string requestId,
            CodeAnalysisRequest request,
            ContextResult chunk,
            ReviewStrategySettings strategySettings,
            CancellationToken cancellationToken)
        {
            var prompt = await this.BuildAnalysisPromptWithContextAsync(request, chunk);
            
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(strategySettings.RequestTimeout);
            
            var response = await this.CallClaudeApiAsync(prompt, timeoutCts.Token);
            
            if (string.IsNullOrEmpty(response?.Content))
            {
                return this.CreateErrorResult(requestId, request.PullRequestId, "Claude API returned empty response for context chunk", TimeSpan.Zero);
            }

            var result = this.ParseClaudeResponse(requestId, request.PullRequestId, response.Content, TimeSpan.Zero);
            result.Metadata.TokensUsed = response.InputTokens + response.OutputTokens;
            result.Metadata.ModelUsed = this.defaultModel;
            
            return result;
        }

        private void MergeAnalysisResults(CodeAnalysisResult target, CodeAnalysisResult source)
        {
            if (source.IsSuccessful)
            {
                target.Comments.AddRange(source.Comments);
                target.Metadata.TokensUsed += source.Metadata.TokensUsed;
                target.Metadata.FilesAnalyzed += source.Metadata.FilesAnalyzed;
                target.Metadata.RequestsProcessed += 1;
            }
            else
            {
                this.logger.LogWarning("Skipping failed analysis result: {ErrorMessage}", source.ErrorMessage);
            }
        }

        private ReviewComment GenerateSummaryComment(List<ReviewComment> allComments)
        {
            var categories = allComments.GroupBy(c => c.Category).ToDictionary(g => g.Key, g => g.Count());
            var severities = allComments.GroupBy(c => c.Severity).ToDictionary(g => g.Key, g => g.Count());

            var summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine("## 🤖 AI Code Review Summary");
            summaryBuilder.AppendLine();
            summaryBuilder.AppendLine($"**Total Issues Found:** {allComments.Count}");
            summaryBuilder.AppendLine();
            
            if (severities.Count > 0)
            {
                summaryBuilder.AppendLine("**By Severity:**");
                foreach (var severity in severities.OrderByDescending(s => (int)s.Key))
                {
                    var icon = severity.Key switch
                    {
                        ReviewSeverity.Critical => "🔴",
                        ReviewSeverity.Error => "🟠", 
                        ReviewSeverity.Warning => "🟡",
                        ReviewSeverity.Info => "🔵",
                        _ => "⚪"
                    };
                    summaryBuilder.AppendLine($"- {icon} {severity.Key}: {severity.Value}");
                }
                summaryBuilder.AppendLine();
            }

            if (categories.Count > 0)
            {
                summaryBuilder.AppendLine("**By Category:**");
                foreach (var category in categories.OrderByDescending(c => c.Value))
                {
                    summaryBuilder.AppendLine($"- {category.Key}: {category.Value}");
                }
                summaryBuilder.AppendLine();
            }

            summaryBuilder.AppendLine("Please review the individual comments below for detailed feedback on each issue.");

            return new ReviewComment
            {
                Content = summaryBuilder.ToString(),
                Severity = ReviewSeverity.Info,
                Category = ReviewCategory.General
            };
        }

        private ContextResult CreateFileSpecificContext(FileDiff file, ContextResult originalContext)
        {
            return new ContextResult
            {
                PrimaryDiff = new GitDiffResult
                {
                    ChangedFiles = new List<FileDiff> { file },
                    IsSuccessful = true
                },
                RelevantChunks = originalContext.RelevantChunks
                    .Where(c => c.FilePath == file.FilePath)
                    .ToList(),
                RelevantFiles = originalContext.RelevantFiles
                    .Where(f => f.FilePath == file.FilePath)
                    .ToList(),
                TotalTokens = originalContext.RelevantChunks
                    .Where(c => c.FilePath == file.FilePath)
                    .Sum(c => c.TokenCount)
            };
        }

        private List<ContextResult> SplitContextByTokenSize(ContextResult context, int maxTokensPerChunk)
        {
            var chunks = new List<ContextResult>();
            var currentChunk = new ContextResult
            {
                PrimaryDiff = context.PrimaryDiff,
                RelevantChunks = new List<CodeChunk>(),
                RelevantFiles = new List<FileContent>(),
                TotalTokens = 0
            };

            foreach (var chunk in context.RelevantChunks)
            {
                if (currentChunk.TotalTokens + chunk.TokenCount > maxTokensPerChunk && currentChunk.RelevantChunks.Count > 0)
                {
                    chunks.Add(currentChunk);
                    currentChunk = new ContextResult
                    {
                        PrimaryDiff = context.PrimaryDiff,
                        RelevantChunks = new List<CodeChunk>(),
                        RelevantFiles = new List<FileContent>(),
                        TotalTokens = 0
                    };
                }

                currentChunk.RelevantChunks.Add(chunk);
                currentChunk.TotalTokens += chunk.TokenCount;
            }

            if (currentChunk.RelevantChunks.Count > 0)
            {
                chunks.Add(currentChunk);
            }

            return chunks;
        }

        private async Task<string> BuildFileAnalysisPromptAsync(
            CodeAnalysisRequest request, 
            FileDiff file, 
            ContextResult fileContext, 
            int maxTokensPerFile)
        {
            var prompt = new StringBuilder();
            
            prompt.AppendLine($"You are reviewing changes to the file: {file.FilePath}");
            prompt.AppendLine();
            prompt.AppendLine("Focus on:");
            prompt.AppendLine("- Code quality and best practices specific to this file");
            prompt.AppendLine("- Potential bugs in the changes");
            prompt.AppendLine("- Security implications");
            prompt.AppendLine("- Performance considerations");
            prompt.AppendLine();

            if (file.Hunks?.Count > 0)
            {
                prompt.AppendLine("## Changes in this file:");
                foreach (var hunk in file.Hunks)
                {
                    prompt.AppendLine($"```diff");
                    prompt.AppendLine($"@@ -{hunk.OldStart},{hunk.OldLines} +{hunk.NewStart},{hunk.NewLines} @@");
                    
                    foreach (var line in hunk.Lines)
                    {
                        var prefix = line.Type switch
                        {
                            DiffLineType.Addition => "+",
                            DiffLineType.Deletion => "-",
                            _ => " "
                        };
                        prompt.AppendLine($"{prefix}{line.Content}");
                    }
                    prompt.AppendLine("```");
                    prompt.AppendLine();
                }
            }

            // Add relevant context if available
            if (fileContext.RelevantChunks?.Count > 0)
            {
                prompt.AppendLine("## Related context from this file:");
                foreach (var chunk in fileContext.RelevantChunks.Take(3))
                {
                    prompt.AppendLine($"```csharp");
                    prompt.AppendLine(chunk.Content);
                    prompt.AppendLine("```");
                }
                prompt.AppendLine();
            }

            prompt.AppendLine("Please provide specific feedback in JSON format:");
            prompt.AppendLine("```json");
            prompt.AppendLine("{");
            prompt.AppendLine("  \"comments\": [");
            prompt.AppendLine("    {");
            prompt.AppendLine($"      \"filePath\": \"{file.FilePath}\",");
            prompt.AppendLine("      \"lineNumber\": 42,");
            prompt.AppendLine("      \"content\": \"Detailed feedback here\",");
            prompt.AppendLine("      \"severity\": \"warning\",");
            prompt.AppendLine("      \"category\": \"code-quality\"");
            prompt.AppendLine("    }");
            prompt.AppendLine("  ]");
            prompt.AppendLine("}");
            prompt.AppendLine("```");

            var result = prompt.ToString();
            
            // Truncate if too long
            if (result.Length > maxTokensPerFile * 4)
            {
                result = result.Substring(0, maxTokensPerFile * 4);
                result += "\n\n**Note: Prompt was truncated due to token limits.**";
            }

            return result;
        }

        private string BuildAnalysisPrompt(CodeAnalysisRequest request)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("You are an expert code reviewer analyzing a pull request. Please provide a thorough code review focusing on:");
            prompt.AppendLine("- Code quality and best practices");
            prompt.AppendLine("- Potential bugs and logical issues");
            prompt.AppendLine("- Security vulnerabilities");
            prompt.AppendLine("- Performance considerations");
            prompt.AppendLine("- Maintainability and readability");
            prompt.AppendLine();

            // Add command-specific focus if provided
            if (request.Command?.Parameters?.Count > 0)
            {
                prompt.AppendLine("Special focus areas requested:");
                foreach (var param in request.Command.Parameters)
                {
                    prompt.AppendLine($"- {param.Key}: {param.Value}");
                }

                prompt.AppendLine();
            }

            prompt.AppendLine("## Pull Request Information");
            prompt.AppendLine($"**Title:** {request.Title ?? "N/A"}");
            prompt.AppendLine($"**Description:** {request.Description ?? "N/A"}");
            prompt.AppendLine($"**Repository:** {request.Organization}/{request.Project}/{request.Repository}");
            prompt.AppendLine($"**Branch:** {request.SourceBranch} → {request.TargetBranch}");
            prompt.AppendLine();

            if (request.ChangedFiles.Count > 0)
            {
                prompt.AppendLine("## Changed Files");
                foreach (var file in request.ChangedFiles)
                {
                    prompt.AppendLine($"- {file}");
                }

                prompt.AppendLine();
            }

            if (!string.IsNullOrEmpty(request.DiffContent))
            {
                prompt.AppendLine("## Code Changes");
                prompt.AppendLine("```diff");
                prompt.AppendLine(request.DiffContent);
                prompt.AppendLine("```");
                prompt.AppendLine();
            }

            prompt.AppendLine("## Response Format");
            prompt.AppendLine("Please provide your review as structured comments in the following JSON format:");
            prompt.AppendLine("```json");
            prompt.AppendLine("{");
            prompt.AppendLine("  \"comments\": [");
            prompt.AppendLine("    {");
            prompt.AppendLine("      \"content\": \"Specific review comment here\",");
            prompt.AppendLine("      \"filePath\": \"path/to/file.cs\",");
            prompt.AppendLine("      \"lineNumber\": 42,");
            prompt.AppendLine("      \"severity\": \"Warning\",");
            prompt.AppendLine("      \"category\": \"CodeQuality\"");
            prompt.AppendLine("    }");
            prompt.AppendLine("  ]");
            prompt.AppendLine("}");
            prompt.AppendLine("```");
            prompt.AppendLine();
            prompt.AppendLine("Available severity levels: Info, Warning, Error, Critical");
            prompt.AppendLine("Available categories: General, CodeQuality, Security, Performance, Documentation, Testing, BestPractices");

            return prompt.ToString();
        }

        private CodeAnalysisResult ParseClaudeResponse(string requestId, string pullRequestId, string responseText, TimeSpan processingTime)
        {
            var result = new CodeAnalysisResult
            {
                RequestId = requestId,
                PullRequestId = pullRequestId,
                IsSuccessful = true,
                Metadata = { ProcessingTime = processingTime },
            };

            try
            {
                // Try to extract JSON from the response
                var jsonStart = responseText.IndexOf("{", StringComparison.Ordinal);
                var jsonEnd = responseText.LastIndexOf("}", StringComparison.Ordinal);

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonText = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var parseOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    };

                    var claudeResponse = JsonSerializer.Deserialize<ClaudeReviewResponse>(jsonText, parseOptions);

                    if (claudeResponse?.Comments != null)
                    {
                        foreach (var comment in claudeResponse.Comments)
                        {
                            var reviewComment = new ReviewComment
                            {
                                Content = comment.Content ?? "No content provided",
                                FilePath = comment.FilePath,
                                LineNumber = comment.LineNumber,
                            };

                            // Parse severity
                            if (Enum.TryParse<ReviewSeverity>(comment.Severity, true, out var severity))
                            {
                                reviewComment.Severity = severity;
                            }

                            // Parse category
                            if (Enum.TryParse<ReviewCategory>(comment.Category, true, out var category))
                            {
                                reviewComment.Category = category;
                            }

                            result.Comments.Add(reviewComment);
                        }

                        this.logger.LogDebug("Successfully parsed {CommentCount} structured comments from Claude response", result.Comments.Count);
                    }
                }
                else
                {
                    // Fallback: create a general comment with the full response
                    result.Comments.Add(new ReviewComment
                    {
                        Content = responseText.Trim(),
                        Severity = ReviewSeverity.Info,
                        Category = ReviewCategory.General,
                    });

                    this.logger.LogDebug("Could not parse structured JSON from Claude response, using full text as general comment");
                }
            }
            catch (JsonException ex)
            {
                this.logger.LogWarning(ex, "Failed to parse JSON from Claude response, using full text as fallback");

                // Fallback: create a general comment with the response text
                result.Comments.Add(new ReviewComment
                {
                    Content = responseText.Trim(),
                    Severity = ReviewSeverity.Info,
                    Category = ReviewCategory.General,
                });
            }

            return result;
        }

        private CodeAnalysisResult CreateErrorResult(string requestId, string pullRequestId, string errorMessage, TimeSpan processingTime)
        {
            return new CodeAnalysisResult
            {
                RequestId = requestId,
                PullRequestId = pullRequestId,
                IsSuccessful = false,
                ErrorMessage = errorMessage,
                Metadata = { ProcessingTime = processingTime },
            };
        }

        private async Task<string> BuildAnalysisPromptWithContextAsync(CodeAnalysisRequest request, ContextResult context)
        {
            var prompt = new StringBuilder();

            prompt.AppendLine("You are an expert code reviewer analyzing a pull request. Please provide a thorough code review focusing on:");
            prompt.AppendLine("- Code quality and best practices");
            prompt.AppendLine("- Potential bugs and logical issues");
            prompt.AppendLine("- Security vulnerabilities");
            prompt.AppendLine("- Performance considerations");
            prompt.AppendLine("- Maintainability and readability");
            prompt.AppendLine();

            // Add command-specific focus if provided
            if (request.Command?.Parameters?.Count > 0)
            {
                prompt.AppendLine("Special focus areas requested:");
                foreach (var param in request.Command.Parameters)
                {
                    prompt.AppendLine($"- {param.Key}: {param.Value}");
                }
                prompt.AppendLine();
            }

            prompt.AppendLine("## Pull Request Information");
            prompt.AppendLine($"**Title:** {request.Title ?? "N/A"}");
            prompt.AppendLine($"**Description:** {request.Description ?? "N/A"}");
            prompt.AppendLine($"**Repository:** {request.Organization}/{request.Project}/{request.Repository}");
            prompt.AppendLine($"**Branch:** {request.SourceBranch} → {request.TargetBranch}");
            prompt.AppendLine();

            // Add diff information
            if (context.PrimaryDiff?.ChangedFiles?.Count > 0)
            {
                prompt.AppendLine("## Changed Files Summary");
                foreach (var file in context.PrimaryDiff.ChangedFiles.Take(10))
                {
                    prompt.AppendLine($"- {file.FilePath} ({file.ChangeType}) +{file.LinesAdded}/-{file.LinesRemoved}");
                }
                prompt.AppendLine();

                // Add detailed diff for first few files
                prompt.AppendLine("## Detailed Changes");
                foreach (var file in context.PrimaryDiff.ChangedFiles.Take(3))
                {
                    prompt.AppendLine($"### File: {file.FilePath}");
                    
                    if (file.IsBinary)
                    {
                        prompt.AppendLine("*Binary file - changes not shown*");
                    }
                    else
                    {
                        foreach (var hunk in file.Hunks.Take(5)) // Limit hunks per file
                        {
                            prompt.AppendLine($"```diff");
                            prompt.AppendLine($"@@ -{hunk.OldStart},{hunk.OldLines} +{hunk.NewStart},{hunk.NewLines} @@");
                            
                            foreach (var line in hunk.Lines.Take(20)) // Limit lines per hunk
                            {
                                var prefix = line.Type switch
                                {
                                    DiffLineType.Addition => "+",
                                    DiffLineType.Deletion => "-",
                                    _ => " "
                                };
                                prompt.AppendLine($"{prefix}{line.Content}");
                            }
                            
                            prompt.AppendLine("```");
                        }
                    }
                    prompt.AppendLine();
                }
            }

            // Add relevant context from repository
            if (context.RelevantChunks?.Count > 0)
            {
                prompt.AppendLine("## Related Code Context");
                
                var groupedChunks = context.RelevantChunks
                    .GroupBy(c => c.FilePath)
                    .Take(5)
                    .ToList();

                foreach (var fileGroup in groupedChunks)
                {
                    prompt.AppendLine($"### Context from {fileGroup.Key}");
                    
                    foreach (var chunk in fileGroup.Take(3))
                    {
                        if (!string.IsNullOrEmpty(chunk.ClassName) || !string.IsNullOrEmpty(chunk.MethodName))
                        {
                            var identifier = !string.IsNullOrEmpty(chunk.ClassName) 
                                ? $"Class: {chunk.ClassName}" 
                                : $"Method: {chunk.MethodName}";
                            prompt.AppendLine($"**{identifier}** (Lines {chunk.StartLine}-{chunk.EndLine})");
                        }
                        
                        prompt.AppendLine("```csharp");
                        var lines = chunk.Content.Split('\n').Take(30); // Limit lines per chunk
                        prompt.AppendLine(string.Join("\n", lines));
                        prompt.AppendLine("```");
                        prompt.AppendLine();
                    }
                }
            }

            // Add context metadata
            if (context.IsContextTruncated)
            {
                prompt.AppendLine($"**Note:** Context was truncated due to size limits. {context.TruncationReason}");
                prompt.AppendLine();
            }

            prompt.AppendLine("## Instructions");
            prompt.AppendLine("Please analyze the changes and provide specific, actionable feedback.");
            prompt.AppendLine("For each issue identified, provide:");
            prompt.AppendLine("1. The specific file and line number (if applicable)");
            prompt.AppendLine("2. A clear description of the issue");
            prompt.AppendLine("3. The severity level (error, warning, or info)");
            prompt.AppendLine("4. A suggested fix or improvement");
            prompt.AppendLine();
            prompt.AppendLine("Format your response as JSON with the following structure:");
            prompt.AppendLine("```json");
            prompt.AppendLine("{");
            prompt.AppendLine("  \"comments\": [");
            prompt.AppendLine("    {");
            prompt.AppendLine("      \"filePath\": \"path/to/file.cs\",");
            prompt.AppendLine("      \"lineNumber\": 42,");
            prompt.AppendLine("      \"content\": \"Description of the issue and suggested fix\",");
            prompt.AppendLine("      \"severity\": \"warning\",");
            prompt.AppendLine("      \"category\": \"code-quality\"");
            prompt.AppendLine("    }");
            prompt.AppendLine("  ]");
            prompt.AppendLine("}");
            prompt.AppendLine("```");

            return prompt.ToString();
        }

        private async Task<ClaudeApiResponse> CallClaudeApiAsync(string prompt, CancellationToken cancellationToken)
        {
            try
            {
                this.logger.LogDebug("Calling Claude API with prompt length: {PromptLength}", prompt.Length);

                var messages = new List<Message>
                {
                    new Message
                    {
                        Role = RoleType.User,
                        Content = new List<ContentBase> { new TextContent { Text = prompt } }
                    }
                };

                var parameters = new MessageParameters
                {
                    Messages = messages,
                    Model = this.defaultModel,
                    MaxTokens = this.maxTokens,
                    Stream = false
                };

                var response = await this.anthropicClient.Messages.GetClaudeMessageAsync(parameters, cancellationToken);

                if (response?.Content?.FirstOrDefault() is TextContent textContent)
                {
                    return new ClaudeApiResponse
                    {
                        Content = textContent.Text,
                        InputTokens = response.Usage?.InputTokens ?? 0,
                        OutputTokens = response.Usage?.OutputTokens ?? 0
                    };
                }

                this.logger.LogWarning("Claude API returned unexpected response format");
                return new ClaudeApiResponse
                {
                    Content = "Error: Unexpected response format from Claude API",
                    InputTokens = 0,
                    OutputTokens = 0
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to call Claude API");
                throw;
            }
        }

        private class ClaudeReviewResponse
        {
            public List<ClaudeComment>? Comments { get; set; }
        }

        private class ClaudeComment
        {
            public string? Content { get; set; }

            public string? FilePath { get; set; }

            public int? LineNumber { get; set; }

            public string? Severity { get; set; }

            public string? Category { get; set; }
        }

        private class ClaudeApiResponse
        {
            public string Content { get; set; } = string.Empty;
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
        }
    }
}