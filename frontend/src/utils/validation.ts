import { z } from 'zod';
import {
  ReviewStrategy,
  QueryStrategy,
  ReviewRuleType,
  ReviewSeverity,
  ExclusionType,
  PromptType,
  PromptScope,
} from '../types/configuration';

// Helper function to create enum schema
const createEnumSchema = <T extends Record<string, string>>(enumObject: T) =>
  z.nativeEnum(enumObject);

// Duration schema for TimeSpan strings (ISO 8601 format)
const durationSchema = z.string().regex(
  /^P?T?(\d+H)?(\d+M)?(\d+(\.\d+)?S)?$/,
  'Invalid duration format. Use ISO 8601 format (e.g., "PT5M" for 5 minutes)'
);

// Basic settings schemas
export const webhookSettingsSchema = z.object({
  autoReviewOnCreate: z.boolean(),
  autoReviewOnUpdate: z.boolean(),
  requireCommentTrigger: z.boolean(),
  allowedTriggerUsers: z.array(z.string()),
  maxFilesForAutoReview: z.number().min(1).max(1000),
  maxDiffSizeBytes: z.number().min(1024).max(100 * 1024 * 1024), // 1KB to 100MB
});

export const commentSettingsSchema = z.object({
  enableLineComments: z.boolean(),
  enableSummaryComment: z.boolean(),
  groupSimilarIssues: z.boolean(),
  includeConfidenceScore: z.boolean(),
  commentPrefix: z.string().min(1).max(100),
  enableReplyToComments: z.boolean(),
  maxCommentsPerFile: z.number().min(1).max(50),
});

export const reviewStrategySettingsSchema = z.object({
  strategy: createEnumSchema(ReviewStrategy),
  enableParallelProcessing: z.boolean(),
  maxFilesPerRequest: z.number().min(1).max(100),
  maxTokensPerRequest: z.number().min(1000).max(1000000),
  maxTokensPerFile: z.number().min(100).max(100000),
  includeSummaryWhenSplit: z.boolean(),
  combineResultsFromMultipleRequests: z.boolean(),
  maxConcurrentRequests: z.number().min(1).max(10),
  requestTimeout: durationSchema,
});

export const querySettingsSchema = z.object({
  strategy: createEnumSchema(QueryStrategy),
  enableDirectFileAccess: z.boolean(),
  enableVectorSearch: z.boolean(),
  maxDirectSearchResults: z.number().min(10).max(1000),
  maxFileReadSizeKB: z.number().min(10).max(10240), // 10KB to 10MB
  defaultContextLines: z.number().min(0).max(20),
  defaultExcludePatterns: z.array(z.string()),
  defaultExcludeDirectories: z.array(z.string()),
  enableSearchResultCaching: z.boolean(),
  cacheExpiration: durationSchema,
});

// Rule schemas
export const reviewRuleSchema = z.object({
  id: z.string().min(1),
  name: z.string().min(1).max(100),
  description: z.string().optional(),
  type: createEnumSchema(ReviewRuleType),
  isEnabled: z.boolean(),
  minimumSeverity: createEnumSchema(ReviewSeverity),
  maximumSeverity: createEnumSchema(ReviewSeverity),
  filePatterns: z.array(z.string()),
  excludeFilePatterns: z.array(z.string()),
  parameters: z.record(z.string(), z.any()),
  priority: z.number().min(-100).max(100),
  createdAt: z.string(),
  updatedAt: z.string(),
});

export const fileExclusionRuleSchema = z.object({
  id: z.string().min(1),
  name: z.string().min(1).max(100),
  description: z.string().optional(),
  pattern: z.string().min(1),
  type: createEnumSchema(ExclusionType),
  isEnabled: z.boolean(),
  caseSensitive: z.boolean(),
  maxFileSizeBytes: z.number().min(0).optional(),
  fileExtensions: z.array(z.string()),
  createdAt: z.string(),
  updatedAt: z.string(),
});

export const customPromptSchema = z.object({
  id: z.string().min(1),
  name: z.string().min(1).max(100),
  description: z.string().optional(),
  type: createEnumSchema(PromptType),
  template: z.string().min(10), // Minimum meaningful prompt length
  isEnabled: z.boolean(),
  supportedLanguages: z.array(z.string()),
  supportedFileExtensions: z.array(z.string()),
  variables: z.record(z.string(), z.string()),
  scope: createEnumSchema(PromptScope),
  priority: z.number().min(-100).max(100),
  createdAt: z.string(),
  updatedAt: z.string(),
  createdBy: z.string().optional(),
  updatedBy: z.string().optional(),
});

// Main configuration schema
export const repositoryConfigurationSchema = z
  .object({
    id: z.string().min(1),
    organization: z.string().min(1).max(100),
    project: z.string().min(1).max(100),
    repository: z.string().min(1).max(100),
    isEnabled: z.boolean(),
    reviewRules: z.array(reviewRuleSchema),
    fileExclusionRules: z.array(fileExclusionRuleSchema),
    customPrompts: z.array(customPromptSchema),
    webhookSettings: webhookSettingsSchema,
    commentSettings: commentSettingsSchema,
    reviewStrategySettings: reviewStrategySettingsSchema,
    querySettings: querySettingsSchema,
    createdAt: z.string(),
    updatedAt: z.string(),
    createdBy: z.string().optional(),
    updatedBy: z.string().optional(),
    version: z.number().min(1),
  })
  .refine(
    (data) => {
      // Custom validation: minimum severity should not be higher than maximum severity
      return data.reviewRules.every((rule) => {
        const severityOrder = { Info: 0, Warning: 1, Error: 2 };
        return (
          severityOrder[rule.minimumSeverity] <= severityOrder[rule.maximumSeverity]
        );
      });
    },
    {
      message: 'Minimum severity cannot be higher than maximum severity',
      path: ['reviewRules'],
    }
  )
  .refine(
    (data) => {
      // Custom validation: query strategy must have at least one method enabled
      const { enableDirectFileAccess, enableVectorSearch, strategy } = data.querySettings;
      if (strategy === QueryStrategy.DirectOnly) return enableDirectFileAccess;
      if (strategy === QueryStrategy.VectorOnly) return enableVectorSearch;
      return enableDirectFileAccess || enableVectorSearch;
    },
    {
      message: 'At least one query method must be enabled for the selected strategy',
      path: ['querySettings'],
    }
  );

// Request schemas
export const cloneConfigurationRequestSchema = z.object({
  sourceOrganization: z.string().min(1),
  sourceProject: z.string().min(1),
  sourceRepository: z.string().min(1),
  targetOrganization: z.string().min(1),
  targetProject: z.string().min(1),
  targetRepository: z.string().min(1),
  overwriteExisting: z.boolean(),
});

export const importConfigurationRequestSchema = z.object({
  configurationJson: z.string().min(1),
});

// Utility functions for validation
export const validateConfiguration = (data: unknown) => {
  return repositoryConfigurationSchema.safeParse(data);
};

export const validatePartialConfiguration = (data: unknown, path?: string) => {
  try {
    repositoryConfigurationSchema.parse(data);
    return { success: true, data };
  } catch (error) {
    if (error instanceof z.ZodError) {
      const filteredErrors = path
        ? error.errors.filter((err) => err.path.join('.').startsWith(path))
        : error.errors;
      return {
        success: false,
        errors: filteredErrors.map((err) => ({
          path: err.path.join('.'),
          message: err.message,
        })),
      };
    }
    return { success: false, errors: [{ path: '', message: 'Unknown validation error' }] };
  }
};

// Form validation helpers
export const getFieldValidationRule = (schema: z.ZodSchema) => ({
  validator: async (_: any, value: any) => {
    try {
      await schema.parseAsync(value);
    } catch (error) {
      if (error instanceof z.ZodError) {
        throw new Error(error.errors[0]?.message || 'Validation failed');
      }
      throw error;
    }
  },
});

// Duration formatting utilities
export const formatDuration = (isoString: string): string => {
  const match = isoString.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?/);
  if (!match) return isoString;

  const [, hours, minutes, seconds] = match;
  const parts: string[] = [];

  if (hours) parts.push(`${hours}h`);
  if (minutes) parts.push(`${minutes}m`);
  if (seconds) parts.push(`${parseFloat(seconds)}s`);

  return parts.join(' ') || '0s';
};

export const parseDuration = (formatted: string): string => {
  if (formatted.startsWith('PT')) return formatted; // Already in ISO format

  const parts = formatted.toLowerCase().split(' ');
  let totalSeconds = 0;

  for (const part of parts) {
    const match = part.match(/^(\d+(?:\.\d+)?)(h|m|s)$/);
    if (match) {
      const [, value, unit] = match;
      const num = parseFloat(value);
      switch (unit) {
        case 'h':
          totalSeconds += num * 3600;
          break;
        case 'm':
          totalSeconds += num * 60;
          break;
        case 's':
          totalSeconds += num;
          break;
      }
    }
  }

  if (totalSeconds >= 3600) {
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    let result = `PT${hours}H`;
    if (minutes > 0) result += `${minutes}M`;
    if (seconds > 0) result += `${seconds}S`;
    return result;
  } else if (totalSeconds >= 60) {
    const minutes = Math.floor(totalSeconds / 60);
    const seconds = totalSeconds % 60;
    let result = `PT${minutes}M`;
    if (seconds > 0) result += `${seconds}S`;
    return result;
  } else {
    return `PT${totalSeconds}S`;
  }
};