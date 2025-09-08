namespace AzureDevOpsPRReviewAI.Core.Models
{
    public class User
    {
        public required string Id { get; set; }

        public required string Username { get; set; }

        public required string Email { get; set; }

        public string? DisplayName { get; set; }

        public bool IsActive { get; set; } = true;

        public List<UserRole> Roles { get; set; } = new();

        public List<string> Organizations { get; set; } = new();

        public UserPreferences Preferences { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastLoginAt { get; set; }

        public string? CreatedBy { get; set; }

        public string? UpdatedBy { get; set; }

        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class UserRole
    {
        public required string Role { get; set; }

        public string? Organization { get; set; }

        public string? Project { get; set; }

        public string? Repository { get; set; }

        public List<string> Permissions { get; set; } = new();

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

        public string? GrantedBy { get; set; }

        public DateTime? ExpiresAt { get; set; }
    }

    public class UserPreferences
    {
        public NotificationSettings Notifications { get; set; } = new();

        public string? PreferredLanguage { get; set; } = "en";

        public string? TimeZone { get; set; }

        public bool EnableEmailNotifications { get; set; } = true;

        public bool EnableInAppNotifications { get; set; } = true;

        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    public class NotificationSettings
    {
        public bool ReviewCompleted { get; set; } = true;

        public bool ReviewFailed { get; set; } = true;

        public bool ConfigurationChanged { get; set; } = false;

        public bool WeeklyDigest { get; set; } = true;

        public List<string> EmailAddresses { get; set; } = new();
    }

    public enum SystemRole
    {
        /// <summary>
        /// System administrator with full access
        /// </summary>
        SystemAdmin,

        /// <summary>
        /// Organization administrator
        /// </summary>
        OrganizationAdmin,

        /// <summary>
        /// Project administrator
        /// </summary>
        ProjectAdmin,

        /// <summary>
        /// Repository administrator - can configure repository settings
        /// </summary>
        RepositoryAdmin,

        /// <summary>
        /// Reviewer - can trigger reviews and view results
        /// </summary>
        Reviewer,

        /// <summary>
        /// Developer - can view review results
        /// </summary>
        Developer,

        /// <summary>
        /// Read-only user
        /// </summary>
        ReadOnly
    }

    public static class Permissions
    {
        // System permissions
        public const string SystemManage = "system:manage";
        public const string SystemView = "system:view";
        
        // Organization permissions
        public const string OrganizationManage = "organization:manage";
        public const string OrganizationView = "organization:view";
        public const string OrganizationConfigEdit = "organization:config:edit";
        public const string OrganizationConfigView = "organization:config:view";
        
        // Project permissions
        public const string ProjectManage = "project:manage";
        public const string ProjectView = "project:view";
        public const string ProjectConfigEdit = "project:config:edit";
        public const string ProjectConfigView = "project:config:view";
        
        // Repository permissions
        public const string RepositoryManage = "repository:manage";
        public const string RepositoryView = "repository:view";
        public const string RepositoryConfigEdit = "repository:config:edit";
        public const string RepositoryConfigView = "repository:config:view";
        public const string RepositoryReviewTrigger = "repository:review:trigger";
        public const string RepositoryReviewView = "repository:review:view";
        
        // User management permissions
        public const string UserManage = "user:manage";
        public const string UserView = "user:view";
        public const string UserRoleAssign = "user:role:assign";
        
        // Configuration permissions
        public const string ConfigurationImport = "configuration:import";
        public const string ConfigurationExport = "configuration:export";
        public const string ConfigurationClone = "configuration:clone";

        public static Dictionary<SystemRole, List<string>> GetDefaultPermissions()
        {
            return new Dictionary<SystemRole, List<string>>
            {
                [SystemRole.SystemAdmin] = new List<string>
                {
                    SystemManage, SystemView,
                    OrganizationManage, OrganizationView, OrganizationConfigEdit, OrganizationConfigView,
                    ProjectManage, ProjectView, ProjectConfigEdit, ProjectConfigView,
                    RepositoryManage, RepositoryView, RepositoryConfigEdit, RepositoryConfigView, RepositoryReviewTrigger, RepositoryReviewView,
                    UserManage, UserView, UserRoleAssign,
                    ConfigurationImport, ConfigurationExport, ConfigurationClone
                },
                [SystemRole.OrganizationAdmin] = new List<string>
                {
                    OrganizationView, OrganizationConfigEdit, OrganizationConfigView,
                    ProjectView, ProjectConfigEdit, ProjectConfigView,
                    RepositoryView, RepositoryConfigEdit, RepositoryConfigView, RepositoryReviewTrigger, RepositoryReviewView,
                    ConfigurationImport, ConfigurationExport, ConfigurationClone
                },
                [SystemRole.ProjectAdmin] = new List<string>
                {
                    ProjectView, ProjectConfigEdit, ProjectConfigView,
                    RepositoryView, RepositoryConfigEdit, RepositoryConfigView, RepositoryReviewTrigger, RepositoryReviewView,
                    ConfigurationImport, ConfigurationExport, ConfigurationClone
                },
                [SystemRole.RepositoryAdmin] = new List<string>
                {
                    RepositoryView, RepositoryConfigEdit, RepositoryConfigView, RepositoryReviewTrigger, RepositoryReviewView,
                    ConfigurationImport, ConfigurationExport, ConfigurationClone
                },
                [SystemRole.Reviewer] = new List<string>
                {
                    RepositoryView, RepositoryConfigView, RepositoryReviewTrigger, RepositoryReviewView
                },
                [SystemRole.Developer] = new List<string>
                {
                    RepositoryView, RepositoryConfigView, RepositoryReviewView
                },
                [SystemRole.ReadOnly] = new List<string>
                {
                    RepositoryView, RepositoryReviewView
                }
            };
        }
    }
}