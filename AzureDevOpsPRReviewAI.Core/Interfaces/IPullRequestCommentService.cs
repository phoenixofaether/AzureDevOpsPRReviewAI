namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface IPullRequestCommentService
    {
        Task<CommentPostResult> PostCommentAsync(
            string organization,
            string project,
            string repository,
            int pullRequestId,
            FormattedComment comment,
            CancellationToken cancellationToken = default);

        Task<List<CommentPostResult>> PostCommentsAsync(
            string organization,
            string project,
            string repository,
            int pullRequestId,
            List<FormattedComment> comments,
            CancellationToken cancellationToken = default);

        Task<bool> DeleteCommentAsync(
            string organization,
            string project,
            string repository,
            int pullRequestId,
            string commentId,
            CancellationToken cancellationToken = default);

        Task<List<string>> GetExistingAICommentsAsync(
            string organization,
            string project,
            string repository,
            int pullRequestId,
            string requestId,
            CancellationToken cancellationToken = default);
    }
}