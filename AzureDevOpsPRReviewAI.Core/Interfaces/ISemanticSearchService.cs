namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface ISemanticSearchService
    {
        Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, int maxTokens = 100000);
        Task<SemanticSearchResult> SearchSimilarCodeAsync(string repositoryPath, string codeSnippet, int topResults = 10);
        Task IndexRepositoryAsync(string repositoryPath);
        Task UpdateIndexAsync(string repositoryPath, IEnumerable<string> changedFiles);
    }
}