namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class RepositoryCloneResult
    {
        public bool IsSuccessful { get; set; }
        public string LocalPath { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public DateTime CloneTime { get; set; }
        public long SizeInBytes { get; set; }
        public string DefaultBranch { get; set; } = string.Empty;
        public List<string> AvailableBranches { get; set; } = new();
    }
}