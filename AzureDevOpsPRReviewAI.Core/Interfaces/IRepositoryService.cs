namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface IRepositoryService
    {
        Task<RepositoryCloneResult> CloneRepositoryAsync(string organization, string project, string repository, string accessToken);

        Task<GitDiffResult> GetPullRequestDiffAsync(string repositoryPath, string sourceBranch, string targetBranch);

        Task<string?> GetFileContentAsync(string repositoryPath, string filePath, string? branch = null);

        Task<List<string>> GetAllFilesAsync(string repositoryPath, string? branch = null);

        Task<bool> SwitchBranchAsync(string repositoryPath, string branchName);

        Task CleanupRepositoryAsync(string repositoryPath);

        Task<DiskSpaceInfo> GetDiskSpaceInfoAsync();

        Task PerformRepositoryCleanupAsync();
    }
}