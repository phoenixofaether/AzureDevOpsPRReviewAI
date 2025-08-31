namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface ICommentFormatterService
    {
        FormattedComment FormatReviewComment(ReviewComment comment, string requestId);

        List<FormattedComment> FormatAnalysisResults(CodeAnalysisResult analysisResult);

        string FormatSummaryComment(CodeAnalysisResult analysisResult);
    }
}