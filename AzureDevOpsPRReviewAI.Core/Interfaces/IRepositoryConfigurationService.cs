namespace AzureDevOpsPRReviewAI.Core.Interfaces
{
    using AzureDevOpsPRReviewAI.Core.Models;

    public interface IRepositoryConfigurationService
    {
        /// <summary>
        /// Gets the configuration for a specific repository
        /// </summary>
        Task<RepositoryConfiguration?> GetConfigurationAsync(string organization, string project, string repository);

        /// <summary>
        /// Gets all configurations for an organization
        /// </summary>
        Task<List<RepositoryConfiguration>> GetOrganizationConfigurationsAsync(string organization);

        /// <summary>
        /// Gets all configurations for a project
        /// </summary>
        Task<List<RepositoryConfiguration>> GetProjectConfigurationsAsync(string organization, string project);

        /// <summary>
        /// Creates or updates a repository configuration
        /// </summary>
        Task<RepositoryConfiguration> SaveConfigurationAsync(RepositoryConfiguration configuration);

        /// <summary>
        /// Deletes a repository configuration
        /// </summary>
        Task<bool> DeleteConfigurationAsync(string organization, string project, string repository);

        /// <summary>
        /// Checks if a repository has a custom configuration
        /// </summary>
        Task<bool> HasConfigurationAsync(string organization, string project, string repository);

        /// <summary>
        /// Gets the effective configuration for a repository (includes inheritance)
        /// </summary>
        Task<RepositoryConfiguration> GetEffectiveConfigurationAsync(string organization, string project, string repository);

        /// <summary>
        /// Validates a repository configuration
        /// </summary>
        Task<ConfigurationValidationResult> ValidateConfigurationAsync(RepositoryConfiguration configuration);

        /// <summary>
        /// Gets configuration by ID
        /// </summary>
        Task<RepositoryConfiguration?> GetConfigurationByIdAsync(string configurationId);

        /// <summary>
        /// Creates a default configuration for a repository
        /// </summary>
        Task<RepositoryConfiguration> CreateDefaultConfigurationAsync(string organization, string project, string repository, string? createdBy = null);

        /// <summary>
        /// Clones configuration from one repository to another
        /// </summary>
        Task<RepositoryConfiguration> CloneConfigurationAsync(string sourceOrganization, string sourceProject, string sourceRepository, string targetOrganization, string targetProject, string targetRepository, string? createdBy = null);

        /// <summary>
        /// Gets configuration history/versions
        /// </summary>
        Task<List<RepositoryConfiguration>> GetConfigurationHistoryAsync(string organization, string project, string repository);

        /// <summary>
        /// Exports configuration as JSON
        /// </summary>
        Task<string> ExportConfigurationAsync(string organization, string project, string repository);

        /// <summary>
        /// Imports configuration from JSON
        /// </summary>
        Task<RepositoryConfiguration> ImportConfigurationAsync(string configurationJson, string? updatedBy = null);
    }
}