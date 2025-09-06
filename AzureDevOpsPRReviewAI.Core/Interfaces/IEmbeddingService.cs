namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    public interface IEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);

        Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts);

        Task<double> CalculateSimilarityAsync(float[] embedding1, float[] embedding2);
    }
}