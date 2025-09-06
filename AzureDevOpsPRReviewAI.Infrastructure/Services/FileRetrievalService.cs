namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using LibGit2Sharp;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using System.Text;
    using System.Text.RegularExpressions;

    public class FileRetrievalService : IFileRetrievalService
    {
        private readonly ILogger<FileRetrievalService> logger;
        private readonly IMemoryCache memoryCache;
        private readonly IRepositoryService repositoryService;

        private readonly HashSet<string> binaryExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".dll", ".bin", ".obj", ".pdb", ".lib", ".so", ".dylib",
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico", ".tiff", ".webp",
            ".mp3", ".wav", ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv",
            ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".nupkg", ".vsix", ".msi", ".cab"
        };

        private readonly HashSet<string> textExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".vb", ".fs", ".ts", ".js", ".jsx", ".tsx", ".html", ".htm",
            ".css", ".scss", ".sass", ".less", ".json", ".xml", ".yaml", ".yml",
            ".md", ".txt", ".log", ".cfg", ".config", ".ini", ".properties",
            ".sql", ".ps1", ".sh", ".bat", ".cmd", ".py", ".java", ".cpp", ".c",
            ".h", ".hpp", ".go", ".rs", ".rb", ".php", ".pl", ".r", ".swift",
            ".kt", ".scala", ".clj", ".hs", ".elm", ".dart", ".vue", ".svelte"
        };

        public FileRetrievalService(
            ILogger<FileRetrievalService> logger,
            IMemoryCache memoryCache,
            IRepositoryService repositoryService)
        {
            this.logger = logger;
            this.memoryCache = memoryCache;
            this.repositoryService = repositoryService;
        }

        public async Task<FileContent?> GetFileAsync(string repositoryPath, string filePath, string? branch = null)
        {
            try
            {
                var cacheKey = $"{repositoryPath}:{filePath}:{branch ?? "working"}";
                
                if (this.memoryCache.TryGetValue(cacheKey, out FileContent? cachedContent))
                {
                    this.logger.LogDebug("Retrieved cached file content for {FilePath}", filePath);
                    return cachedContent;
                }

                var content = await this.repositoryService.GetFileContentAsync(repositoryPath, filePath, branch);
                if (content == null)
                {
                    return null;
                }

                var isBinary = await this.IsBinaryFileAsync(filePath);
                var encoding = await this.DetectFileEncodingAsync(Path.Combine(repositoryPath, filePath));

                var fileContent = new FileContent
                {
                    FilePath = filePath,
                    Content = isBinary ? "[Binary file - content not displayed]" : content,
                    Encoding = encoding,
                    IsBinary = isBinary,
                    SizeInBytes = Encoding.UTF8.GetByteCount(content),
                    LastModified = DateTime.UtcNow,
                    Branch = branch
                };

                // Cache for 5 minutes
                this.memoryCache.Set(cacheKey, fileContent, TimeSpan.FromMinutes(5));

                this.logger.LogDebug(
                    "Retrieved file content for {FilePath} ({SizeKB}KB, Binary: {IsBinary})",
                    filePath,
                    fileContent.SizeInBytes / 1024,
                    isBinary);

                return fileContent;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to retrieve file {FilePath}", filePath);
                return null;
            }
        }

        public async Task<List<FileContent>> GetFilesAsync(string repositoryPath, IEnumerable<string> filePaths, string? branch = null)
        {
            var results = new List<FileContent>();
            var tasks = filePaths.Select(async filePath =>
            {
                var content = await this.GetFileAsync(repositoryPath, filePath, branch);
                return content;
            });

            var fileContents = await Task.WhenAll(tasks);
            results.AddRange(fileContents.Where(content => content != null)!);

            this.logger.LogInformation(
                "Retrieved {SuccessCount} of {TotalCount} requested files",
                results.Count,
                filePaths.Count());

            return results;
        }

        public async Task<bool> IsBinaryFileAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var extension = Path.GetExtension(filePath);
                
                if (this.binaryExtensions.Contains(extension))
                {
                    return true;
                }

                if (this.textExtensions.Contains(extension))
                {
                    return false;
                }

                // For unknown extensions, try to detect by content
                try
                {
                    if (File.Exists(filePath))
                    {
                        return this.IsBinaryContent(filePath);
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogDebug(ex, "Could not check file content for {FilePath}", filePath);
                }

                // Default to text for unknown extensions
                return false;
            });
        }

        public async Task<string> DetectFileEncodingAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                {
                    return "UTF-8";
                }

                try
                {
                    var bytes = File.ReadAllBytes(filePath);
                    
                    // Check for BOM
                    if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    {
                        return "UTF-8-BOM";
                    }

                    if (bytes.Length >= 2)
                    {
                        if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                        {
                            return "UTF-16LE";
                        }
                        if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                        {
                            return "UTF-16BE";
                        }
                    }

                    // Try to decode as UTF-8
                    try
                    {
                        var decoder = Encoding.UTF8.GetDecoder();
                        decoder.Fallback = DecoderFallback.ExceptionFallback;
                        decoder.GetCharCount(bytes, 0, bytes.Length);
                        return "UTF-8";
                    }
                    catch (DecoderFallbackException)
                    {
                        // Not valid UTF-8
                    }

                    // Check for high-bit bytes that might indicate extended ASCII
                    var highBitCount = bytes.Count(b => b > 127);
                    if (highBitCount > 0)
                    {
                        return "Windows-1252"; // Common extended ASCII encoding
                    }

                    return "ASCII";
                }
                catch (Exception ex)
                {
                    this.logger.LogDebug(ex, "Could not detect encoding for {FilePath}", filePath);
                    return "UTF-8";
                }
            });
        }

        public async Task<List<string>> FilterFilesAsync(string repositoryPath, IEnumerable<string> filePaths, FileFilterOptions options)
        {
            return await Task.Run(() =>
            {
                var filteredFiles = new List<string>();

                foreach (var filePath in filePaths)
                {
                    try
                    {
                        // Check exclude patterns
                        if (options.ExcludePatterns.Any(pattern => this.MatchesPattern(filePath, pattern)))
                        {
                            continue;
                        }

                        // Check include patterns (if any specified)
                        if (options.IncludePatterns.Any() && 
                            !options.IncludePatterns.Any(pattern => this.MatchesPattern(filePath, pattern)))
                        {
                            continue;
                        }

                        var extension = Path.GetExtension(filePath);

                        // Check exclude extensions
                        if (options.ExcludeExtensions.Any() && 
                            options.ExcludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Check include extensions (if any specified)
                        if (options.IncludeExtensions.Any() && 
                            !options.IncludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Check binary files
                        if (options.ExcludeBinaryFiles && this.IsBinaryFileAsync(filePath).Result)
                        {
                            continue;
                        }

                        // Check file size
                        var fullPath = Path.Combine(repositoryPath, filePath);
                        if (File.Exists(fullPath))
                        {
                            var fileInfo = new FileInfo(fullPath);
                            if (fileInfo.Length > options.MaxFileSizeBytes)
                            {
                                this.logger.LogDebug(
                                    "Excluding file {FilePath} due to size ({SizeKB}KB > {MaxSizeKB}KB)",
                                    filePath,
                                    fileInfo.Length / 1024,
                                    options.MaxFileSizeBytes / 1024);
                                continue;
                            }
                        }

                        filteredFiles.Add(filePath);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Error filtering file {FilePath}", filePath);
                    }
                }

                this.logger.LogInformation(
                    "Filtered {FilteredCount} files from {TotalCount} total files",
                    filteredFiles.Count,
                    filePaths.Count());

                return filteredFiles;
            });
        }

        public async Task CacheFileAsync(string repositoryPath, string filePath, FileContent content)
        {
            await Task.Run(() =>
            {
                var cacheKey = $"{repositoryPath}:{filePath}:{content.Branch ?? "working"}";
                this.memoryCache.Set(cacheKey, content, TimeSpan.FromMinutes(10));
                
                this.logger.LogDebug("Cached file content for {FilePath}", filePath);
            });
        }

        public async Task<FileContent?> GetCachedFileAsync(string repositoryPath, string filePath)
        {
            return await Task.Run(() =>
            {
                var cacheKey = $"{repositoryPath}:{filePath}:working";
                
                if (this.memoryCache.TryGetValue(cacheKey, out FileContent? cachedContent))
                {
                    this.logger.LogDebug("Found cached file content for {FilePath}", filePath);
                    return cachedContent;
                }

                return null;
            });
        }

        public async Task InvalidateCacheAsync(string repositoryPath, string? filePath = null)
        {
            await Task.Run(() =>
            {
                if (filePath != null)
                {
                    // Invalidate specific file cache
                    var cacheKeys = new[]
                    {
                        $"{repositoryPath}:{filePath}:working",
                        $"{repositoryPath}:{filePath}:main",
                        $"{repositoryPath}:{filePath}:master"
                    };

                    foreach (var key in cacheKeys)
                    {
                        this.memoryCache.Remove(key);
                    }

                    this.logger.LogDebug("Invalidated cache for file {FilePath}", filePath);
                }
                else
                {
                    // For now, we don't have a way to invalidate all keys for a repository
                    // This would require a more sophisticated caching mechanism
                    this.logger.LogDebug("Cache invalidation requested for repository {RepositoryPath}", repositoryPath);
                }
            });
        }

        private bool MatchesPattern(string filePath, string pattern)
        {
            // Convert glob pattern to regex
            var regexPattern = pattern
                .Replace("\\", "\\\\")
                .Replace(".", "\\.")
                .Replace("*", ".*")
                .Replace("?", ".")
                .Replace("/", "[/\\\\]"); // Support both forward and back slashes

            return Regex.IsMatch(filePath, $"^{regexPattern}$", RegexOptions.IgnoreCase);
        }

        private bool IsBinaryContent(string filePath)
        {
            try
            {
                var buffer = new byte[8192];
                using var fileStream = File.OpenRead(filePath);
                var bytesRead = fileStream.Read(buffer, 0, buffer.Length);

                // Check for null bytes (strong indicator of binary content)
                for (var i = 0; i < bytesRead; i++)
                {
                    if (buffer[i] == 0)
                    {
                        return true;
                    }
                }

                // Check for high percentage of non-printable characters
                var nonPrintableCount = 0;
                for (var i = 0; i < bytesRead; i++)
                {
                    var b = buffer[i];
                    if (b < 32 && b != 9 && b != 10 && b != 13) // Not tab, LF, or CR
                    {
                        nonPrintableCount++;
                    }
                }

                // If more than 30% of bytes are non-printable, consider it binary
                return (double)nonPrintableCount / bytesRead > 0.30;
            }
            catch (Exception ex)
            {
                this.logger.LogDebug(ex, "Could not analyze file content for {FilePath}", filePath);
                return false; // Default to text if we can't analyze
            }
        }
    }
}