namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using LibGit2Sharp;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Polly;
    using System.Text;

    public class RepositoryService : IRepositoryService
    {
        private readonly ILogger<RepositoryService> logger;
        private readonly IConfiguration configuration;
        private readonly ResiliencePipeline resiliencePipeline;
        private readonly string localStoragePath;
        private readonly int maxDiskUsageGB;
        private readonly int cleanupIntervalHours;

        public RepositoryService(ILogger<RepositoryService> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;

            this.localStoragePath = configuration["Repository:LocalStoragePath"] ?? "./repos";
            this.maxDiskUsageGB = int.TryParse(configuration["Repository:MaxDiskUsageGB"], out var maxGB) ? maxGB : 10;
            this.cleanupIntervalHours = int.TryParse(configuration["Repository:CleanupIntervalHours"], out var cleanupHours) ? cleanupHours : 24;

            this.resiliencePipeline = new ResiliencePipelineBuilder()
                .AddRetry(new Polly.Retry.RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder().Handle<Exception>(),
                    MaxRetryAttempts = 2,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = Polly.DelayBackoffType.Exponential,
                    UseJitter = true,
                })
                .Build();

            // Ensure local storage directory exists
            Directory.CreateDirectory(this.localStoragePath);
        }

        public async Task<RepositoryCloneResult> CloneRepositoryAsync(string organization, string project, string repository, string accessToken)
        {
            try
            {
                return await this.resiliencePipeline.ExecuteAsync(async _ =>
                {
                    var repositoryKey = $"{organization}_{project}_{repository}".Replace("/", "_");
                    var localRepoPath = Path.Combine(this.localStoragePath, repositoryKey);

                    this.logger.LogInformation(
                        "Cloning repository {Organization}/{Project}/{Repository} to {LocalPath}",
                        organization,
                        project,
                        repository,
                        localRepoPath);

                    // Check if repository already exists
                    if (Directory.Exists(localRepoPath) && Repository.IsValid(localRepoPath))
                    {
                        this.logger.LogInformation("Repository already exists locally, updating...");
                        return await this.UpdateExistingRepositoryAsync(localRepoPath, accessToken);
                    }

                    // Clean up old directory if it exists but is invalid
                    if (Directory.Exists(localRepoPath))
                    {
                        Directory.Delete(localRepoPath, recursive: true);
                    }

                    var cloneUrl = $"https://{organization}.visualstudio.com/{project}/_git/{repository}";

                    var credentials = new UsernamePasswordCredentials
                    {
                        Username = string.Empty,
                        Password = accessToken
                    };

                    var cloneOptions = new CloneOptions
                    {
                        IsBare = false,
                        Checkout = true,
                    };

                    cloneOptions.FetchOptions.CredentialsProvider = (_, _, _) => credentials;

                    var startTime = DateTime.UtcNow;

                    var repo = Repository.Clone(cloneUrl, localRepoPath, cloneOptions);

                    var endTime = DateTime.UtcNow;
                    var directoryInfo = new DirectoryInfo(localRepoPath);
                    var sizeInBytes = this.GetDirectorySize(directoryInfo);

                    using var repository_obj = new Repository(localRepoPath);
                    var branches = repository_obj.Branches.Select(b => b.FriendlyName).ToList();
                    var defaultBranch = repository_obj.Head.FriendlyName;

                    this.logger.LogInformation(
                        "Successfully cloned repository to {LocalPath}. Size: {SizeInMB}MB, Duration: {Duration}s",
                        localRepoPath,
                        sizeInBytes / (1024 * 1024),
                        (endTime - startTime).TotalSeconds);

                    return new RepositoryCloneResult
                    {
                        IsSuccessful = true,
                        LocalPath = localRepoPath,
                        CloneTime = startTime,
                        SizeInBytes = sizeInBytes,
                        DefaultBranch = defaultBranch,
                        AvailableBranches = branches
                    };
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to clone repository {Organization}/{Project}/{Repository}",
                    organization,
                    project,
                    repository);

                return new RepositoryCloneResult
                {
                    IsSuccessful = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<GitDiffResult> GetPullRequestDiffAsync(string repositoryPath, string sourceBranch, string targetBranch)
        {
            try
            {
                return await Task.Run(() =>
                {
                    this.logger.LogInformation(
                        "Generating diff between {SourceBranch} and {TargetBranch} in {RepositoryPath}",
                        sourceBranch,
                        targetBranch,
                        repositoryPath);

                    using var repo = new Repository(repositoryPath);

                    // Ensure we have the latest remote branches
                    var remote = repo.Network.Remotes["origin"];
                    var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                    Commands.Fetch(repo, remote.Name, refSpecs, null, null);

                    // Get the commits
                    var sourceCommit = repo.Branches[sourceBranch]?.Tip ?? 
                                      repo.Branches[$"origin/{sourceBranch}"]?.Tip;
                    var targetCommit = repo.Branches[targetBranch]?.Tip ?? 
                                      repo.Branches[$"origin/{targetBranch}"]?.Tip;

                    if (sourceCommit == null)
                    {
                        throw new ArgumentException($"Source branch '{sourceBranch}' not found");
                    }

                    if (targetCommit == null)
                    {
                        throw new ArgumentException($"Target branch '{targetBranch}' not found");
                    }

                    // Generate patch between commits
                    var patch = repo.Diff.Compare<Patch>(targetCommit.Tree, sourceCommit.Tree);

                    var result = new GitDiffResult
                    {
                        IsSuccessful = true,
                        TotalLinesAdded = patch.LinesAdded,
                        TotalLinesRemoved = patch.LinesDeleted
                    };

                    foreach (var patchEntry in patch)
                    {
                        var fileDiff = new FileDiff
                        {
                            FilePath = patchEntry.Path,
                            OldFilePath = patchEntry.OldPath,
                            ChangeType = this.MapChangeType(patchEntry.Status),
                            IsBinary = patchEntry.IsBinaryComparison,
                            LinesAdded = patchEntry.LinesAdded,
                            LinesRemoved = patchEntry.LinesDeleted
                        };

                        // Parse hunks
                        if (!patchEntry.IsBinaryComparison)
                        {
                            fileDiff.Hunks = this.ParseDiffHunks(patchEntry.Patch);
                        }

                        result.ChangedFiles.Add(fileDiff);
                    }

                    this.logger.LogInformation(
                        "Generated diff with {FileCount} changed files, +{LinesAdded}/-{LinesRemoved} lines",
                        result.ChangedFiles.Count,
                        result.TotalLinesAdded,
                        result.TotalLinesRemoved);

                    return result;
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(
                    ex,
                    "Failed to generate diff between {SourceBranch} and {TargetBranch}",
                    sourceBranch,
                    targetBranch);

                return new GitDiffResult
                {
                    IsSuccessful = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<string?> GetFileContentAsync(string repositoryPath, string filePath, string? branch = null)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var repo = new Repository(repositoryPath);

                    if (branch != null)
                    {
                        var branchObj = repo.Branches[branch] ?? repo.Branches[$"origin/{branch}"];
                        if (branchObj == null)
                        {
                            this.logger.LogWarning("Branch {Branch} not found in repository", branch);
                            return null;
                        }

                        var treeEntry = branchObj.Tip[filePath];
                        if (treeEntry?.Target is Blob blob)
                        {
                            return blob.GetContentText();
                        }
                    }
                    else
                    {
                        // Use working directory
                        var fullPath = Path.Combine(repositoryPath, filePath.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(fullPath))
                        {
                            return File.ReadAllText(fullPath);
                        }
                    }

                    return null;
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get file content for {FilePath}", filePath);
                return null;
            }
        }

        public async Task<List<string>> GetAllFilesAsync(string repositoryPath, string? branch = null)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var repo = new Repository(repositoryPath);

                    if (branch != null)
                    {
                        var branchObj = repo.Branches[branch] ?? repo.Branches[$"origin/{branch}"];
                        if (branchObj == null)
                        {
                            return new List<string>();
                        }

                        return branchObj.Tip.Tree.Select(entry => entry.Path).ToList();
                    }
                    else
                    {
                        // Use working directory
                        var files = Directory.GetFiles(repositoryPath, "*", SearchOption.AllDirectories)
                            .Where(f => !f.Contains(".git"))
                            .Select(f => Path.GetRelativePath(repositoryPath, f).Replace(Path.DirectorySeparatorChar, '/'))
                            .ToList();

                        return files;
                    }
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get all files from repository");
                return new List<string>();
            }
        }

        public async Task<bool> SwitchBranchAsync(string repositoryPath, string branchName)
        {
            try
            {
                return await Task.Run(() =>
                {
                    using var repo = new Repository(repositoryPath);

                    var branch = repo.Branches[branchName] ?? repo.Branches[$"origin/{branchName}"];
                    if (branch == null)
                    {
                        this.logger.LogWarning("Branch {BranchName} not found", branchName);
                        return false;
                    }

                    Commands.Checkout(repo, branch);
                    this.logger.LogInformation("Switched to branch {BranchName}", branchName);
                    return true;
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to switch to branch {BranchName}", branchName);
                return false;
            }
        }

        public async Task CleanupRepositoryAsync(string repositoryPath)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (Directory.Exists(repositoryPath))
                    {
                        Directory.Delete(repositoryPath, recursive: true);
                        this.logger.LogInformation("Cleaned up repository at {RepositoryPath}", repositoryPath);
                    }
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to cleanup repository at {RepositoryPath}", repositoryPath);
            }
        }

        public async Task<DiskSpaceInfo> GetDiskSpaceInfoAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    var driveInfo = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(this.localStoragePath))!);
                    var directoryInfo = new DirectoryInfo(this.localStoragePath);

                    var repositoriesSpace = 0L;
                    var repositoryCount = 0;

                    if (directoryInfo.Exists)
                    {
                        foreach (var subDir in directoryInfo.GetDirectories())
                        {
                            if (Repository.IsValid(subDir.FullName))
                            {
                                repositoriesSpace += this.GetDirectorySize(subDir);
                                repositoryCount++;
                            }
                        }
                    }

                    return new DiskSpaceInfo
                    {
                        TotalSpaceBytes = driveInfo.TotalSize,
                        FreeSpaceBytes = driveInfo.AvailableFreeSpace,
                        RepositoriesSpaceBytes = repositoriesSpace,
                        RepositoryCount = repositoryCount
                    };
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get disk space information");
                return new DiskSpaceInfo();
            }
        }

        public async Task PerformRepositoryCleanupAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    this.logger.LogInformation("Starting repository cleanup process");

                    var directoryInfo = new DirectoryInfo(this.localStoragePath);
                    if (!directoryInfo.Exists)
                    {
                        return;
                    }

                    var cutoffTime = DateTime.UtcNow.AddHours(-this.cleanupIntervalHours);
                    var deletedCount = 0;

                    foreach (var subDir in directoryInfo.GetDirectories())
                    {
                        if (subDir.LastWriteTimeUtc < cutoffTime)
                        {
                            try
                            {
                                subDir.Delete(recursive: true);
                                deletedCount++;
                                this.logger.LogInformation("Deleted old repository {DirectoryName}", subDir.Name);
                            }
                            catch (Exception ex)
                            {
                                this.logger.LogWarning(ex, "Failed to delete old repository {DirectoryName}", subDir.Name);
                            }
                        }
                    }

                    // Check disk usage
                    var diskInfo = this.GetDiskSpaceInfoAsync().Result;
                    var usageGB = diskInfo.RepositoriesSpaceBytes / (1024.0 * 1024.0 * 1024.0);

                    if (usageGB > this.maxDiskUsageGB)
                    {
                        this.logger.LogWarning(
                            "Repository storage usage ({UsageGB}GB) exceeds limit ({MaxGB}GB)",
                            usageGB,
                            this.maxDiskUsageGB);

                        // Delete oldest repositories until under limit
                        var repositoryDirs = directoryInfo.GetDirectories()
                            .Where(d => Repository.IsValid(d.FullName))
                            .OrderBy(d => d.LastWriteTimeUtc)
                            .ToList();

                        foreach (var oldRepo in repositoryDirs)
                        {
                            try
                            {
                                oldRepo.Delete(recursive: true);
                                deletedCount++;
                                this.logger.LogInformation("Deleted repository {DirectoryName} due to disk usage limit", oldRepo.Name);

                                // Recalculate usage
                                diskInfo = this.GetDiskSpaceInfoAsync().Result;
                                usageGB = diskInfo.RepositoriesSpaceBytes / (1024.0 * 1024.0 * 1024.0);

                                if (usageGB <= this.maxDiskUsageGB)
                                {
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                this.logger.LogWarning(ex, "Failed to delete repository {DirectoryName}", oldRepo.Name);
                            }
                        }
                    }

                    this.logger.LogInformation("Repository cleanup completed. Deleted {DeletedCount} repositories", deletedCount);
                });
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to perform repository cleanup");
            }
        }

        private async Task<RepositoryCloneResult> UpdateExistingRepositoryAsync(string localRepoPath, string accessToken)
        {
            try
            {
                using var repo = new Repository(localRepoPath);

                var fetchOptions = new FetchOptions
                {
                    CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials
                    {
                        Username = string.Empty,
                        Password = accessToken
                    }
                };

                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(repo, remote.Name, refSpecs, fetchOptions, null);

                var directoryInfo = new DirectoryInfo(localRepoPath);
                var sizeInBytes = this.GetDirectorySize(directoryInfo);
                var branches = repo.Branches.Select(b => b.FriendlyName).ToList();
                var defaultBranch = repo.Head.FriendlyName;

                this.logger.LogInformation("Updated existing repository at {LocalPath}", localRepoPath);

                return new RepositoryCloneResult
                {
                    IsSuccessful = true,
                    LocalPath = localRepoPath,
                    CloneTime = DateTime.UtcNow,
                    SizeInBytes = sizeInBytes,
                    DefaultBranch = defaultBranch,
                    AvailableBranches = branches
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to update existing repository at {LocalPath}", localRepoPath);
                throw;
            }
        }

        private long GetDirectorySize(DirectoryInfo directoryInfo)
        {
            long size = 0;

            try
            {
                size += directoryInfo.GetFiles().Sum(file => file.Length);
                size += directoryInfo.GetDirectories().Sum(dir => this.GetDirectorySize(dir));
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }

            return size;
        }

        private FileChangeType MapChangeType(ChangeKind changeKind)
        {
            return changeKind switch
            {
                ChangeKind.Added => FileChangeType.Add,
                ChangeKind.Modified => FileChangeType.Edit,
                ChangeKind.Deleted => FileChangeType.Delete,
                ChangeKind.Renamed => FileChangeType.Rename,
                _ => FileChangeType.Edit,
            };
        }

        private List<DiffHunk> ParseDiffHunks(string patchContent)
        {
            var hunks = new List<DiffHunk>();

            if (string.IsNullOrEmpty(patchContent))
            {
                return hunks;
            }

            var lines = patchContent.Split('\n');
            DiffHunk? currentHunk = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("@@"))
                {
                    // Parse hunk header: @@ -oldStart,oldLines +newStart,newLines @@
                    currentHunk = this.ParseHunkHeader(line);
                    if (currentHunk != null)
                    {
                        hunks.Add(currentHunk);
                    }
                }
                else if (currentHunk != null && (line.StartsWith(" ") || line.StartsWith("+") || line.StartsWith("-")))
                {
                    var diffLine = this.ParseDiffLine(line);
                    currentHunk.Lines.Add(diffLine);
                }
            }

            return hunks;
        }

        private DiffHunk? ParseHunkHeader(string headerLine)
        {
            // Format: @@ -oldStart,oldLines +newStart,newLines @@
            var match = System.Text.RegularExpressions.Regex.Match(
                headerLine,
                @"@@\s*-(\d+),(\d+)\s*\+(\d+),(\d+)\s*@@");

            if (match.Success)
            {
                return new DiffHunk
                {
                    OldStart = int.Parse(match.Groups[1].Value),
                    OldLines = int.Parse(match.Groups[2].Value),
                    NewStart = int.Parse(match.Groups[3].Value),
                    NewLines = int.Parse(match.Groups[4].Value)
                };
            }

            return null;
        }

        private DiffLine ParseDiffLine(string line)
        {
            var type = line[0] switch
            {
                ' ' => DiffLineType.Context,
                '+' => DiffLineType.Addition,
                '-' => DiffLineType.Deletion,
                _ => DiffLineType.Context
            };

            return new DiffLine
            {
                Type = type,
                Content = line.Length > 1 ? line[1..] : string.Empty
            };
        }
    }
}