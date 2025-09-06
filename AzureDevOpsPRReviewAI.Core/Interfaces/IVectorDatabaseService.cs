namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface IVectorDatabaseService
    {
        Task InitializeAsync();

        Task UpsertEmbeddingsAsync(List<CodeChunkEmbedding> embeddings);

        Task<List<SimilarityResult>> SearchSimilarAsync(float[] queryEmbedding, int topK = 10, double threshold = 0.7);

        Task DeleteByRepositoryAsync(string repositoryPath);

        Task UpdateEmbeddingsAsync(List<CodeChunkEmbedding> embeddings);

        Task<bool> ExistsAsync(string embeddingId);

        Task<int> GetEmbeddingCountAsync(string? repositoryPath = null);
    }
}