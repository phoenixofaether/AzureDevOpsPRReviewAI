namespace AzureDevOpsPRReviewAI.Infrastructure.Services
{
    using AzureDevOpsPRReviewAI.Core.Interfaces;
    using AzureDevOpsPRReviewAI.Core.Models;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using System.Diagnostics;
    using System.Text;
    using System.Text.RegularExpressions;

    public class DirectRepositoryQueryService : IDirectRepositoryQueryService
    {
        private readonly ILogger<DirectRepositoryQueryService> logger;
        private readonly IFileRetrievalService fileRetrievalService;
        private readonly IRepositoryService repositoryService;
        private readonly ITokenizerService tokenizerService;
        private readonly IMemoryCache cache;

        public DirectRepositoryQueryService(
            ILogger<DirectRepositoryQueryService> logger,
            IFileRetrievalService fileRetrievalService,
            IRepositoryService repositoryService,
            ITokenizerService tokenizerService,
            IMemoryCache cache)
        {
            this.logger = logger;
            this.fileRetrievalService = fileRetrievalService;
            this.repositoryService = repositoryService;
            this.tokenizerService = tokenizerService;
            this.cache = cache;
        }

        public async Task<DirectFileReadResult> ReadFileAsync(string repositoryPath, string filePath, int? startLine = null, int? endLine = null, string? branch = null)
        {
            try
            {
                this.logger.LogDebug("Reading file {FilePath} from repository {RepositoryPath} (lines {StartLine}-{EndLine})",
                    filePath, repositoryPath, startLine, endLine);

                var fileContent = await this.fileRetrievalService.GetFileAsync(repositoryPath, filePath, branch);
                if (fileContent == null)
                {
                    return new DirectFileReadResult
                    {
                        FilePath = filePath,
                        Content = string.Empty,
                        StartLine = 0,
                        EndLine = 0,
                        TotalLines = 0,
                        IsPartialRead = false,
                        IsBinary = false,
                        TruncationReason = "File not found"
                    };
                }

                if (fileContent.IsBinary)
                {
                    return new DirectFileReadResult
                    {
                        FilePath = filePath,
                        Content = $"<Binary file - {fileContent.SizeInBytes} bytes>",
                        StartLine = 0,
                        EndLine = 0,
                        TotalLines = 0,
                        IsPartialRead = false,
                        IsBinary = true,
                        FileSizeBytes = fileContent.SizeInBytes,
                        LastModified = fileContent.LastModified,
                        Encoding = fileContent.Encoding
                    };
                }

                var lines = fileContent.Content!.Split('\n');
                var totalLines = lines.Length;

                var actualStartLine = Math.Max(1, startLine ?? 1);
                var actualEndLine = Math.Min(totalLines, endLine ?? totalLines);

                // Adjust for 0-based array indexing
                var startIndex = actualStartLine - 1;
                var endIndex = actualEndLine - 1;

                if (startIndex >= totalLines || startIndex < 0)
                {
                    return new DirectFileReadResult
                    {
                        FilePath = filePath,
                        Content = string.Empty,
                        StartLine = actualStartLine,
                        EndLine = actualEndLine,
                        TotalLines = totalLines,
                        IsPartialRead = true,
                        IsBinary = false,
                        TruncationReason = "Start line exceeds file length"
                    };
                }

                var selectedLines = lines[startIndex..(endIndex + 1)];
                var contentBuilder = new StringBuilder();

                for (int i = 0; i < selectedLines.Length; i++)
                {
                    var lineNumber = actualStartLine + i;
                    contentBuilder.AppendLine($"{lineNumber,5:D}→{selectedLines[i]}");
                }

                return new DirectFileReadResult
                {
                    FilePath = filePath,
                    Content = contentBuilder.ToString(),
                    StartLine = actualStartLine,
                    EndLine = Math.Min(actualEndLine, actualStartLine + selectedLines.Length - 1),
                    TotalLines = totalLines,
                    IsPartialRead = startLine.HasValue || endLine.HasValue,
                    IsBinary = false,
                    FileSizeBytes = fileContent.SizeInBytes,
                    LastModified = fileContent.LastModified,
                    Encoding = fileContent.Encoding
                };
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to read file {FilePath} from repository {RepositoryPath}", filePath, repositoryPath);
                return new DirectFileReadResult
                {
                    FilePath = filePath,
                    Content = string.Empty,
                    StartLine = 0,
                    EndLine = 0,
                    TotalLines = 0,
                    IsPartialRead = false,
                    IsBinary = false,
                    TruncationReason = $"Error reading file: {ex.Message}"
                };
            }
        }

        public async Task<DirectSearchResult> SearchFilesAsync(string repositoryPath, string pattern, DirectSearchOptions options)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new DirectSearchResult { Query = pattern };

            try
            {
                this.logger.LogDebug("Searching for pattern '{Pattern}' in repository {RepositoryPath}", pattern, repositoryPath);

                var allFiles = await this.repositoryService.GetAllFilesAsync(repositoryPath);
                var filteredFiles = this.FilterFilesByOptions(allFiles, options).ToList();

                result.FilesSearched = filteredFiles.Count;

                var regex = options.IsRegex ? new Regex(pattern, options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)
                    : new Regex(Regex.Escape(pattern), options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);

                foreach (var filePath in filteredFiles)
                {
                    if (result.TotalMatches >= options.MaxResults)
                    {
                        result.WasTruncated = true;
                        result.TruncationReason = $"Reached maximum results limit of {options.MaxResults}";
                        break;
                    }

                    try
                    {
                        var fileContent = await this.fileRetrievalService.GetFileAsync(repositoryPath, filePath);
                        if (fileContent?.Content == null || fileContent.IsBinary && !options.IncludeBinaryFiles)
                        {
                            result.FilesSkipped++;
                            continue;
                        }

                        var lines = fileContent.Content.Split('\n');
                        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                        {
                            var line = lines[lineIndex];
                            var matches = regex.Matches(line);

                            foreach (Match match in matches)
                            {
                                if (result.TotalMatches >= options.MaxResults)
                                {
                                    result.WasTruncated = true;
                                    result.TruncationReason = $"Reached maximum results limit of {options.MaxResults}";
                                    break;
                                }

                                var contextBefore = new List<string>();
                                var contextAfter = new List<string>();

                                // Get context lines
                                for (int i = Math.Max(0, lineIndex - options.ContextLines); i < lineIndex; i++)
                                {
                                    contextBefore.Add($"{i + 1,5:D}→{lines[i]}");
                                }

                                for (int i = lineIndex + 1; i < Math.Min(lines.Length, lineIndex + options.ContextLines + 1); i++)
                                {
                                    contextAfter.Add($"{i + 1,5:D}→{lines[i]}");
                                }

                                result.Matches.Add(new DirectSearchMatch
                                {
                                    FilePath = filePath,
                                    LineNumber = lineIndex + 1,
                                    LineContent = $"{lineIndex + 1,5:D}→{line}",
                                    ContextBefore = contextBefore,
                                    ContextAfter = contextAfter,
                                    MatchStartIndex = match.Index,
                                    MatchLength = match.Length,
                                    MatchedText = match.Value
                                });

                                result.TotalMatches++;
                            }

                            if (result.WasTruncated) break;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogWarning(ex, "Failed to search in file {FilePath}", filePath);
                        result.FilesSkipped++;
                    }
                }

                result.SearchDuration = stopwatch.Elapsed;
                this.logger.LogDebug("Search completed: {MatchCount} matches in {FileCount} files in {Duration}ms",
                    result.TotalMatches, result.FilesSearched, result.SearchDuration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to search for pattern '{Pattern}' in repository {RepositoryPath}", pattern, repositoryPath);
                result.SearchDuration = stopwatch.Elapsed;
                result.TruncationReason = $"Search failed: {ex.Message}";
                return result;
            }
        }

        public async Task<DirectFileSearchResult> FindFilesAsync(string repositoryPath, string namePattern, DirectFileSearchOptions options)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new DirectFileSearchResult { Pattern = namePattern };

            try
            {
                this.logger.LogDebug("Finding files matching pattern '{Pattern}' in repository {RepositoryPath}", namePattern, repositoryPath);

                var allFiles = await this.repositoryService.GetAllFilesAsync(repositoryPath);
                var filteredFiles = this.FilterFilesByFileSearchOptions(allFiles, options).ToList();

                var wildcardPattern = this.ConvertWildcardToRegex(namePattern);
                var regex = new Regex(wildcardPattern, RegexOptions.IgnoreCase);

                var matchingFiles = new List<DirectFileInfo>();

                foreach (var filePath in filteredFiles)
                {
                    var fileName = Path.GetFileName(filePath);
                    if (regex.IsMatch(fileName))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(Path.Combine(repositoryPath, filePath));
                            var isBinary = await this.fileRetrievalService.IsBinaryFileAsync(filePath);

                            matchingFiles.Add(new DirectFileInfo
                            {
                                FilePath = filePath,
                                FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
                                LastModified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue,
                                FileExtension = Path.GetExtension(filePath),
                                IsBinary = isBinary,
                                IsHidden = fileName.StartsWith('.')
                            });
                        }
                        catch (Exception ex)
                        {
                            this.logger.LogWarning(ex, "Failed to get file info for {FilePath}", filePath);
                        }
                    }
                }

                if (options.SortByModificationDate)
                {
                    matchingFiles = matchingFiles.OrderByDescending(f => f.LastModified).ToList();
                }

                if (matchingFiles.Count > options.MaxResults)
                {
                    result.WasTruncated = true;
                    result.TruncationReason = $"Reached maximum results limit of {options.MaxResults}";
                    matchingFiles = matchingFiles.Take(options.MaxResults).ToList();
                }

                result.Files = matchingFiles;
                result.TotalFiles = matchingFiles.Count;
                result.SearchDuration = stopwatch.Elapsed;

                this.logger.LogDebug("File search completed: {FileCount} files found in {Duration}ms",
                    result.TotalFiles, result.SearchDuration.TotalMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to find files matching pattern '{Pattern}' in repository {RepositoryPath}", namePattern, repositoryPath);
                result.SearchDuration = stopwatch.Elapsed;
                result.TruncationReason = $"File search failed: {ex.Message}";
                return result;
            }
        }

        public async Task<DirectoryStructureResult> GetFileStructureAsync(string repositoryPath, string? subPath = null, int maxDepth = 3)
        {
            try
            {
                this.logger.LogDebug("Getting file structure for repository {RepositoryPath} (subPath: {SubPath}, maxDepth: {MaxDepth})",
                    repositoryPath, subPath, maxDepth);

                var result = new DirectoryStructureResult
                {
                    RootPath = subPath ?? "/"
                };

                var allFiles = await this.repositoryService.GetAllFilesAsync(repositoryPath);
                var filteredFiles = allFiles
                    .Where(f => subPath == null || f.StartsWith(subPath.TrimEnd('/') + "/") || f == subPath)
                    .ToList();

                var fileGroups = filteredFiles
                    .Select(f => new
                    {
                        FilePath = f,
                        Parts = f.Split('/', StringSplitOptions.RemoveEmptyEntries),
                        IsInSubPath = subPath == null || f.StartsWith(subPath.TrimEnd('/') + "/") || f == subPath
                    })
                    .Where(x => x.IsInSubPath)
                    .GroupBy(x => x.Parts.Length > 0 ? x.Parts[0] : string.Empty)
                    .ToList();

                foreach (var group in fileGroups.Take(100)) // Limit to prevent excessive output
                {
                    var node = await this.BuildDirectoryNodeAsync(repositoryPath, group.Key, group, 0, maxDepth);
                    if (node != null)
                    {
                        result.Nodes.Add(node);
                        this.CountNodesRecursive(node, result);
                    }
                }

                if (fileGroups.Count > 100)
                {
                    result.WasTruncated = true;
                    result.TruncationReason = "Too many files/directories to display";
                }

                return result;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get file structure for repository {RepositoryPath}", repositoryPath);
                return new DirectoryStructureResult
                {
                    RootPath = subPath ?? "/",
                    TruncationReason = $"Failed to get structure: {ex.Message}"
                };
            }
        }

        public async Task<DirectFileReadResult> GetNearbyContextAsync(string repositoryPath, string filePath, int lineNumber, int contextLines = 5)
        {
            var startLine = Math.Max(1, lineNumber - contextLines);
            var endLine = lineNumber + contextLines;

            return await this.ReadFileAsync(repositoryPath, filePath, startLine, endLine);
        }

        public async Task<List<CodeChunk>> GetRelevantContextAsync(string repositoryPath, string query, int maxTokens = 100000)
        {
            try
            {
                this.logger.LogDebug("Getting relevant context for query '{Query}' in repository {RepositoryPath}", query, repositoryPath);

                var cacheKey = $"direct_context_{repositoryPath}_{query}_{maxTokens}";
                if (this.cache.TryGetValue(cacheKey, out List<CodeChunk>? cachedResult))
                {
                    return cachedResult!;
                }

                var relevantChunks = new List<CodeChunk>();
                var currentTokens = 0;

                // Extract keywords from query
                var keywords = this.ExtractKeywords(query);
                var searchPattern = string.Join("|", keywords.Select(Regex.Escape));

                var searchOptions = new DirectSearchOptions
                {
                    IsRegex = true,
                    CaseSensitive = false,
                    MaxResults = 50,
                    ContextLines = 3,
                    FileExtensions = new List<string> { ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".h" },
                    ExcludeDirectories = new List<string> { "node_modules", "bin", "obj", ".git", ".vs" }
                };

                var searchResult = await this.SearchFilesAsync(repositoryPath, searchPattern, searchOptions);

                foreach (var match in searchResult.Matches.OrderByDescending(m => this.CalculateRelevanceScore(m, keywords)))
                {
                    if (currentTokens >= maxTokens)
                    {
                        break;
                    }

                    var content = string.Join("\n", 
                        match.ContextBefore.Concat(new[] { match.LineContent }).Concat(match.ContextAfter));

                    var tokenCount = await this.tokenizerService.CountTokensAsync(content);

                    if (currentTokens + tokenCount <= maxTokens)
                    {
                        var chunk = new CodeChunk
                        {
                            FilePath = match.FilePath,
                            Content = content,
                            StartLine = match.LineNumber - match.ContextBefore.Count,
                            EndLine = match.LineNumber + match.ContextAfter.Count,
                            ChunkType = this.DetermineChunkType(content),
                            // Language property removed from CodeChunk model
                            TokenCount = tokenCount,
                            RelevanceScore = this.CalculateRelevanceScore(match, keywords)
                        };

                        relevantChunks.Add(chunk);
                        currentTokens += tokenCount;
                    }
                }

                // Cache results for 30 minutes
                this.cache.Set(cacheKey, relevantChunks, TimeSpan.FromMinutes(30));

                this.logger.LogDebug("Found {ChunkCount} relevant chunks using {TokenCount} tokens via direct search",
                    relevantChunks.Count, currentTokens);

                return relevantChunks;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to get relevant context for query '{Query}' in repository {RepositoryPath}", query, repositoryPath);
                return new List<CodeChunk>();
            }
        }

        private IEnumerable<string> FilterFilesByOptions(IEnumerable<string> files, DirectSearchOptions options)
        {
            return files.Where(file =>
            {
                // Check file extensions
                if (options.FileExtensions.Any() && 
                    !options.FileExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Check exclude patterns
                if (options.ExcludeFilePatterns.Any(pattern => 
                    Regex.IsMatch(Path.GetFileName(file), this.ConvertWildcardToRegex(pattern), RegexOptions.IgnoreCase)))
                {
                    return false;
                }

                // Check directories
                var directory = Path.GetDirectoryName(file) ?? string.Empty;
                
                if (options.ExcludeDirectories.Any(excludeDir => 
                    directory.Contains(excludeDir, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                if (options.IncludeDirectories.Any() && 
                    !options.IncludeDirectories.Any(includeDir => 
                        directory.Contains(includeDir, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                return true;
            });
        }

        private IEnumerable<string> FilterFilesByFileSearchOptions(IEnumerable<string> files, DirectFileSearchOptions options)
        {
            return files.Where(file =>
            {
                // Check file extensions
                if (options.FileExtensions.Any() && 
                    !options.FileExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Check exclude patterns
                if (options.ExcludeFilePatterns.Any(pattern => 
                    Regex.IsMatch(Path.GetFileName(file), this.ConvertWildcardToRegex(pattern), RegexOptions.IgnoreCase)))
                {
                    return false;
                }

                // Check directories
                var directory = Path.GetDirectoryName(file) ?? string.Empty;
                
                if (options.ExcludeDirectories.Any(excludeDir => 
                    directory.Contains(excludeDir, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                if (options.IncludeDirectories.Any() && 
                    !options.IncludeDirectories.Any(includeDir => 
                        directory.Contains(includeDir, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                // Check hidden files
                if (!options.IncludeHiddenFiles && Path.GetFileName(file).StartsWith('.'))
                {
                    return false;
                }

                return true;
            });
        }

        private string ConvertWildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                .Replace(@"\*", ".*")
                .Replace(@"\?", ".") + "$";
        }

        private async Task<DirectoryNode?> BuildDirectoryNodeAsync(string repositoryPath, string name, IEnumerable<dynamic> group, int level, int maxDepth)
        {
            if (level >= maxDepth) return null;

            var firstItem = group.First();
            var fullPath = string.Join("/", ((string[])firstItem.Parts).Take(level + 1));
            var isDirectory = group.Any(x => x.Parts.Length > level + 1);

            var node = new DirectoryNode
            {
                Name = name,
                FullPath = fullPath,
                IsDirectory = isDirectory,
                Level = level
            };

            if (!isDirectory)
            {
                // It's a file
                try
                {
                    var fileInfo = new FileInfo(Path.Combine(repositoryPath, fullPath));
                    node.FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0;
                    node.LastModified = fileInfo.Exists ? fileInfo.LastWriteTime : DateTime.MinValue;
                    node.FileExtension = Path.GetExtension(fullPath);
                    node.IsBinary = await this.fileRetrievalService.IsBinaryFileAsync(fullPath);
                }
                catch (Exception ex)
                {
                    this.logger.LogWarning(ex, "Failed to get file info for {FilePath}", fullPath);
                }
            }
            else
            {
                // It's a directory, build children
                var children = group
                    .Where(x => x.Parts.Length > level + 1)
                    .GroupBy(x => (string)x.Parts[level + 1])
                    .ToList();

                foreach (var childGroup in children.Take(20)) // Limit children
                {
                    var childNode = await this.BuildDirectoryNodeAsync(repositoryPath, childGroup.Key, childGroup, level + 1, maxDepth);
                    if (childNode != null)
                    {
                        node.Children.Add(childNode);
                    }
                }
            }

            return node;
        }

        private void CountNodesRecursive(DirectoryNode node, DirectoryStructureResult result)
        {
            if (node.IsDirectory)
            {
                result.TotalDirectories++;
                foreach (var child in node.Children)
                {
                    this.CountNodesRecursive(child, result);
                }
            }
            else
            {
                result.TotalFiles++;
            }
        }

        private List<string> ExtractKeywords(string query)
        {
            var words = Regex.Matches(query, @"\b[A-Za-z_][A-Za-z0-9_]*\b")
                .Cast<Match>()
                .Select(m => m.Value)
                .Where(w => w.Length > 2)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return words;
        }

        private double CalculateRelevanceScore(DirectSearchMatch match, List<string> keywords)
        {
            var content = match.LineContent.ToLower();
            var matchCount = keywords.Count(keyword => content.Contains(keyword.ToLower()));
            
            var baseScore = (double)matchCount / keywords.Count;
            
            // Boost for certain file types or locations
            if (match.FilePath.EndsWith(".cs") || match.FilePath.EndsWith(".ts"))
            {
                baseScore *= 1.2;
            }
            
            // Boost for interface or class definitions
            if (content.Contains("class ") || content.Contains("interface ") || content.Contains("public "))
            {
                baseScore *= 1.3;
            }

            return Math.Min(1.0, baseScore);
        }

        private CodeChunkType DetermineChunkType(string content)
        {
            var lowerContent = content.ToLower();
            
            if (lowerContent.Contains("class "))
                return CodeChunkType.Class;
            if (lowerContent.Contains("interface "))
                return CodeChunkType.Interface;
            if (lowerContent.Contains("public ") || lowerContent.Contains("private ") || lowerContent.Contains("protected "))
                return CodeChunkType.Method;
            if (lowerContent.Contains("//") || lowerContent.Contains("/*"))
                return CodeChunkType.Comment;
            
            return CodeChunkType.Other;
        }

        private string DetermineLanguageFromFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".cs" => "csharp",
                ".js" => "javascript",
                ".ts" => "typescript",
                ".py" => "python",
                ".java" => "java",
                ".cpp" or ".c" => "cpp",
                ".h" or ".hpp" => "cpp",
                _ => "text"
            };
        }
    }
}