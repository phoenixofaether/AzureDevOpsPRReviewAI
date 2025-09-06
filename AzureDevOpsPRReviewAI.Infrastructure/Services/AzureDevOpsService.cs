namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.TeamFoundation.SourceControl.WebApi;
    using Microsoft.VisualStudio.Services.Common;
    using Microsoft.VisualStudio.Services.WebApi;
    using Polly;
    using System.Text;

    public class AzureDevOpsService : IAzureDevOpsService
    {
        private readonly IAuthenticationService authService;
        private readonly ILogger<AzureDevOpsService> logger;
        private readonly ResiliencePipeline resiliencePipeline;

        public AzureDevOpsService(IAuthenticationService authService, ILogger<AzureDevOpsService> logger)
        {
            this.authService = authService;
            this.logger = logger;

            this.resiliencePipeline = new ResiliencePipelineBuilder()
                .AddRetry(new Polly.Retry.RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(2),
                    BackoffType = Polly.DelayBackoffType.Exponential,
                    UseJitter = true,
                })
                .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(30),
                })
                .Build();
        }

        public async Task<PullRequestModel?> GetPullRequestAsync(string organization, string project, string repository, int pullRequestId)
        {
            try
            {
                return await this.resiliencePipeline.ExecuteAsync(async _ =>
                {
                    var connection = await this.CreateConnectionAsync(organization);
                    if (connection == null)
                    {
                        return null;
                    }

                    var gitClient = connection.GetClient<GitHttpClient>();
                    var pullRequest = await gitClient.GetPullRequestAsync(project, repository, pullRequestId);

                    this.logger.LogInformation(
                        "Retrieved pull request {PullRequestId} from {Organization}/{Project}/{Repository}",
                        pullRequestId,
                        organization,
                        project,
                        repository);

                    return new PullRequestModel
                    {
                        PullRequestId = pullRequest.PullRequestId,
                        Title = pullRequest.Title,
                        Description = pullRequest.Description,
                        SourceBranch = pullRequest.SourceRefName,
                        TargetBranch = pullRequest.TargetRefName,
                        AuthorName = pullRequest.CreatedBy.DisplayName,
                        AuthorEmail = pullRequest.CreatedBy.UniqueName,
                        CreatedDate = pullRequest.CreationDate,
                        UpdatedDate = pullRequest.LastMergeSourceCommit?.Committer?.Date,
                        Status = pullRequest.Status switch
                        {
                            Microsoft.TeamFoundation.SourceControl.WebApi.PullRequestStatus.Active => Core.Models.PullRequestStatus.Active,
                            Microsoft.TeamFoundation.SourceControl.WebApi.PullRequestStatus.Completed => Core.Models.PullRequestStatus.Completed,
                            Microsoft.TeamFoundation.SourceControl.WebApi.PullRequestStatus.Abandoned => Core.Models.PullRequestStatus.Abandoned,
                            _ => Core.Models.PullRequestStatus.Active,
                        },
                        RepositoryName = repository,
                        ProjectName = project,
                        OrganizationName = organization,
                        ReviewerIds = pullRequest.Reviewers?.Select(r => r.Id).ToList() ?? new List<string>(),
                    };
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to retrieve pull request {PullRequestId} from {Organization}/{Project}/{Repository}",
                    pullRequestId,
                    organization,
                    project,
                    repository);
                return null;
            }
        }

        public async Task<List<PullRequestFile>> GetPullRequestFilesAsync(string organization, string project, string repository, int pullRequestId)
        {
            try
            {
                return await this.resiliencePipeline.ExecuteAsync(async _ =>
                {
                    var connection = await this.CreateConnectionAsync(organization);
                    if (connection == null)
                    {
                        return new List<PullRequestFile>();
                    }

                    var gitClient = connection.GetClient<GitHttpClient>();
                    var iterations = await gitClient.GetPullRequestIterationsAsync(project, repository, pullRequestId);

                    if (!iterations.Any())
                    {
                        return new List<PullRequestFile>();
                    }

                    var latestIteration = iterations.Last();
                    var changes = await gitClient.GetPullRequestIterationChangesAsync(project, repository, pullRequestId, latestIteration.Id!.Value);

                    var files = new List<PullRequestFile>();
                    foreach (var change in changes.ChangeEntries)
                    {
                        var file = new PullRequestFile
                        {
                            FilePath = change.Item.Path,
                            ChangeType = change.ChangeType switch
                            {
                                VersionControlChangeType.Add => FileChangeType.Add,
                                VersionControlChangeType.Edit => FileChangeType.Edit,
                                VersionControlChangeType.Delete => FileChangeType.Delete,
                                VersionControlChangeType.Rename => FileChangeType.Rename,
                                _ => FileChangeType.Edit,
                            },
                        };
                        files.Add(file);
                    }

                    this.logger.LogInformation(
                        "Retrieved {FileCount} files for pull request {PullRequestId}",
                        files.Count,
                        pullRequestId);
                    return files;
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to retrieve files for pull request {PullRequestId}", pullRequestId);
                return new List<PullRequestFile>();
            }
        }

        public async Task<string> GetFileDiffAsync(string organization, string project, string repository, int pullRequestId, string filePath)
        {
            try
            {
                return await this.resiliencePipeline.ExecuteAsync(async _ =>
                {
                    var connection = await this.CreateConnectionAsync(organization);
                    if (connection == null)
                    {
                        return string.Empty;
                    }

                    var gitClient = connection.GetClient<GitHttpClient>();
                    var iterations = await gitClient.GetPullRequestIterationsAsync(project, repository, pullRequestId);

                    if (!iterations.Any())
                    {
                        return string.Empty;
                    }

                    var latestIteration = iterations.Last();

                    // Get the diff for the specific file
                    var diffStream = await gitClient.GetPullRequestIterationChangesAsync(
                        project,
                        repository,
                        pullRequestId,
                        latestIteration.Id!.Value,
                        top: 1000,
                        skip: 0);

                    // For now, return a placeholder - full diff implementation would be more complex
                    this.logger.LogInformation(
                        "Retrieved diff for file {FilePath} in pull request {PullRequestId}",
                        filePath,
                        pullRequestId);
                    return $"Diff content for {filePath} (placeholder - full implementation needed)";
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to retrieve diff for file {FilePath} in pull request {PullRequestId}",
                    filePath,
                    pullRequestId);
                return string.Empty;
            }
        }

        public async Task<bool> PostPullRequestCommentAsync(string organization, string project, string repository, int pullRequestId, string comment, string filePath = "", int lineNumber = 0)
        {
            try
            {
                return await this.resiliencePipeline.ExecuteAsync(async _ =>
                {
                    var connection = await this.CreateConnectionAsync(organization);
                    if (connection == null)
                    {
                        return false;
                    }

                    var gitClient = connection.GetClient<GitHttpClient>();

                    var commentThread = new GitPullRequestCommentThread
                    {
                        Comments = new List<Comment>
                        {
                            new Comment
                            {
                                Content = comment,
                                CommentType = CommentType.Text,
                            },
                        },
                        Status = CommentThreadStatus.Active,
                    };

                    // Add position context if file path and line number are provided
                    if (!string.IsNullOrEmpty(filePath) && lineNumber > 0)
                    {
                        commentThread.ThreadContext = new CommentThreadContext
                        {
                            FilePath = filePath,
                            RightFileStart = new CommentPosition { Line = lineNumber, Offset = 1 },
                            RightFileEnd = new CommentPosition { Line = lineNumber, Offset = 1 },
                        };
                    }

                    var result = await gitClient.CreateThreadAsync(commentThread, project, repository, pullRequestId);

                    this.logger.LogInformation(
                        "Posted comment to pull request {PullRequestId} at {FilePath}:{LineNumber}",
                        pullRequestId,
                        filePath,
                        lineNumber);
                    return result != null;
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to post comment to pull request {PullRequestId}", pullRequestId);
                return false;
            }
        }

        public async Task<bool> ValidateConnectionAsync()
        {
            try
            {
                return await this.resiliencePipeline.ExecuteAsync(async _ =>
                {
                    var isAuthenticated = await this.authService.IsAuthenticatedAsync();
                    if (!isAuthenticated)
                    {
                        this.logger.LogWarning("Authentication service is not authenticated");
                        return false;
                    }

                    // Try to create a connection and validate it
                    var connection = await this.CreateConnectionAsync("davidoberkalmsteiner");
                    if (connection == null)
                    {
                        return false;
                    }

                    // Try to get user profile to validate connection
                    var profileClient = connection.GetClient<Microsoft.VisualStudio.Services.Profile.Client.ProfileHttpClient>();
                    var profile = await profileClient.GetProfileAsync(new Microsoft.VisualStudio.Services.Profile.ProfileQueryContext(Microsoft.VisualStudio.Services.Profile.AttributesScope.Core));

                    this.logger.LogInformation("Azure DevOps connection validated for user: {DisplayName}", profile.DisplayName);
                    return true;
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to validate Azure DevOps connection");
                return false;
            }
        }

        private async Task<VssConnection?> CreateConnectionAsync(string organization)
        {
            var token = await this.authService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                this.logger.LogError("No access token available for Azure DevOps connection");
                return null;
            }

            var orgUrl = new Uri($"https://dev.azure.com/{organization}");
            var credentials = new VssBasicCredential(string.Empty, token);

            return new VssConnection(orgUrl, credentials);
        }
    }
}