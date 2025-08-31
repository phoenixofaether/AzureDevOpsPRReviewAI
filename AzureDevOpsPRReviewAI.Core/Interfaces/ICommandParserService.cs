namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface ICommandParserService
    {
        AIReviewCommand ParseComment(string comment);
    }
}