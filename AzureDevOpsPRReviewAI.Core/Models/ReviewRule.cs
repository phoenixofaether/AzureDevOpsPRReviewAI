namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class ReviewRule
    {
        public required string Id { get; set; }

        public required string Name { get; set; }

        public string? Description { get; set; }

        public ReviewRuleType Type { get; set; }

        public bool IsEnabled { get; set; } = true;

        public ReviewSeverity MinimumSeverity { get; set; } = ReviewSeverity.Info;

        public ReviewSeverity MaximumSeverity { get; set; } = ReviewSeverity.Error;

        public List<string> FilePatterns { get; set; } = new();

        public List<string> ExcludeFilePatterns { get; set; } = new();

        public Dictionary<string, object> Parameters { get; set; } = new();

        public int Priority { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ReviewRuleType
    {
        CodeQuality,
        Security,
        Performance,
        Documentation,
        Testing,
        Architecture,
        Style,
        BestPractices,
        Custom
    }

}