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
        private readonly string defaultModel;
        private readonly int maxTokens;

        public ClaudeApiService(IConfiguration configuration, ILogger<ClaudeApiService> logger)
        {
            this.logger = logger;

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
            var requestId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();

            this.logger.LogInformation(
                "Starting code analysis for PR {PullRequestId} in {Organization}/{Project}/{Repository}",
                request.PullRequestId,
                request.Organization,
                request.Project,
                request.Repository);

            try
            {
                // Build the analysis prompt
                var prompt = this.BuildAnalysisPrompt(request);

                this.logger.LogDebug("Analysis prompt length: {PromptLength} characters", prompt.Length);

                // Call Claude API - we'll implement this step by step
                // For now, create a placeholder response
                this.logger.LogInformation("Calling Claude API for code analysis (placeholder implementation)");

                var response = new
                {
                    Message = new { Content = "Placeholder Claude response - API integration in progress" },
                    Usage = new { InputTokens = 0, OutputTokens = 0 },
                };

                stopwatch.Stop();

                if (string.IsNullOrEmpty(response?.Message?.Content))
                {
                    this.logger.LogWarning("Claude API returned empty response for request {RequestId}", requestId);
                    return this.CreateErrorResult(requestId, request.PullRequestId, "Claude API returned empty response", stopwatch.Elapsed);
                }

                // Parse the response into structured comments
                var result = this.ParseClaudeResponse(requestId, request.PullRequestId, response.Message.Content, stopwatch.Elapsed);

                // Add metadata
                result.Metadata.TokensUsed = response.Usage?.InputTokens + response.Usage?.OutputTokens ?? 0;
                result.Metadata.ModelUsed = this.defaultModel;
                result.Metadata.FilesAnalyzed = request.ChangedFiles.Count;

                this.logger.LogInformation(
                    "Code analysis completed for PR {PullRequestId}. Generated {CommentCount} comments in {ElapsedTime}ms using {TokensUsed} tokens",
                    request.PullRequestId,
                    result.Comments.Count,
                    stopwatch.ElapsedMilliseconds,
                    result.Metadata.TokensUsed);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                this.logger.LogError(ex, "Failed to analyze code for PR {PullRequestId}: {ErrorMessage}", request.PullRequestId, ex.Message);
                return this.CreateErrorResult(requestId, request.PullRequestId, ex.Message, stopwatch.Elapsed);
            }
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
    }
}