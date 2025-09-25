// TypeScript interfaces matching the C# backend models

export interface RepositoryConfiguration {
  id: string;
  organization: string;
  project: string;
  repository: string;
  isEnabled: boolean;
  reviewRules: ReviewRule[];
  fileExclusionRules: FileExclusionRule[];
  customPrompts: CustomPrompt[];
  webhookSettings: WebhookSettings;
  commentSettings: CommentSettings;
  reviewStrategySettings: ReviewStrategySettings;
  querySettings: QuerySettings;
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
  updatedBy?: string;
  version: number;
}

export interface WebhookSettings {
  autoReviewOnCreate: boolean;
  autoReviewOnUpdate: boolean;
  requireCommentTrigger: boolean;
  allowedTriggerUsers: string[];
  maxFilesForAutoReview: number;
  maxDiffSizeBytes: number;
}

export interface CommentSettings {
  enableLineComments: boolean;
  enableSummaryComment: boolean;
  groupSimilarIssues: boolean;
  includeConfidenceScore: boolean;
  commentPrefix: string;
  enableReplyToComments: boolean;
  maxCommentsPerFile: number;
}

export interface ReviewStrategySettings {
  strategy: ReviewStrategy;
  enableParallelProcessing: boolean;
  maxFilesPerRequest: number;
  maxTokensPerRequest: number;
  maxTokensPerFile: number;
  includeSummaryWhenSplit: boolean;
  combineResultsFromMultipleRequests: boolean;
  maxConcurrentRequests: number;
  requestTimeout: string; // TimeSpan as ISO 8601 duration string
}

export const ReviewStrategy = {
  SingleRequest: 'SingleRequest',
  MultipleRequestsPerFile: 'MultipleRequestsPerFile',
  MultipleRequestsByTokenSize: 'MultipleRequestsByTokenSize',
  HybridStrategy: 'HybridStrategy'
} as const;

export type ReviewStrategy = typeof ReviewStrategy[keyof typeof ReviewStrategy];

export interface QuerySettings {
  strategy: QueryStrategy;
  enableDirectFileAccess: boolean;
  enableVectorSearch: boolean;
  maxDirectSearchResults: number;
  maxFileReadSizeKB: number;
  defaultContextLines: number;
  defaultExcludePatterns: string[];
  defaultExcludeDirectories: string[];
  enableSearchResultCaching: boolean;
  cacheExpiration: string; // TimeSpan as ISO 8601 duration string
}

export const QueryStrategy = {
  VectorOnly: 'VectorOnly',
  DirectOnly: 'DirectOnly',
  Hybrid: 'Hybrid',
  DirectFallback: 'DirectFallback'
} as const;

export type QueryStrategy = typeof QueryStrategy[keyof typeof QueryStrategy];

export interface ReviewRule {
  id: string;
  name: string;
  description?: string;
  type: ReviewRuleType;
  isEnabled: boolean;
  minimumSeverity: ReviewSeverity;
  maximumSeverity: ReviewSeverity;
  filePatterns: string[];
  excludeFilePatterns: string[];
  parameters: Record<string, any>;
  priority: number;
  createdAt: string;
  updatedAt: string;
}

export const ReviewRuleType = {
  CodeQuality: 'CodeQuality',
  Security: 'Security',
  Performance: 'Performance',
  Documentation: 'Documentation',
  Testing: 'Testing',
  Architecture: 'Architecture',
  Style: 'Style',
  BestPractices: 'BestPractices',
  Custom: 'Custom'
} as const;

export type ReviewRuleType = typeof ReviewRuleType[keyof typeof ReviewRuleType];

export const ReviewSeverity = {
  Info: 'Info',
  Warning: 'Warning',
  Error: 'Error'
} as const;

export type ReviewSeverity = typeof ReviewSeverity[keyof typeof ReviewSeverity];

export interface FileExclusionRule {
  id: string;
  name: string;
  description?: string;
  pattern: string;
  type: ExclusionType;
  isEnabled: boolean;
  caseSensitive: boolean;
  maxFileSizeBytes?: number;
  fileExtensions: string[];
  createdAt: string;
  updatedAt: string;
}

export const ExclusionType = {
  Glob: 'Glob',
  Regex: 'Regex',
  ExactPath: 'ExactPath',
  Directory: 'Directory',
  Extension: 'Extension',
  FileSize: 'FileSize',
  BinaryFiles: 'BinaryFiles'
} as const;

export type ExclusionType = typeof ExclusionType[keyof typeof ExclusionType];

export interface CustomPrompt {
  id: string;
  name: string;
  description?: string;
  type: PromptType;
  template: string;
  isEnabled: boolean;
  supportedLanguages: string[];
  supportedFileExtensions: string[];
  variables: Record<string, string>;
  scope: PromptScope;
  priority: number;
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
  updatedBy?: string;
}

export const PromptType = {
  CodeAnalysis: 'CodeAnalysis',
  SecurityAnalysis: 'SecurityAnalysis',
  PerformanceAnalysis: 'PerformanceAnalysis',
  DocumentationReview: 'DocumentationReview',
  TestAnalysis: 'TestAnalysis',
  ArchitectureReview: 'ArchitectureReview',
  StyleReview: 'StyleReview',
  Summary: 'Summary',
  Custom: 'Custom'
} as const;

export type PromptType = typeof PromptType[keyof typeof PromptType];

export const PromptScope = {
  Organization: 'Organization',
  Project: 'Project',
  Repository: 'Repository',
  FileType: 'FileType'
} as const;

export type PromptScope = typeof PromptScope[keyof typeof PromptScope];

export interface ConfigurationValidationResult {
  isValid: boolean;
  errors: ValidationError[];
  warnings: ValidationWarning[];
}

export interface ValidationError {
  message: string;
  propertyPath: string;
  errorCode: string;
}

export interface ValidationWarning {
  message: string;
  propertyPath: string;
  warningCode: string;
}

// API Request/Response types
export interface CloneConfigurationRequest {
  sourceOrganization: string;
  sourceProject: string;
  sourceRepository: string;
  targetOrganization: string;
  targetProject: string;
  targetRepository: string;
  overwriteExisting: boolean;
}

export interface ImportConfigurationRequest {
  configurationJson: string;
}