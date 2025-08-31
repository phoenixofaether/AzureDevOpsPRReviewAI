namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    public enum AuthenticationType
    {
        PersonalAccessToken,
        EntraIdOAuth,
    }

    public interface IAuthenticationService
    {
        Task<string?> GetAccessTokenAsync();

        Task<bool> IsAuthenticatedAsync();

        AuthenticationType GetAuthenticationType();
    }
}