namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    public interface IDependencyAnalysisService
    {
        Task<List<string>> AnalyzeFileDependenciesAsync(string repositoryPath, string filePath);
        Task<List<string>> FindRelatedFilesAsync(string repositoryPath, List<string> changedFiles);
    }
}