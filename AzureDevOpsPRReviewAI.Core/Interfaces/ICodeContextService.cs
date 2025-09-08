namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface ICodeContextService
    {
        Task<List<CodeChunk>> ChunkCodeAsync(string filePath, string content);

        Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, int maxTokens = 100000);

        Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, QuerySettings querySettings, int maxTokens = 100000);

        Task<ContextResult> BuildAnalysisContextAsync(string repositoryPath, PullRequestAnalysisRequest request);

        Task<int> CountTokensAsync(string text);

        Task<List<string>> AnalyzeFileDependenciesAsync(string repositoryPath, string filePath);

        Task<SemanticSearchResult> SearchSimilarCodeAsync(string repositoryPath, string codeSnippet, int topResults = 10);

        Task IndexRepositoryAsync(string repositoryPath);

        Task UpdateIndexAsync(string repositoryPath, IEnumerable<string> changedFiles);
    }
}