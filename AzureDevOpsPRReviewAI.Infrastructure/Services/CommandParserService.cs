namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using System.Text.RegularExpressions;
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;

    public class CommandParserService : ICommandParserService
    {
        private static readonly Regex CommandRegex = new(
            @"^/(?<command>\w+)(?:\s+(?<params>.+))?$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex ParameterRegex = new(
            @"(?<key>\w+)=(?<value>\S+)",
            RegexOptions.Compiled);

        public AIReviewCommand ParseComment(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return new AIReviewCommand { Command = string.Empty, IsValid = false };
            }

            // Look for command pattern in the comment (can be anywhere in the comment)
            var lines = comment.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                var match = CommandRegex.Match(trimmedLine);

                if (match.Success)
                {
                    var command = match.Groups["command"].Value.ToLowerInvariant();
                    var paramString = match.Groups["params"].Value;

                    var aiCommand = new AIReviewCommand
                    {
                        Command = command,
                        IsValid = IsValidCommand(command),
                    };

                    // Parse parameters if present
                    if (!string.IsNullOrWhiteSpace(paramString))
                    {
                        var paramMatches = ParameterRegex.Matches(paramString);
                        foreach (Match paramMatch in paramMatches)
                        {
                            var key = paramMatch.Groups["key"].Value.ToLowerInvariant();
                            var value = paramMatch.Groups["value"].Value;
                            aiCommand.Parameters[key] = value;
                        }
                    }

                    return aiCommand;
                }
            }

            return new AIReviewCommand { Command = string.Empty, IsValid = false };
        }

        private static bool IsValidCommand(string command)
        {
            return command switch
            {
                "run-ai-review" => true,
                "ai-review" => true,
                "review" => true,
                _ => false,
            };
        }
    }
}