namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class RepositoryConfigurationService : IRepositoryConfigurationService
    {
        private readonly ILogger<RepositoryConfigurationService> logger;
        private readonly string configurationStoragePath;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        public RepositoryConfigurationService(ILogger<RepositoryConfigurationService> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configurationStoragePath = configuration["RepositoryConfiguration:StoragePath"] ?? "./config";
            
            this.jsonSerializerOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            this.EnsureStorageDirectoryExists();
        }

        public async Task<RepositoryConfiguration?> GetConfigurationAsync(string organization, string project, string repository)
        {
            try
            {
                var filePath = this.GetConfigurationFilePath(organization, project, repository);
                
                if (!File.Exists(filePath))
                {
                    this.logger.LogDebug("Configuration file not found: {FilePath}", filePath);
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var configuration = JsonSerializer.Deserialize<RepositoryConfiguration>(json, this.jsonSerializerOptions);
                
                this.logger.LogDebug("Loaded configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return configuration;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return null;
            }
        }

        public async Task<List<RepositoryConfiguration>> GetOrganizationConfigurationsAsync(string organization)
        {
            try
            {
                var configurations = new List<RepositoryConfiguration>();
                var orgPath = Path.Combine(this.configurationStoragePath, organization);
                
                if (!Directory.Exists(orgPath))
                {
                    return configurations;
                }

                var configFiles = Directory.GetFiles(orgPath, "*.json", SearchOption.AllDirectories);
                
                foreach (var filePath in configFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var configuration = JsonSerializer.Deserialize<RepositoryConfiguration>(json, this.jsonSerializerOptions);
                        
                        if (configuration != null && configuration.Organization.Equals(organization, StringComparison.OrdinalIgnoreCase))
                        {
                            configurations.Add(configuration);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Failed to load configuration from file: {FilePath}", filePath);
                    }
                }

                this.logger.LogDebug("Loaded {Count} configurations for organization {Organization}", configurations.Count, organization);
                return configurations;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load configurations for organization {Organization}", organization);
                return new List<RepositoryConfiguration>();
            }
        }

        public async Task<List<RepositoryConfiguration>> GetProjectConfigurationsAsync(string organization, string project)
        {
            try
            {
                var configurations = new List<RepositoryConfiguration>();
                var projectPath = Path.Combine(this.configurationStoragePath, organization, project);
                
                if (!Directory.Exists(projectPath))
                {
                    return configurations;
                }

                var configFiles = Directory.GetFiles(projectPath, "*.json", SearchOption.AllDirectories);
                
                foreach (var filePath in configFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var configuration = JsonSerializer.Deserialize<RepositoryConfiguration>(json, this.jsonSerializerOptions);
                        
                        if (configuration != null && 
                            configuration.Organization.Equals(organization, StringComparison.OrdinalIgnoreCase) &&
                            configuration.Project.Equals(project, StringComparison.OrdinalIgnoreCase))
                        {
                            configurations.Add(configuration);
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Failed to load configuration from file: {FilePath}", filePath);
                    }
                }

                this.logger.LogDebug("Loaded {Count} configurations for project {Organization}/{Project}", configurations.Count, organization, project);
                return configurations;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to load configurations for project {Organization}/{Project}", organization, project);
                return new List<RepositoryConfiguration>();
            }
        }

        public async Task<RepositoryConfiguration> SaveConfigurationAsync(RepositoryConfiguration configuration)
        {
            try
            {
                // Validate configuration before saving
                var validationResult = await this.ValidateConfigurationAsync(configuration);
                if (!validationResult.IsValid)
                {
                    var errorMessages = string.Join(", ", validationResult.Errors.Select(e => e.Message));
                    throw new ArgumentException($"Configuration validation failed: {errorMessages}");
                }

                // Update metadata
                if (string.IsNullOrEmpty(configuration.Id))
                {
                    configuration.Id = Guid.NewGuid().ToString();
                }
                
                configuration.UpdatedAt = DateTime.UtcNow;
                configuration.Version++;

                // Ensure directory exists
                var filePath = this.GetConfigurationFilePath(configuration.Organization, configuration.Project, configuration.Repository);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save to file
                var json = JsonSerializer.Serialize(configuration, this.jsonSerializerOptions);
                await File.WriteAllTextAsync(filePath, json);

                this.logger.LogInformation("Saved configuration for {Organization}/{Project}/{Repository}", 
                    configuration.Organization, configuration.Project, configuration.Repository);
                
                return configuration;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to save configuration for {Organization}/{Project}/{Repository}", 
                    configuration.Organization, configuration.Project, configuration.Repository);
                throw;
            }
        }

        public async Task<bool> DeleteConfigurationAsync(string organization, string project, string repository)
        {
            try
            {
                var filePath = this.GetConfigurationFilePath(organization, project, repository);
                
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    this.logger.LogInformation("Deleted configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to delete configuration for {Organization}/{Project}/{Repository}", organization, project, repository);
                return false;
            }
        }

        public async Task<bool> HasConfigurationAsync(string organization, string project, string repository)
        {
            var filePath = this.GetConfigurationFilePath(organization, project, repository);
            return File.Exists(filePath);
        }

        public async Task<RepositoryConfiguration> GetEffectiveConfigurationAsync(string organization, string project, string repository)
        {
            // Try to get repository-specific configuration first
            var configuration = await this.GetConfigurationAsync(organization, project, repository);
            
            if (configuration != null)
            {
                return configuration;
            }

            // If no repository-specific configuration, create default
            return await this.CreateDefaultConfigurationAsync(organization, project, repository);
        }

        public async Task<ConfigurationValidationResult> ValidateConfigurationAsync(RepositoryConfiguration configuration)
        {
            var result = new ConfigurationValidationResult { IsValid = true };

            try
            {
                // Required field validation
                if (string.IsNullOrWhiteSpace(configuration.Organization))
                {
                    result.Errors.Add(new ConfigurationValidationError { Message = "Organization is required", PropertyPath = nameof(configuration.Organization) });
                }

                if (string.IsNullOrWhiteSpace(configuration.Project))
                {
                    result.Errors.Add(new ConfigurationValidationError { Message = "Project is required", PropertyPath = nameof(configuration.Project) });
                }

                if (string.IsNullOrWhiteSpace(configuration.Repository))
                {
                    result.Errors.Add(new ConfigurationValidationError { Message = "Repository is required", PropertyPath = nameof(configuration.Repository) });
                }

                // Validate review rules
                foreach (var rule in configuration.ReviewRules)
                {
                    if (string.IsNullOrWhiteSpace(rule.Name))
                    {
                        result.Errors.Add(new ConfigurationValidationError { Message = "Review rule name is required", PropertyPath = $"ReviewRules.{rule.Id}.Name" });
                    }

                    if (rule.MinimumSeverity > rule.MaximumSeverity)
                    {
                        result.Warnings.Add(new ConfigurationValidationWarning { Message = "Minimum severity is higher than maximum severity", PropertyPath = $"ReviewRules.{rule.Id}" });
                    }
                }

                // Validate file exclusion rules
                foreach (var exclusionRule in configuration.FileExclusionRules)
                {
                    if (string.IsNullOrWhiteSpace(exclusionRule.Pattern))
                    {
                        result.Errors.Add(new ConfigurationValidationError { Message = "Exclusion rule pattern is required", PropertyPath = $"FileExclusionRules.{exclusionRule.Id}.Pattern" });
                    }

                    // Validate regex patterns
                    if (exclusionRule.Type == ExclusionType.Regex)
                    {
                        try
                        {
                            var regex = new Regex(exclusionRule.Pattern);
                        }
                        catch (ArgumentException)
                        {
                            result.Errors.Add(new ConfigurationValidationError { Message = "Invalid regex pattern", PropertyPath = $"FileExclusionRules.{exclusionRule.Id}.Pattern", Value = exclusionRule.Pattern });
                        }
                    }
                }

                // Validate custom prompts
                foreach (var prompt in configuration.CustomPrompts)
                {
                    if (string.IsNullOrWhiteSpace(prompt.Name))
                    {
                        result.Errors.Add(new ConfigurationValidationError { Message = "Custom prompt name is required", PropertyPath = $"CustomPrompts.{prompt.Id}.Name" });
                    }

                    if (string.IsNullOrWhiteSpace(prompt.Template))
                    {
                        result.Errors.Add(new ConfigurationValidationError { Message = "Custom prompt template is required", PropertyPath = $"CustomPrompts.{prompt.Id}.Template" });
                    }
                }

                // Validate webhook settings
                if (configuration.WebhookSettings.MaxFilesForAutoReview <= 0)
                {
                    result.Warnings.Add(new ConfigurationValidationWarning { Message = "MaxFilesForAutoReview should be greater than 0", PropertyPath = "WebhookSettings.MaxFilesForAutoReview" });
                }

                if (configuration.WebhookSettings.MaxDiffSizeBytes <= 0)
                {
                    result.Warnings.Add(new ConfigurationValidationWarning { Message = "MaxDiffSizeBytes should be greater than 0", PropertyPath = "WebhookSettings.MaxDiffSizeBytes" });
                }

                // Validate comment settings
                if (configuration.CommentSettings.MaxCommentsPerFile <= 0)
                {
                    result.Warnings.Add(new ConfigurationValidationWarning { Message = "MaxCommentsPerFile should be greater than 0", PropertyPath = "CommentSettings.MaxCommentsPerFile" });
                }

                result.IsValid = !result.Errors.Any();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error during configuration validation");
                result.Errors.Add(new ConfigurationValidationError { Message = $"Validation error: {ex.Message}" });
                result.IsValid = false;
            }

            return result;
        }

        public async Task<RepositoryConfiguration?> GetConfigurationByIdAsync(string configurationId)
        {
            try
            {
                var configFiles = Directory.GetFiles(this.configurationStoragePath, "*.json", SearchOption.AllDirectories);
                
                foreach (var filePath in configFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(filePath);
                        var configuration = JsonSerializer.Deserialize<RepositoryConfiguration>(json, this.jsonSerializerOptions);
                        
                        if (configuration?.Id == configurationId)
                        {
                            return configuration;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Failed to load configuration from file: {FilePath}", filePath);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to find configuration by ID: {ConfigurationId}", configurationId);
                return null;
            }
        }

        public async Task<RepositoryConfiguration> CreateDefaultConfigurationAsync(string organization, string project, string repository, string? createdBy = null)
        {
            var configuration = new RepositoryConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Organization = organization,
                Project = project,
                Repository = repository,
                IsEnabled = true,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
                ReviewRules = this.CreateDefaultReviewRules(),
                FileExclusionRules = this.CreateDefaultFileExclusionRules(),
                CustomPrompts = new List<CustomPrompt>(),
                WebhookSettings = new WebhookSettings(),
                CommentSettings = new CommentSettings()
            };

            return await this.SaveConfigurationAsync(configuration);
        }

        public async Task<RepositoryConfiguration> CloneConfigurationAsync(string sourceOrganization, string sourceProject, string sourceRepository, string targetOrganization, string targetProject, string targetRepository, string? createdBy = null)
        {
            var sourceConfiguration = await this.GetConfigurationAsync(sourceOrganization, sourceProject, sourceRepository);
            
            if (sourceConfiguration == null)
            {
                throw new ArgumentException($"Source configuration not found: {sourceOrganization}/{sourceProject}/{sourceRepository}");
            }

            var clonedConfiguration = new RepositoryConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                Organization = targetOrganization,
                Project = targetProject,
                Repository = targetRepository,
                IsEnabled = sourceConfiguration.IsEnabled,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
                ReviewRules = sourceConfiguration.ReviewRules.Select(r => new ReviewRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = r.Name,
                    Description = r.Description,
                    Type = r.Type,
                    IsEnabled = r.IsEnabled,
                    MinimumSeverity = r.MinimumSeverity,
                    MaximumSeverity = r.MaximumSeverity,
                    FilePatterns = new List<string>(r.FilePatterns),
                    ExcludeFilePatterns = new List<string>(r.ExcludeFilePatterns),
                    Parameters = new Dictionary<string, object>(r.Parameters),
                    Priority = r.Priority
                }).ToList(),
                FileExclusionRules = sourceConfiguration.FileExclusionRules.Select(e => new FileExclusionRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = e.Name,
                    Description = e.Description,
                    Pattern = e.Pattern,
                    Type = e.Type,
                    IsEnabled = e.IsEnabled,
                    CaseSensitive = e.CaseSensitive,
                    MaxFileSizeBytes = e.MaxFileSizeBytes,
                    FileExtensions = new List<string>(e.FileExtensions)
                }).ToList(),
                CustomPrompts = sourceConfiguration.CustomPrompts.Select(p => new CustomPrompt
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = p.Name,
                    Description = p.Description,
                    Type = p.Type,
                    Template = p.Template,
                    IsEnabled = p.IsEnabled,
                    SupportedLanguages = new List<string>(p.SupportedLanguages),
                    SupportedFileExtensions = new List<string>(p.SupportedFileExtensions),
                    Variables = new Dictionary<string, string>(p.Variables),
                    Scope = p.Scope,
                    Priority = p.Priority,
                    CreatedBy = createdBy
                }).ToList(),
                WebhookSettings = new WebhookSettings
                {
                    AutoReviewOnCreate = sourceConfiguration.WebhookSettings.AutoReviewOnCreate,
                    AutoReviewOnUpdate = sourceConfiguration.WebhookSettings.AutoReviewOnUpdate,
                    RequireCommentTrigger = sourceConfiguration.WebhookSettings.RequireCommentTrigger,
                    AllowedTriggerUsers = new List<string>(sourceConfiguration.WebhookSettings.AllowedTriggerUsers),
                    MaxFilesForAutoReview = sourceConfiguration.WebhookSettings.MaxFilesForAutoReview,
                    MaxDiffSizeBytes = sourceConfiguration.WebhookSettings.MaxDiffSizeBytes
                },
                CommentSettings = new CommentSettings
                {
                    EnableLineComments = sourceConfiguration.CommentSettings.EnableLineComments,
                    EnableSummaryComment = sourceConfiguration.CommentSettings.EnableSummaryComment,
                    GroupSimilarIssues = sourceConfiguration.CommentSettings.GroupSimilarIssues,
                    IncludeConfidenceScore = sourceConfiguration.CommentSettings.IncludeConfidenceScore,
                    CommentPrefix = sourceConfiguration.CommentSettings.CommentPrefix,
                    EnableReplyToComments = sourceConfiguration.CommentSettings.EnableReplyToComments,
                    MaxCommentsPerFile = sourceConfiguration.CommentSettings.MaxCommentsPerFile
                }
            };

            return await this.SaveConfigurationAsync(clonedConfiguration);
        }

        public async Task<List<RepositoryConfiguration>> GetConfigurationHistoryAsync(string organization, string project, string repository)
        {
            // For file-based storage, we don't maintain history
            // In the future, this could be implemented by storing versioned files
            var configuration = await this.GetConfigurationAsync(organization, project, repository);
            return configuration != null ? new List<RepositoryConfiguration> { configuration } : new List<RepositoryConfiguration>();
        }

        public async Task<string> ExportConfigurationAsync(string organization, string project, string repository)
        {
            var configuration = await this.GetConfigurationAsync(organization, project, repository);
            
            if (configuration == null)
            {
                throw new ArgumentException($"Configuration not found: {organization}/{project}/{repository}");
            }

            return JsonSerializer.Serialize(configuration, this.jsonSerializerOptions);
        }

        public async Task<RepositoryConfiguration> ImportConfigurationAsync(string configurationJson, string? updatedBy = null)
        {
            try
            {
                var configuration = JsonSerializer.Deserialize<RepositoryConfiguration>(configurationJson, this.jsonSerializerOptions);
                
                if (configuration == null)
                {
                    throw new ArgumentException("Invalid configuration JSON");
                }

                configuration.UpdatedBy = updatedBy;
                return await this.SaveConfigurationAsync(configuration);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Invalid JSON format: {ex.Message}", ex);
            }
        }

        private string GetConfigurationFilePath(string organization, string project, string repository)
        {
            var sanitizedOrg = this.SanitizeFileName(organization);
            var sanitizedProject = this.SanitizeFileName(project);
            var sanitizedRepo = this.SanitizeFileName(repository);
            
            return Path.Combine(this.configurationStoragePath, sanitizedOrg, sanitizedProject, $"{sanitizedRepo}.json");
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        private void EnsureStorageDirectoryExists()
        {
            if (!Directory.Exists(this.configurationStoragePath))
            {
                Directory.CreateDirectory(this.configurationStoragePath);
                this.logger.LogInformation("Created configuration storage directory: {Path}", this.configurationStoragePath);
            }
        }

        private List<ReviewRule> CreateDefaultReviewRules()
        {
            return new List<ReviewRule>
            {
                new ReviewRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Code Quality",
                    Description = "General code quality and best practices review",
                    Type = ReviewRuleType.CodeQuality,
                    IsEnabled = true,
                    MinimumSeverity = ReviewSeverity.Info,
                    MaximumSeverity = ReviewSeverity.Error,
                    FilePatterns = new List<string> { "**/*.cs", "**/*.js", "**/*.ts", "**/*.py" },
                    Priority = 1
                },
                new ReviewRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Security",
                    Description = "Security vulnerability and best practices review",
                    Type = ReviewRuleType.Security,
                    IsEnabled = true,
                    MinimumSeverity = ReviewSeverity.Warning,
                    MaximumSeverity = ReviewSeverity.Critical,
                    FilePatterns = new List<string> { "**/*.cs", "**/*.js", "**/*.ts", "**/*.py", "**/*.java" },
                    Priority = 0
                },
                new ReviewRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Performance",
                    Description = "Performance optimization suggestions",
                    Type = ReviewRuleType.Performance,
                    IsEnabled = true,
                    MinimumSeverity = ReviewSeverity.Info,
                    MaximumSeverity = ReviewSeverity.Warning,
                    FilePatterns = new List<string> { "**/*.cs", "**/*.js", "**/*.ts", "**/*.py" },
                    Priority = 2
                }
            };
        }

        private List<FileExclusionRule> CreateDefaultFileExclusionRules()
        {
            return new List<FileExclusionRule>
            {
                new FileExclusionRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Node Modules",
                    Description = "Exclude node_modules directory",
                    Pattern = "node_modules/**",
                    Type = ExclusionType.Glob,
                    IsEnabled = true
                },
                new FileExclusionRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Build Artifacts",
                    Description = "Exclude common build output directories",
                    Pattern = "{bin,obj,dist,build,target}/**",
                    Type = ExclusionType.Glob,
                    IsEnabled = true
                },
                new FileExclusionRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Binary Files",
                    Description = "Exclude common binary file types",
                    Type = ExclusionType.BinaryFiles,
                    Pattern = "*",
                    IsEnabled = true,
                    FileExtensions = new List<string> { ".exe", ".dll", ".so", ".dylib", ".bin", ".zip", ".tar", ".gz" }
                },
                new FileExclusionRule
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = "Large Files",
                    Description = "Exclude files larger than 1MB",
                    Type = ExclusionType.FileSize,
                    Pattern = "*",
                    IsEnabled = true,
                    MaxFileSizeBytes = 1024 * 1024 // 1MB
                }
            };
        }
    }
}