namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class FileContent
    {
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Encoding { get; set; } = "UTF-8";
        public bool IsBinary { get; set; }
        public long SizeInBytes { get; set; }
        public DateTime LastModified { get; set; }
        public string? Branch { get; set; }
        public string? CommitHash { get; set; }
    }

    public class FileFilterOptions
    {
        public List<string> ExcludePatterns { get; set; } = new();
        public List<string> IncludePatterns { get; set; } = new();
        public long MaxFileSizeBytes { get; set; } = 1024 * 1024; // 1MB default
        public bool ExcludeBinaryFiles { get; set; } = true;
        public List<string> ExcludeExtensions { get; set; } = new();
        public List<string> IncludeExtensions { get; set; } = new();
    }

    public class DiskSpaceInfo
    {
        public long TotalSpaceBytes { get; set; }
        public long FreeSpaceBytes { get; set; }
        public long UsedSpaceBytes => TotalSpaceBytes - FreeSpaceBytes;
        public double UsagePercentage => (double)UsedSpaceBytes / TotalSpaceBytes * 100;
        public long RepositoriesSpaceBytes { get; set; }
        public int RepositoryCount { get; set; }
    }
}