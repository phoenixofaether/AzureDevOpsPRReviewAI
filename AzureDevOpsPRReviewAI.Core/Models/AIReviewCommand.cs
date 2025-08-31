namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class AIReviewCommand
    {
        public required string Command { get; set; }

        public Dictionary<string, string> Parameters { get; set; } = new();

        public bool IsValid { get; set; }
    }
}