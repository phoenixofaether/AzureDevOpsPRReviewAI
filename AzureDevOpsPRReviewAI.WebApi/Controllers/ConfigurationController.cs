namespace AzureDevOpsPRReviewAI.WebApi.Controllers
{
    using System.ComponentModel.DataAnnotations;
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using AzureDevOpsPRReviewAI.WebApi.Attributes;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly IRepositoryConfigurationService configurationService;
        private readonly ILogger<ConfigurationController> logger;

        public ConfigurationController(
            IRepositoryConfigurationService configurationService,
            ILogger<ConfigurationController> logger)
        {
            this.configurationService = configurationService;
            this.logger = logger;
        }

        /// <summary>
        /// Gets the configuration for a specific repository
        /// </summary>
        /// <param name="organization">Organization name</param>
        /// <param name="project">Project name</param>
        /// <param name="repository">Repository name</param>
        /// <returns>Repository configuration</returns>
        [HttpGet("{organization}/{project}/{repository}")]
        [RequirePermission(Permissions.RepositoryConfigView)]
        public async Task<ActionResult<RepositoryConfiguration>> GetConfiguration(
            [FromRoute, Required] string organization,
            [FromRoute, Required] string project,
            [FromRoute, Required] string repository)
        {
            try
            {
                this.logger.LogDebug("Getting configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                
                var configuration = await this.configurationService.GetConfigurationAsync(organization, project, repository);
                
                if (configuration == null)
                {
                    this.logger.LogInformation("Configuration not found for {Organization}/{Project}/{Repository}", organization, project, repository);
                    return this.NotFound($"Configuration not found for {organization}/{project}/{repository}");
                }

                return this.Ok(configuration);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets the effective configuration for a repository (includes defaults)
        /// </summary>
        /// <param name="organization">Organization name</param>
        /// <param name="project">Project name</param>
        /// <param name="repository">Repository name</param>
        /// <returns>Effective repository configuration</returns>
        [HttpGet("{organization}/{project}/{repository}/effective")]
        [RequirePermission(Permissions.RepositoryConfigView)]
        public async Task<ActionResult<RepositoryConfiguration>> GetEffectiveConfiguration(
            [FromRoute, Required] string organization,
            [FromRoute, Required] string project,
            [FromRoute, Required] string repository)
        {
            try
            {
                this.logger.LogDebug("Getting effective configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                
                var configuration = await this.configurationService.GetEffectiveConfigurationAsync(organization, project, repository);
                return this.Ok(configuration);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get effective configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets all configurations for an organization
        /// </summary>
        /// <param name="organization">Organization name</param>
        /// <returns>List of repository configurations</returns>
        [HttpGet("organization/{organization}")]
        [RequirePermission(Permissions.OrganizationConfigView)]
        public async Task<ActionResult<List<RepositoryConfiguration>>> GetOrganizationConfigurations(
            [FromRoute, Required] string organization)
        {
            try
            {
                this.logger.LogDebug("Getting configurations for organization {Organization}", organization);
                
                var configurations = await this.configurationService.GetOrganizationConfigurationsAsync(organization);
                return this.Ok(configurations);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get configurations for organization {Organization}", organization);
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Gets all configurations for a project
        /// </summary>
        /// <param name="organization">Organization name</param>
        /// <param name="project">Project name</param>
        /// <returns>List of repository configurations</returns>
        [HttpGet("project/{organization}/{project}")]
        [RequirePermission(Permissions.ProjectConfigView)]
        public async Task<ActionResult<List<RepositoryConfiguration>>> GetProjectConfigurations(
            [FromRoute, Required] string organization,
            [FromRoute, Required] string project)
        {
            try
            {
                this.logger.LogDebug("Getting configurations for project {Organization}/{Project}", organization, project);
                
                var configurations = await this.configurationService.GetProjectConfigurationsAsync(organization, project);
                return this.Ok(configurations);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get configurations for project {Organization}/{Project}", organization, project);
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Creates or updates a repository configuration
        /// </summary>
        /// <param name="configuration">Repository configuration to save</param>
        /// <returns>Saved repository configuration</returns>
        [HttpPost]
        [RequirePermission(Permissions.RepositoryConfigEdit)]
        public async Task<ActionResult<RepositoryConfiguration>> SaveConfiguration(
            [FromBody, Required] RepositoryConfiguration configuration)
        {
            try
            {
                this.logger.LogDebug("Saving configuration for {Organization}/{Project}/{Repository}", 
                    configuration.Organization, configuration.Project, configuration.Repository);
                
                // Validate configuration
                var validationResult = await this.configurationService.ValidateConfigurationAsync(configuration);
                if (!validationResult.IsValid)
                {
                    var errors = validationResult.Errors.Select(e => new
                    {
                        Message = e.Message,
                        PropertyPath = e.PropertyPath,
                        ErrorCode = e.ErrorCode
                    });
                    
                    return this.BadRequest(new
                    {
                        Message = "Configuration validation failed",
                        Errors = errors,
                        Warnings = validationResult.Warnings.Select(w => new
                        {
                            Message = w.Message,
                            PropertyPath = w.PropertyPath,
                            WarningCode = w.WarningCode
                        })
                    });
                }

                var savedConfiguration = await this.configurationService.SaveConfigurationAsync(configuration);
                
                this.logger.LogInformation("Successfully saved configuration for {Organization}/{Project}/{Repository}", 
                    savedConfiguration.Organization, savedConfiguration.Project, savedConfiguration.Repository);
                
                return this.Ok(savedConfiguration);
            }
            catch (ArgumentException ex)
            {
                this.logger.LogWarning(ex, "Invalid configuration data");
                return this.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to save configuration");
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Updates a specific repository configuration
        /// </summary>
        /// <param name="organization">Organization name</param>
        /// <param name="project">Project name</param>
        /// <param name="repository">Repository name</param>
        /// <param name="configuration">Updated repository configuration</param>
        /// <returns>Updated repository configuration</returns>
        [HttpPut("{organization}/{project}/{repository}")]
        [RequirePermission(Permissions.RepositoryConfigEdit)]
        public async Task<ActionResult<RepositoryConfiguration>> UpdateConfiguration(
            [FromRoute, Required] string organization,
            [FromRoute, Required] string project,
            [FromRoute, Required] string repository,
            [FromBody, Required] RepositoryConfiguration configuration)
        {
            try
            {
                // Ensure the route parameters match the configuration
                if (!configuration.Organization.Equals(organization, StringComparison.OrdinalIgnoreCase) ||
                    !configuration.Project.Equals(project, StringComparison.OrdinalIgnoreCase) ||
                    !configuration.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase))
                {
                    return this.BadRequest("Organization, project, or repository in the request body does not match the route parameters");
                }

                this.logger.LogDebug("Updating configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                
                var savedConfiguration = await this.configurationService.SaveConfigurationAsync(configuration);
                
                this.logger.LogInformation("Successfully updated configuration for {Organization}/{Project}/{Repository}", 
                    organization, project, repository);
                
                return this.Ok(savedConfiguration);
            }
            catch (ArgumentException ex)
            {
                this.logger.LogWarning(ex, "Invalid configuration data for {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to update configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Deletes a repository configuration
        /// </summary>
        /// <param name="organization">Organization name</param>
        /// <param name="project">Project name</param>
        /// <param name="repository">Repository name</param>
        /// <returns>Success or failure result</returns>
        [HttpDelete("{organization}/{project}/{repository}")]
        [RequirePermission(Permissions.RepositoryConfigEdit)]
        public async Task<ActionResult> DeleteConfiguration(
            [FromRoute, Required] string organization,
            [FromRoute, Required] string project,
            [FromRoute, Required] string repository)
        {
            try
            {
                this.logger.LogDebug("Deleting configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                
                var deleted = await this.configurationService.DeleteConfigurationAsync(organization, project, repository);
                
                if (!deleted)
                {
                    this.logger.LogInformation("Configuration not found for deletion: {Organization}/{Project}/{Repository}", organization, project, repository);
                    return this.NotFound($"Configuration not found for {organization}/{project}/{repository}");
                }

                this.logger.LogInformation("Successfully deleted configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.NoContent();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to delete configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Creates a default configuration for a repository
        /// </summary>
        /// <param name="organization">Organization name</param>
        /// <param name="project">Project name</param>
        /// <param name="repository">Repository name</param>
        /// <returns>Default repository configuration</returns>
        [HttpPost("{organization}/{project}/{repository}/default")]
        [RequirePermission(Permissions.RepositoryConfigEdit)]
        public async Task<ActionResult<RepositoryConfiguration>> CreateDefaultConfiguration(
            [FromRoute, Required] string organization,
            [FromRoute, Required] string project,
            [FromRoute, Required] string repository)
        {
            try
            {
                this.logger.LogDebug("Creating default configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                
                // Check if configuration already exists
                var existingConfig = await this.configurationService.GetConfigurationAsync(organization, project, repository);
                if (existingConfig != null)
                {
                    return this.Conflict($"Configuration already exists for {organization}/{project}/{repository}");
                }

                var defaultConfiguration = await this.configurationService.CreateDefaultConfigurationAsync(organization, project, repository, "API");
                
                this.logger.LogInformation("Successfully created default configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.Created($"/api/configuration/{organization}/{project}/{repository}", defaultConfiguration);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to create default configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Clones configuration from one repository to another
        /// </summary>
        /// <param name="cloneRequest">Clone configuration request</param>
        /// <returns>Cloned repository configuration</returns>
        [HttpPost("clone")]
        [RequirePermission(Permissions.ConfigurationClone)]
        public async Task<ActionResult<RepositoryConfiguration>> CloneConfiguration(
            [FromBody, Required] CloneConfigurationRequest cloneRequest)
        {
            try
            {
                this.logger.LogDebug("Cloning configuration from {SourceOrg}/{SourceProject}/{SourceRepo} to {TargetOrg}/{TargetProject}/{TargetRepo}", 
                    cloneRequest.SourceOrganization, cloneRequest.SourceProject, cloneRequest.SourceRepository,
                    cloneRequest.TargetOrganization, cloneRequest.TargetProject, cloneRequest.TargetRepository);
                
                // Check if target configuration already exists
                var existingConfig = await this.configurationService.GetConfigurationAsync(
                    cloneRequest.TargetOrganization, cloneRequest.TargetProject, cloneRequest.TargetRepository);
                
                if (existingConfig != null && !cloneRequest.OverwriteExisting)
                {
                    return this.Conflict($"Configuration already exists for {cloneRequest.TargetOrganization}/{cloneRequest.TargetProject}/{cloneRequest.TargetRepository}. Set OverwriteExisting=true to overwrite.");
                }

                var clonedConfiguration = await this.configurationService.CloneConfigurationAsync(
                    cloneRequest.SourceOrganization, cloneRequest.SourceProject, cloneRequest.SourceRepository,
                    cloneRequest.TargetOrganization, cloneRequest.TargetProject, cloneRequest.TargetRepository,
                    "API");
                
                this.logger.LogInformation("Successfully cloned configuration to {TargetOrg}/{TargetProject}/{TargetRepo}", 
                    cloneRequest.TargetOrganization, cloneRequest.TargetProject, cloneRequest.TargetRepository);
                
                return this.Created($"/api/configuration/{cloneRequest.TargetOrganization}/{cloneRequest.TargetProject}/{cloneRequest.TargetRepository}", clonedConfiguration);
            }
            catch (ArgumentException ex)
            {
                this.logger.LogWarning(ex, "Invalid clone configuration request");
                return this.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to clone configuration");
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Validates a repository configuration
        /// </summary>
        /// <param name="configuration">Configuration to validate</param>
        /// <returns>Validation result</returns>
        [HttpPost("validate")]
        [RequirePermission(Permissions.RepositoryConfigView)]
        public async Task<ActionResult<ConfigurationValidationResult>> ValidateConfiguration(
            [FromBody, Required] RepositoryConfiguration configuration)
        {
            try
            {
                this.logger.LogDebug("Validating configuration for {Organization}/{Project}/{Repository}", 
                    configuration.Organization, configuration.Project, configuration.Repository);
                
                var validationResult = await this.configurationService.ValidateConfigurationAsync(configuration);
                return this.Ok(validationResult);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to validate configuration");
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Exports a repository configuration as JSON
        /// </summary>
        /// <param name="organization">Organization name</param>
        /// <param name="project">Project name</param>
        /// <param name="repository">Repository name</param>
        /// <returns>Configuration JSON</returns>
        [HttpGet("{organization}/{project}/{repository}/export")]
        [RequirePermission(Permissions.ConfigurationExport)]
        public async Task<ActionResult> ExportConfiguration(
            [FromRoute, Required] string organization,
            [FromRoute, Required] string project,
            [FromRoute, Required] string repository)
        {
            try
            {
                this.logger.LogDebug("Exporting configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                
                var configurationJson = await this.configurationService.ExportConfigurationAsync(organization, project, repository);
                
                var fileName = $"{organization}_{project}_{repository}_config.json";
                return this.File(System.Text.Encoding.UTF8.GetBytes(configurationJson), "application/json", fileName);
            }
            catch (ArgumentException ex)
            {
                this.logger.LogWarning(ex, "Configuration not found for export: {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to export configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return this.StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Imports a repository configuration from JSON
        /// </summary>
        /// <param name="importRequest">Import configuration request</param>
        /// <returns>Imported repository configuration</returns>
        [HttpPost("import")]
        [RequirePermission(Permissions.ConfigurationImport)]
        public async Task<ActionResult<RepositoryConfiguration>> ImportConfiguration(
            [FromBody, Required] ImportConfigurationRequest importRequest)
        {
            try
            {
                this.logger.LogDebug("Importing configuration from JSON");
                
                var importedConfiguration = await this.configurationService.ImportConfigurationAsync(importRequest.ConfigurationJson, "API");
                
                this.logger.LogInformation("Successfully imported configuration for {Organization}/{Project}/{Repository}", 
                    importedConfiguration.Organization, importedConfiguration.Project, importedConfiguration.Repository);
                
                return this.Ok(importedConfiguration);
            }
            catch (ArgumentException ex)
            {
                this.logger.LogWarning(ex, "Invalid configuration JSON for import");
                return this.BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to import configuration");
                return this.StatusCode(500, "Internal server error");
            }
        }
    }

    /// <summary>
    /// Request model for cloning configuration
    /// </summary>
    public class CloneConfigurationRequest
    {
        [Required]
        public required string SourceOrganization { get; set; }

        [Required]
        public required string SourceProject { get; set; }

        [Required]
        public required string SourceRepository { get; set; }

        [Required]
        public required string TargetOrganization { get; set; }

        [Required]
        public required string TargetProject { get; set; }

        [Required]
        public required string TargetRepository { get; set; }

        public bool OverwriteExisting { get; set; } = false;
    }

    /// <summary>
    /// Request model for importing configuration
    /// </summary>
    public class ImportConfigurationRequest
    {
        [Required]
        public required string ConfigurationJson { get; set; }
    }
}