namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using Anthropic.SDK;
    using Anthropic.SDK.Constants;
    using Anthropic.SDK.Messaging;
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class TokenizerService : ITokenizerService
    {
        private readonly ILogger<TokenizerService> logger;
        private readonly AnthropicClient anthropicClient;
        private readonly string defaultModel;

        public TokenizerService(ILogger<TokenizerService> logger, IConfiguration configuration)
        {
            this.logger = logger;

            var apiKey = configuration["Claude:ApiKey"];
            this.anthropicClient = new AnthropicClient(apiKey);
            this.defaultModel = configuration["Claude:Model"] ?? AnthropicModels.Claude35Haiku;
        }

        public async Task<int> CountTokensAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            try
            {
                var messages = new List<Message>
                {
                    new Message(RoleType.User, text),
                };

                var parameters = new MessageCountTokenParameters
                {
                    Messages = messages,
                    Model = this.defaultModel,
                };

                var response = await this.anthropicClient.Messages.CountMessageTokensAsync(parameters);
                return response.InputTokens;
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "Failed to count tokens using Anthropic API, falling back to approximation");

                // Fallback to character-based approximation
                return Math.Max(1, text.Length / 4);
            }
        }

        public async Task<List<string>> TokenizeAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            try
            {
                // Note: Anthropic API doesn't provide individual tokens
                // This is a simplified approximation for backwards compatibility
                var tokenCount = await this.CountTokensAsync(text);
                var approximateTokens = new List<string>();

                // Split text into words as a rough approximation
                var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                if (words.Length == 0)
                {
                    return approximateTokens;
                }

                // Distribute tokens across words proportionally
                var wordsPerToken = Math.Max(1.0, (double)words.Length / tokenCount);

                for (int i = 0; i < words.Length; i += (int)Math.Ceiling(wordsPerToken))
                {
                    var tokenWords = words.Skip(i).Take((int)Math.Ceiling(wordsPerToken));
                    approximateTokens.Add(string.Join(" ", tokenWords));
                }

                return approximateTokens;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to tokenize text");
                return new List<string>();
            }
        }

        public async Task<string> TruncateToTokenLimitAsync(string text, int maxTokens)
        {
            if (string.IsNullOrEmpty(text) || maxTokens <= 0)
            {
                return string.Empty;
            }

            try
            {
                var originalTokenCount = await this.CountTokensAsync(text);

                if (originalTokenCount <= maxTokens)
                {
                    return text;
                }

                // Use binary search to find the maximum text length that fits within token limit
                var left = 0;
                var right = text.Length;
                var bestResult = string.Empty;

                while (left <= right)
                {
                    var mid = (left + right) / 2;
                    var truncatedText = text.Substring(0, mid);
                    var tokenCount = await this.CountTokensAsync(truncatedText);

                    if (tokenCount <= maxTokens)
                    {
                        bestResult = truncatedText;
                        left = mid + 1;
                    }
                    else
                    {
                        right = mid - 1;
                    }
                }

                this.logger.LogDebug(
                    "Truncated text from {OriginalTokens} to approximately {MaxTokens} tokens",
                    originalTokenCount,
                    maxTokens);

                return bestResult;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to truncate text, returning original");
                return text;
            }
        }
    }
}