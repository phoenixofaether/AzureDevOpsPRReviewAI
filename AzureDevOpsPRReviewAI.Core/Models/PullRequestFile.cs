namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class PullRequestFile
    {
        public required string FilePath { get; set; }

        public FileChangeType ChangeType { get; set; }

        public int AddedLines { get; set; }

        public int DeletedLines { get; set; }

        public string? DiffContent { get; set; }
    }
}