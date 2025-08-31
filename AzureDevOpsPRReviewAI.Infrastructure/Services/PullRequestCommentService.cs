namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using System.Text.Json;
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.TeamFoundation.SourceControl.WebApi;
    using Microsoft.VisualStudio.Services.Common;
    using Microsoft.VisualStudio.Services.WebApi;

    public class PullRequestCommentService : IPullRequestCommentService
    {
        private readonly IAuthenticationService authenticationService;
        private readonly ILogger<PullRequestCommentService> logger;

        public PullRequestCommentService(
            IAuthenticationService authenticationService,
            ILogger<PullRequestCommentService> logger)
        {
            this.authenticationService = authenticationService;
            this.logger = logger;
        }

        public async Task<CommentPostResult> PostCommentAsync(
            string organization,
            string project,
            string repository,
            int pullRequestId,
            FormattedComment comment,
            CancellationToken cancellationToken = default)
        {
            try
            {
                this.logger.LogInformation(
                    "Posting comment to PR {PullRequestId} in {Organization}/{Project}/{Repository}",
                    pullRequestId,
                    organization,
                    project,
                    repository);

                var connection = await this.CreateConnectionAsync(organization);
                var gitClient = connection.GetClient<GitHttpClient>();

                // Create the comment thread
                var thread = new GitPullRequestCommentThread
                {
                    Comments = new List<Comment>
                    {
                        new Comment
                        {
                            Content = comment.Content,
                            CommentType = CommentType.Text
                        }
                    },
                    Status = CommentThreadStatus.Active
                };

                // Add thread position if file path and line number are specified
                if (!string.IsNullOrEmpty(comment.FilePath) && comment.LineNumber.HasValue)
                {
                    thread.ThreadContext = new CommentThreadContext
                    {
                        FilePath = comment.FilePath,
                        RightFileStart = new CommentPosition
                        {
                            Line = comment.LineNumber.Value,
                            Offset = 1
                        },
                        RightFileEnd = new CommentPosition
                        {
                            Line = comment.LineNumber.Value,
                            Offset = 1
                        }
                    };
                }

                var postedThread = await gitClient.CreateThreadAsync(
                    thread,
                    project,
                    repository,
                    pullRequestId,
                    cancellationToken: cancellationToken);

                var result = new CommentPostResult
                {
                    CommentId = postedThread.Id.ToString(),
                    IsSuccessful = true,
                    PostedAt = DateTime.UtcNow
                };

                this.logger.LogInformation(
                    "Successfully posted comment {CommentId} to PR {PullRequestId}",
                    result.CommentId,
                    pullRequestId);

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to post comment to PR {PullRequestId} in {Organization}/{Project}/{Repository}: {ErrorMessage}",
                    pullRequestId,
                    organization,
                    project,
                    repository,
                    ex.Message);

                return new CommentPostResult
                {
                    CommentId = string.Empty,
                    IsSuccessful = false,
                    ErrorMessage = ex.Message,
                    PostedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<List<CommentPostResult>> PostCommentsAsync(
            string organization,
            string project,
            string repository,
            int pullRequestId,
            List<FormattedComment> comments,
            CancellationToken cancellationToken = default)
        {
            var results = new List<CommentPostResult>();

            this.logger.LogInformation(
                "Posting {CommentCount} comments to PR {PullRequestId}",
                comments.Count,
                pullRequestId);

            foreach (var comment in comments)
            {
                var result = await this.PostCommentAsync(
                    organization,
                    project,
                    repository,
                    pullRequestId,
                    comment,
                    cancellationToken);

                results.Add(result);

                // Add small delay between comments to avoid rate limiting
                if (comments.Count > 1)
                {
                    await Task.Delay(500, cancellationToken);
                }
            }

            var successCount = results.Count(r => r.IsSuccessful);
            this.logger.LogInformation(
                "Posted {SuccessCount}/{TotalCount} comments successfully to PR {PullRequestId}",
                successCount,
                comments.Count,
                pullRequestId);

            return results;
        }

        public async Task<bool> DeleteCommentAsync(
            string organization,
            string project,
            string repository,
            int pullRequestId,
            string commentId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                this.logger.LogInformation(
                    "Deleting comment {CommentId} from PR {PullRequestId}",
                    commentId,
                    pullRequestId);

                var connection = await this.CreateConnectionAsync(organization);
                var gitClient = connection.GetClient<GitHttpClient>();

                if (int.TryParse(commentId, out var threadId))
                {
                    var thread = await gitClient.GetPullRequestThreadAsync(
                        project,
                        repository,
                        pullRequestId,
                        threadId,
                        cancellationToken: cancellationToken);

                    if (thread != null)
                    {
                        // Mark thread as fixed/resolved to effectively hide it
                        thread.Status = CommentThreadStatus.Fixed;
                        
                        await gitClient.UpdateThreadAsync(
                            thread,
                            project,
                            repository,
                            pullRequestId,
                            threadId,
                            cancellationToken: cancellationToken);

                        this.logger.LogInformation(
                            "Successfully deleted/resolved comment {CommentId} from PR {PullRequestId}",
                            commentId,
                            pullRequestId);

                        return true;
                    }
                }

                this.logger.LogWarning(
                    "Comment {CommentId} not found in PR {PullRequestId}",
                    commentId,
                    pullRequestId);

                return false;
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to delete comment {CommentId} from PR {PullRequestId}: {ErrorMessage}",
                    commentId,
                    pullRequestId,
                    ex.Message);

                return false;
            }
        }

        public async Task<List<string>> GetExistingAICommentsAsync(
            string organization,
            string project,
            string repository,
            int pullRequestId,
            string requestId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                this.logger.LogInformation(
                    "Retrieving existing AI comments for PR {PullRequestId} with request ID {RequestId}",
                    pullRequestId,
                    requestId);

                var connection = await this.CreateConnectionAsync(organization);
                var gitClient = connection.GetClient<GitHttpClient>();

                var threads = await gitClient.GetThreadsAsync(
                    project,
                    repository,
                    pullRequestId,
                    cancellationToken: cancellationToken);

                var aiCommentIds = new List<string>();

                foreach (var thread in threads)
                {
                    if (thread.Comments != null)
                    {
                        foreach (var comment in thread.Comments)
                        {
                            // Check if comment contains AI attribution and matches request ID
                            if (!string.IsNullOrEmpty(comment.Content) &&
                                comment.Content.Contains("Generated by AI Code Review") &&
                                comment.Content.Contains($"Request ID: `{requestId}`"))
                            {
                                aiCommentIds.Add(thread.Id.ToString());
                                break; // Only need the thread ID once
                            }
                        }
                    }
                }

                this.logger.LogInformation(
                    "Found {CommentCount} existing AI comments for request {RequestId} in PR {PullRequestId}",
                    aiCommentIds.Count,
                    requestId,
                    pullRequestId);

                return aiCommentIds;
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to retrieve existing AI comments for PR {PullRequestId}: {ErrorMessage}",
                    pullRequestId,
                    ex.Message);

                return new List<string>();
            }
        }

        private async Task<VssConnection> CreateConnectionAsync(string organization)
        {
            var token = await this.authenticationService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("No access token available for Azure DevOps connection");
            }

            var orgUrl = new Uri($"https://dev.azure.com/{organization}");
            var credentials = new VssBasicCredential(string.Empty, token);

            return new VssConnection(orgUrl, credentials);
        }
    }
}