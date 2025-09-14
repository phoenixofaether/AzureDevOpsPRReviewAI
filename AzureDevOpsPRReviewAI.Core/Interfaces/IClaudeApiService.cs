namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface IClaudeApiService
    {
        Task<CodeAnalysisResult> AnalyzeCodeAsync(CodeAnalysisRequest request, CancellationToken cancellationToken = default);
        
        Task<CodeAnalysisResult> AnalyzeCodeAsync(CodeAnalysisRequest request, RepositoryConfiguration repositoryConfig, CancellationToken cancellationToken = default);
    }
}