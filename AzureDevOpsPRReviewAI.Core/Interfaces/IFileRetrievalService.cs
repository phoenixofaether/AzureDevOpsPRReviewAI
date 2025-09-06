namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface IFileRetrievalService
    {
        Task<FileContent?> GetFileAsync(string repositoryPath, string filePath, string? branch = null);

        Task<List<FileContent>> GetFilesAsync(string repositoryPath, IEnumerable<string> filePaths, string? branch = null);

        Task<bool> IsBinaryFileAsync(string filePath);

        Task<string> DetectFileEncodingAsync(string filePath);

        Task<List<string>> FilterFilesAsync(string repositoryPath, IEnumerable<string> filePaths, FileFilterOptions options);

        Task CacheFileAsync(string repositoryPath, string filePath, FileContent content);

        Task<FileContent?> GetCachedFileAsync(string repositoryPath, string filePath);

        Task InvalidateCacheAsync(string repositoryPath, string? filePath = null);
    }
}