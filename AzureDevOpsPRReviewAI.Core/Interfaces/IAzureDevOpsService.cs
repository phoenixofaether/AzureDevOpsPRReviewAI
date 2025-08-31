namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface IAzureDevOpsService
    {
        Task<PullRequestModel?> GetPullRequestAsync(string organization, string project, string repository, int pullRequestId);

        Task<List<PullRequestFile>> GetPullRequestFilesAsync(string organization, string project, string repository, int pullRequestId);

        Task<string> GetFileDiffAsync(string organization, string project, string repository, int pullRequestId, string filePath);

        Task<bool> PostPullRequestCommentAsync(string organization, string project, string repository, int pullRequestId, string comment, string filePath = "", int lineNumber = 0);

        Task<bool> ValidateConnectionAsync();
    }
}