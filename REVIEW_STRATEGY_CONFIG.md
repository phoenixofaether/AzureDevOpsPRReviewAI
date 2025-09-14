# Pull Request Review Strategy Configuration

The Azure DevOps PR Review AI now supports configurable review strategies that determine how pull requests are analyzed by the AI. You can choose between single large requests or multiple smaller requests based on your needs.

## Configuration Options

Add the following to your repository configuration:

```json
{
  "ReviewStrategySettings": {
    "Strategy": "SingleRequest",
    "EnableParallelProcessing": false,
    "MaxFilesPerRequest": 10,
    "MaxTokensPerRequest": 100000,
    "MaxTokensPerFile": 20000,
    "IncludeSummaryWhenSplit": true,
    "CombineResultsFromMultipleRequests": true,
    "MaxConcurrentRequests": 3,
    "RequestTimeout": "00:05:00"
  }
}
```

## Review Strategies

### 1. SingleRequest (Default)
Analyzes the entire pull request in one API call to Claude.
- **Best for**: Small to medium PRs (< 100K tokens)
- **Pros**: Comprehensive context, faster processing
- **Cons**: May hit token limits on large PRs

### 2. MultipleRequestsPerFile
Processes each changed file individually.
- **Best for**: PRs with many independent file changes
- **Pros**: Better handling of large PRs, parallel processing
- **Cons**: Less cross-file context

### 3. MultipleRequestsByTokenSize
Splits the context by token size across multiple requests.
- **Best for**: Large PRs that exceed token limits
- **Pros**: Handles very large PRs, maintains some context
- **Cons**: May split related changes

### 4. HybridStrategy
Automatically chooses the best strategy based on PR size and complexity.
- **Best for**: Mixed workload with varying PR sizes
- **Pros**: Adapts to each PR automatically
- **Cons**: Less predictable processing approach

## Configuration Parameters

| Parameter | Description | Default |
|-----------|-------------|---------|
| `Strategy` | Review strategy to use | `SingleRequest` |
| `EnableParallelProcessing` | Enable concurrent API calls | `false` |
| `MaxFilesPerRequest` | Max files to process together | `10` |
| `MaxTokensPerRequest` | Token limit per API call | `100000` |
| `MaxTokensPerFile` | Token limit per individual file | `20000` |
| `IncludeSummaryWhenSplit` | Add summary comment for multi-request reviews | `true` |
| `CombineResultsFromMultipleRequests` | Merge results from multiple calls | `true` |
| `MaxConcurrentRequests` | Max parallel API calls | `3` |
| `RequestTimeout` | Timeout per API request | `00:05:00` |

## Usage Examples

### For Small Teams (Single Request)
```json
{
  "ReviewStrategySettings": {
    "Strategy": "SingleRequest",
    "MaxTokensPerRequest": 150000
  }
}
```

### For Large PRs (File-by-File)
```json
{
  "ReviewStrategySettings": {
    "Strategy": "MultipleRequestsPerFile",
    "EnableParallelProcessing": true,
    "MaxFilesPerRequest": 5,
    "MaxConcurrentRequests": 2
  }
}
```

### For Enterprise (Hybrid)
```json
{
  "ReviewStrategySettings": {
    "Strategy": "HybridStrategy",
    "EnableParallelProcessing": true,
    "MaxTokensPerRequest": 200000,
    "MaxConcurrentRequests": 5,
    "RequestTimeout": "00:10:00"
  }
}
```

## Performance Considerations

- **SingleRequest**: Fastest for small PRs, may fail on large ones
- **MultipleRequestsPerFile**: Good balance of speed and reliability
- **MultipleRequestsByTokenSize**: Slowest but handles any size PR
- **HybridStrategy**: Optimizes automatically based on PR characteristics

## Token Management

The system automatically estimates token usage and splits requests accordingly. Monitor the `RequestsProcessed` metadata field in analysis results to understand how many API calls were made per review.