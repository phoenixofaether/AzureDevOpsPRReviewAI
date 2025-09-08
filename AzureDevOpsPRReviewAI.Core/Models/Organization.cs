namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class Organization
    {
        public required string Id { get; set; }

        public required string Name { get; set; }

        public string? DisplayName { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public OrganizationSettings Settings { get; set; } = new();

        public List<string> AdminUsers { get; set; } = new();

        public List<Project> Projects { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }

        public string? UpdatedBy { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class Project
    {
        public required string Id { get; set; }

        public required string Name { get; set; }

        public string? DisplayName { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public ProjectSettings Settings { get; set; } = new();

        public List<string> AdminUsers { get; set; } = new();

        public List<Repository> Repositories { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }

        public string? UpdatedBy { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class Repository
    {
        public required string Id { get; set; }

        public required string Name { get; set; }

        public string? DisplayName { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public RepositorySettings Settings { get; set; } = new();

        public List<string> AdminUsers { get; set; } = new();

        public string? DefaultBranch { get; set; } = "main";

        public List<string> Languages { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string? CreatedBy { get; set; }

        public string? UpdatedBy { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class OrganizationSettings
    {
        public bool EnableAIReview { get; set; } = true;

        public bool RequireApprovalForNewRepositories { get; set; } = false;

        public List<string> DefaultAdminUsers { get; set; } = new();

        public RepositoryConfiguration? DefaultRepositoryConfiguration { get; set; }

        public int MaxRepositoriesPerProject { get; set; } = 100;

        public long MaxStorageSizeBytes { get; set; } = 10L * 1024 * 1024 * 1024; // 10GB

        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    public class ProjectSettings
    {
        public bool EnableAIReview { get; set; } = true;

        public bool InheritOrganizationSettings { get; set; } = true;

        public List<string> DefaultAdminUsers { get; set; } = new();

        public RepositoryConfiguration? DefaultRepositoryConfiguration { get; set; }

        public int MaxRepositories { get; set; } = 50;

        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    public class RepositorySettings
    {
        public bool EnableAIReview { get; set; } = true;

        public bool InheritProjectSettings { get; set; } = true;

        public List<string> DefaultAdminUsers { get; set; } = new();

        public List<string> AllowedUsers { get; set; } = new();

        public bool IsPrivate { get; set; } = false;

        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }
}