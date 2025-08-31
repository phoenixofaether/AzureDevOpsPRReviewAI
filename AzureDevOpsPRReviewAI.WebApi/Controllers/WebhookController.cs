namespace AzureDevOpsPRReviewAI.WebApi.Controllers
{
    using System.Text;
    using System.Text.Json;
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/[controller]")]
    public class WebhookController : ControllerBase
    {
        private readonly IAzureDevOpsService azureDevOpsService;
        private readonly ICommandParserService commandParserService;
        private readonly IClaudeApiService claudeApiService;
        private readonly ICommentFormatterService commentFormatterService;
        private readonly IPullRequestCommentService pullRequestCommentService;
        private readonly ILogger<WebhookController> logger;
        private readonly IConfiguration configuration;

        public WebhookController(
            IAzureDevOpsService azureDevOpsService,
            ICommandParserService commandParserService,
            IClaudeApiService claudeApiService,
            ICommentFormatterService commentFormatterService,
            IPullRequestCommentService pullRequestCommentService,
            ILogger<WebhookController> logger,
            IConfiguration configuration)
        {
            this.azureDevOpsService = azureDevOpsService;
            this.commandParserService = commandParserService;
            this.claudeApiService = claudeApiService;
            this.commentFormatterService = commentFormatterService;
            this.pullRequestCommentService = pullRequestCommentService;
            this.logger = logger;
            this.configuration = configuration;
        }

        [HttpPost("pullrequest")]
        public async Task<IActionResult> HandlePullRequestWebhook()
        {
            try
            {
                // Read the request body
                using var reader = new StreamReader(this.Request.Body);
                var requestBody = await reader.ReadToEndAsync();

                this.logger.LogDebug("Received webhook payload: {RequestBody}", requestBody);

                // Validate HTTP Basic Authentication
                if (!this.ValidateBasicAuth())
                {
                    this.logger.LogWarning("Invalid webhook authentication");
                    return this.Unauthorized("Invalid authentication");
                }

                // Parse the webhook event
                var webhookEvent = JsonSerializer.Deserialize<AzureDevOpsWebhookEvent>(requestBody);
                if (webhookEvent?.Resource == null)
                {
                    this.logger.LogWarning("Invalid webhook payload - no resource data");
                    return this.BadRequest("Invalid webhook payload");
                }

                // Check if this is a comment event with AI review command
                if (this.IsCommentEvent(webhookEvent.EventType))
                {
                    await this.ProcessCommentEventAsync(webhookEvent);
                }
                else if (this.IsPullRequestEvent(webhookEvent.EventType))
                {
                    // For now, just log non-comment PR events
                    this.logger.LogInformation("Received PR event {EventType} - not processing automatically", webhookEvent.EventType);
                    return this.Ok("PR event logged");
                }
                else
                {
                    this.logger.LogInformation("Ignoring non-supported event: {EventType}", webhookEvent.EventType);
                    return this.Ok("Event ignored");
                }

                this.logger.LogInformation(
                    "Successfully processed webhook event {EventId} of type {EventType}",
                    webhookEvent.Id,
                    webhookEvent.EventType);

                return this.Ok("Webhook processed successfully");
            }
            catch (JsonException ex)
            {
                this.logger.LogError(ex, "Failed to parse webhook JSON payload");
                return this.BadRequest("Invalid JSON payload");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to process webhook");
                return this.StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("health")]
        public IActionResult Health()
        {
            return this.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow });
        }

        private bool ValidateBasicAuth()
        {
            // Get the authorization header
            if (!this.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                this.logger.LogWarning("No authorization header found in webhook request");
                return false;
            }

            var authorization = authHeader.ToString();
            if (string.IsNullOrEmpty(authorization) || !authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                this.logger.LogWarning("Invalid authorization header format in webhook request");
                return false;
            }

            try
            {
                // Decode the base64 credentials
                var encodedCredentials = authorization.Substring("Basic ".Length).Trim();
                var credentialBytes = Convert.FromBase64String(encodedCredentials);
                var credentials = Encoding.UTF8.GetString(credentialBytes);

                var separatorIndex = credentials.IndexOf(':');
                if (separatorIndex == -1)
                {
                    this.logger.LogWarning("Invalid basic auth format - no separator found");
                    return false;
                }

                var username = credentials.Substring(0, separatorIndex);
                var password = credentials.Substring(separatorIndex + 1);

                // Get expected credentials from configuration
                var expectedUsername = this.configuration["Webhook:BasicAuth:Username"];
                var expectedPassword = this.configuration["Webhook:BasicAuth:Password"];

                if (string.IsNullOrEmpty(expectedUsername) || string.IsNullOrEmpty(expectedPassword))
                {
                    this.logger.LogError("Webhook basic auth credentials are not configured");
                    return false;
                }

                // Compare credentials using time-constant comparison
                return this.SecureEquals(username, expectedUsername) && this.SecureEquals(password, expectedPassword);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to parse basic auth credentials");
                return false;
            }
        }

        private bool SecureEquals(string a, string b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            var result = 0;
            for (var i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        private bool IsPullRequestEvent(string eventType)
        {
            return eventType switch
            {
                "git.pullrequest.created" => true,
                "git.pullrequest.updated" => true,
                "git.pullrequest.merged" => true,
                _ => false,
            };
        }

        private bool IsCommentEvent(string eventType)
        {
            return eventType switch
            {
                "ms.vss-code.git-pullrequest-comment-event" => true,
                "git.pullrequest.comment" => true,
                _ => false,
            };
        }

        private async Task ProcessCommentEventAsync(AzureDevOpsWebhookEvent webhookEvent)
        {
            var commentResource = webhookEvent.GetCommentResource();
            if (commentResource?.PullRequest == null)
            {
                this.logger.LogWarning("Comment webhook resource missing pull request information");
                return;
            }

            this.logger.LogInformation(
                "Processing comment on PR {PullRequestId}: {CommentContent}",
                commentResource.PullRequest.PullRequestId,
                commentResource.Content);

            // Parse the comment for AI review commands
            var command = this.commandParserService.ParseComment(commentResource.Content);

            if (!command.IsValid)
            {
                this.logger.LogInformation("Comment does not contain valid AI review command");
                return;
            }

            this.logger.LogInformation(
                "Found AI review command '{Command}' with {ParameterCount} parameters",
                command.Command,
                command.Parameters.Count);

            // Log parameters for debugging
            foreach (var param in command.Parameters)
            {
                this.logger.LogInformation("Parameter: {Key} = {Value}", param.Key, param.Value);
            }

            var pullRequest = commentResource.PullRequest;
            var repository = pullRequest.Repository;

            if (repository?.Project == null)
            {
                this.logger.LogWarning("Comment resource missing required repository/project information");
                return;
            }

            this.logger.LogInformation(
                "Triggering AI review for PR {PullRequestId} in {Organization}/{Project}/{Repository} - Command: {Command}",
                pullRequest.PullRequestId,
                "organization", // Will need to extract from webhook or config
                repository.Project.Name,
                repository.Name,
                command.Command);

            // Trigger AI analysis
            try
            {
                var analysisRequest = new CodeAnalysisRequest
                {
                    PullRequestId = pullRequest.PullRequestId.ToString(),
                    Organization = "organization", // Extract from config or webhook
                    Project = repository.Project.Name,
                    Repository = repository.Name,
                    SourceBranch = pullRequest.SourceRefName ?? "unknown",
                    TargetBranch = pullRequest.TargetRefName ?? "unknown",
                    Title = pullRequest.Title,
                    Description = "Pull request comment triggered review",
                    Command = command
                };

                var analysisResult = await this.claudeApiService.AnalyzeCodeAsync(analysisRequest);

                if (analysisResult.IsSuccessful)
                {
                    this.logger.LogInformation(
                        "AI analysis completed for PR {PullRequestId}. Generated {CommentCount} review comments",
                        pullRequest.PullRequestId,
                        analysisResult.Comments.Count);

                    // Format and post comments back to Azure DevOps
                    await this.PostReviewCommentsAsync(
                        "organization", // Extract from config or webhook
                        repository.Project.Name,
                        repository.Name,
                        pullRequest.PullRequestId,
                        analysisResult);
                }
                else
                {
                    this.logger.LogError(
                        "AI analysis failed for PR {PullRequestId}: {ErrorMessage}",
                        pullRequest.PullRequestId,
                        analysisResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to perform AI analysis for PR {PullRequestId}", pullRequest.PullRequestId);
            }
        }

        private async Task ProcessPullRequestEventAsync(AzureDevOpsWebhookEvent webhookEvent)
        {
            var resource = webhookEvent.GetPullRequestResource();
            if (resource?.Repository?.Project == null)
            {
                this.logger.LogWarning("Webhook resource missing required repository/project information");
                return;
            }

            var repository = resource.Repository!;
            var project = repository.Project!;

            this.logger.LogInformation(
                "Processing pull request {PullRequestId} in {Organization}/{Project}/{Repository} - Event: {EventType}",
                resource.PullRequestId,
                "organization", // Will need to extract from webhook or config
                project.Name,
                repository.Name,
                webhookEvent.EventType);

            // For now, just log the event - full PR processing will be implemented in Phase 4
            switch (webhookEvent.EventType)
            {
                case "git.pullrequest.created":
                    this.logger.LogInformation("New pull request created: {Title}", resource.Title);
                    break;
                case "git.pullrequest.updated":
                    this.logger.LogInformation("Pull request updated: {Title}", resource.Title);
                    break;
                case "git.pullrequest.merged":
                    this.logger.LogInformation("Pull request merged: {Title}", resource.Title);
                    break;
            }

            // TODO: In Phase 4, this will trigger the AI analysis pipeline
            // await aiAnalysisService.AnalyzePullRequestAsync(organization, project.Name, repository.Name, resource.PullRequestId);
        }

        private async Task PostReviewCommentsAsync(
            string organization,
            string project,
            string repository,
            int pullRequestId,
            CodeAnalysisResult analysisResult)
        {
            try
            {
                this.logger.LogInformation(
                    "Posting {CommentCount} review comments to PR {PullRequestId}",
                    analysisResult.Comments.Count,
                    pullRequestId);

                // Check for existing AI comments from the same request and delete them
                var existingCommentIds = await this.pullRequestCommentService.GetExistingAICommentsAsync(
                    organization,
                    project,
                    repository,
                    pullRequestId,
                    analysisResult.RequestId);

                if (existingCommentIds.Count > 0)
                {
                    this.logger.LogInformation(
                        "Found {ExistingCommentCount} existing AI comments for request {RequestId}. Cleaning up.",
                        existingCommentIds.Count,
                        analysisResult.RequestId);

                    foreach (var commentId in existingCommentIds)
                    {
                        await this.pullRequestCommentService.DeleteCommentAsync(
                            organization,
                            project,
                            repository,
                            pullRequestId,
                            commentId);
                    }
                }

                // Format the analysis results into structured comments
                var formattedComments = this.commentFormatterService.FormatAnalysisResults(analysisResult);

                // Post the formatted comments
                var results = await this.pullRequestCommentService.PostCommentsAsync(
                    organization,
                    project,
                    repository,
                    pullRequestId,
                    formattedComments);

                var successCount = results.Count(r => r.IsSuccessful);
                var failureCount = results.Count - successCount;

                this.logger.LogInformation(
                    "Posted review comments to PR {PullRequestId}: {SuccessCount} successful, {FailureCount} failed",
                    pullRequestId,
                    successCount,
                    failureCount);

                if (failureCount > 0)
                {
                    var failedResults = results.Where(r => !r.IsSuccessful).ToList();
                    foreach (var failedResult in failedResults)
                    {
                        this.logger.LogWarning(
                            "Failed to post comment to PR {PullRequestId}: {ErrorMessage}",
                            pullRequestId,
                            failedResult.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to post review comments to PR {PullRequestId}: {ErrorMessage}",
                    pullRequestId,
                    ex.Message);
            }
        }
    }
}