namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface ICodeChunkingService
    {
        Task<List<CodeChunk>> ChunkCodeAsync(string filePath, string content);

        Task<int> CountTokensAsync(string text);
    }
}