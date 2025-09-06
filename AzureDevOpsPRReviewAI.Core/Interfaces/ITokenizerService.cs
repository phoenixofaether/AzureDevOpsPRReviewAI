namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    public interface ITokenizerService
    {
        Task<int> CountTokensAsync(string text);
        Task<List<string>> TokenizeAsync(string text);
        Task<string> TruncateToTokenLimitAsync(string text, int maxTokens);
    }
}