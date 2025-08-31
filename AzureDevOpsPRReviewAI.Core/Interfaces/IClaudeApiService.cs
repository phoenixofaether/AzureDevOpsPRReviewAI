namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface IClaudeApiService
    {
        Task<CodeAnalysisResult> AnalyzeCodeAsync(CodeAnalysisRequest request, CancellationToken cancellationToken = default);
    }
}