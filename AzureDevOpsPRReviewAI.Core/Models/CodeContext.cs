namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class CodeChunk
    {
        public string FilePath { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public CodeChunkType ChunkType { get; set; }
        public string? ClassName { get; set; }
        public string? MethodName { get; set; }
        public string? Namespace { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public int TokenCount { get; set; }
        public double RelevanceScore { get; set; }
    }

    public enum CodeChunkType
    {
        Class,
        Method,
        Property,
        Field,
        Interface,
        Enum,
        Namespace,
        UsingDirectives,
        Comment,
        Other
    }

    public class ContextResult
    {
        public List<CodeChunk> RelevantChunks { get; set; } = new();
        public List<FileContent> RelevantFiles { get; set; } = new();
        public GitDiffResult PrimaryDiff { get; set; } = new();
        public int TotalTokens { get; set; }
        public bool IsContextTruncated { get; set; }
        public string TruncationReason { get; set; } = string.Empty;
        public List<string> RelatedFiles { get; set; } = new();
    }

    public class PullRequestAnalysisRequest
    {
        public string RepositoryPath { get; set; } = string.Empty;
        public string SourceBranch { get; set; } = string.Empty;
        public string TargetBranch { get; set; } = string.Empty;
        public List<string> ChangedFiles { get; set; } = new();
        public int MaxContextTokens { get; set; } = 100000;
        public AIReviewCommand Command { get; set; } = new() { Command = "review" };
    }

    public class SemanticSearchResult
    {
        public List<CodeChunk> Results { get; set; } = new();
        public string Query { get; set; } = string.Empty;
        public int TotalResults { get; set; }
        public double MaxScore { get; set; }
        public double MinScore { get; set; }
    }

    public class CodeChunkEmbedding
    {
        public string Id { get; set; } = string.Empty;
        public string RepositoryPath { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public CodeChunk Chunk { get; set; } = new();
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
    }

    public class SimilarityResult
    {
        public CodeChunk CodeChunk { get; set; } = new();
        public double SimilarityScore { get; set; }
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}