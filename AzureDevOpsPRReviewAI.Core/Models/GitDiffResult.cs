namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class GitDiffResult
    {
        public bool IsSuccessful { get; set; }
        public List<FileDiff> ChangedFiles { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public int TotalLinesAdded { get; set; }
        public int TotalLinesRemoved { get; set; }
    }

    public class FileDiff
    {
        public string FilePath { get; set; } = string.Empty;
        public FileChangeType ChangeType { get; set; }
        public string? OldFilePath { get; set; }
        public List<DiffHunk> Hunks { get; set; } = new();
        public bool IsBinary { get; set; }
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
    }

    public class DiffHunk
    {
        public int OldStart { get; set; }
        public int OldLines { get; set; }
        public int NewStart { get; set; }
        public int NewLines { get; set; }
        public List<DiffLine> Lines { get; set; } = new();
    }

    public class DiffLine
    {
        public DiffLineType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public int? OldLineNumber { get; set; }
        public int? NewLineNumber { get; set; }
    }

    public enum DiffLineType
    {
        Context,
        Addition,
        Deletion
    }
}