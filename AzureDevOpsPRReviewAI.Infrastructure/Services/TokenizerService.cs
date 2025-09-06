namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using Microsoft.Extensions.Logging;
    using System.Text;
    using System.Text.RegularExpressions;

    public class TokenizerService : ITokenizerService
    {
        private readonly ILogger<TokenizerService> logger;

        // Token patterns based on common tokenization rules for code and natural language
        private static readonly Regex TokenPattern = new Regex(
            @"\b\w+\b|" +           // Word tokens (alphanumeric sequences)
            @"[^\w\s]|" +           // Punctuation and symbols
            @"\s+",                 // Whitespace sequences
            RegexOptions.Compiled);

        // Common programming tokens that should be counted as single units
        private static readonly HashSet<string> ProgrammingTokens = new HashSet<string>
        {
            "public", "private", "protected", "internal", "static", "readonly", "const", "virtual", "override",
            "abstract", "sealed", "partial", "async", "await", "return", "void", "string", "int", "bool",
            "class", "interface", "struct", "enum", "namespace", "using", "if", "else", "for", "foreach",
            "while", "do", "switch", "case", "break", "continue", "try", "catch", "finally", "throw",
            "var", "let", "const", "function", "=>", "==", "!=", "<=", ">=", "&&", "||", "++", "--"
        };

        public TokenizerService(ILogger<TokenizerService> logger)
        {
            this.logger = logger;
        }

        public async Task<int> CountTokensAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var tokens = this.TokenizeText(text);
                    return tokens.Count;
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to tokenize text properly, falling back to approximation");

                    // Fallback to character-based approximation
                    return Math.Max(1, text.Length / 4);
                }
            });
        }

        public async Task<List<string>> TokenizeAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<string>();
            }

            return await Task.Run(() =>
            {
                try
                {
                    return this.TokenizeText(text);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to tokenize text");
                    return new List<string>();
                }
            });
        }

        public async Task<string> TruncateToTokenLimitAsync(string text, int maxTokens)
        {
            if (string.IsNullOrEmpty(text) || maxTokens <= 0)
            {
                return string.Empty;
            }

            return await Task.Run(() =>
            {
                try
                {
                    var tokens = this.TokenizeText(text);

                    if (tokens.Count <= maxTokens)
                    {
                        return text;
                    }

                    // Take only the first maxTokens tokens and reconstruct text
                    var truncatedTokens = tokens.Take(maxTokens).ToList();
                    var result = this.ReconstructTextFromTokens(truncatedTokens, text);

                    this.logger.LogDebug(
                        "Truncated text from {OriginalTokens} to {TruncatedTokens} tokens",
                        tokens.Count,
                        truncatedTokens.Count);

                    return result;
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Failed to truncate text, returning original");
                    return text;
                }
            });
        }

        private List<string> TokenizeText(string text)
        {
            var tokens = new List<string>();
            var matches = TokenPattern.Matches(text);

            foreach (Match match in matches)
            {
                var token = match.Value;

                // Skip pure whitespace tokens for counting purposes
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                // Handle special cases for code tokens
                if (this.IsCodeToken(token))
                {
                    tokens.Add(token);
                }
                else if (this.IsIdentifier(token))
                {
                    // Split camelCase and PascalCase identifiers
                    tokens.AddRange(this.SplitIdentifier(token));
                }
                else
                {
                    tokens.Add(token);
                }
            }

            return tokens;
        }

        private bool IsCodeToken(string token)
        {
            return ProgrammingTokens.Contains(token.ToLower()) || 
                   token.All(c => !char.IsLetterOrDigit(c)); // Operators and punctuation
        }

        private bool IsIdentifier(string token)
        {
            return token.All(c => char.IsLetterOrDigit(c) || c == '_') && 
                   char.IsLetter(token[0]);
        }

        private List<string> SplitIdentifier(string identifier)
        {
            var parts = new List<string>();
            var currentPart = new StringBuilder();

            for (int i = 0; i < identifier.Length; i++)
            {
                char current = identifier[i];

                if (i > 0 && char.IsUpper(current) && 
                    (char.IsLower(identifier[i - 1]) || 
                     (i < identifier.Length - 1 && char.IsLower(identifier[i + 1]))))
                {
                    // CamelCase or PascalCase boundary
                    if (currentPart.Length > 0)
                    {
                        parts.Add(currentPart.ToString().ToLower());
                        currentPart.Clear();
                    }
                }
                else if (current == '_')
                {
                    // Snake_case boundary
                    if (currentPart.Length > 0)
                    {
                        parts.Add(currentPart.ToString().ToLower());
                        currentPart.Clear();
                    }

                    continue; // Skip the underscore
                }

                currentPart.Append(current);
            }

            if (currentPart.Length > 0)
            {
                parts.Add(currentPart.ToString().ToLower());
            }

            // If no splitting occurred, return the original identifier
            return parts.Count > 1 ? parts : new List<string> { identifier };
        }

        private string ReconstructTextFromTokens(List<string> tokens, string originalText)
        {
            // Simple reconstruction by finding token positions in original text
            // This is a basic implementation - a more sophisticated approach would
            // maintain position information during tokenization
            var result = new StringBuilder();
            var originalIndex = 0;

            foreach (var token in tokens)
            {
                // Find the token in the remaining original text
                var tokenIndex = originalText.IndexOf(token, originalIndex, StringComparison.OrdinalIgnoreCase);

                if (tokenIndex >= 0)
                {
                    // Add any text between the last position and this token
                    if (tokenIndex > originalIndex)
                    {
                        var intermediate = originalText.Substring(originalIndex, tokenIndex - originalIndex);
                        if (!string.IsNullOrWhiteSpace(intermediate))
                        {
                            result.Append(intermediate);
                        }
                    }

                    result.Append(originalText.Substring(tokenIndex, token.Length));
                    originalIndex = tokenIndex + token.Length;
                }
                else
                {
                    // If we can't find the token, just append it
                    result.Append(token);
                }
            }

            return result.ToString();
        }
    }
}