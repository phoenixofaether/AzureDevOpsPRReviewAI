import axios, { AxiosInstance } from 'axios';
import {
  RepositoryConfiguration,
  CloneConfigurationRequest,
  ImportConfigurationRequest,
  ConfigurationValidationResult,
} from '../types/configuration';

// Create axios instance with base configuration
const createApiClient = (): AxiosInstance => {
  const client = axios.create({
    baseURL: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5046/api',
    timeout: 30000,
    headers: {
      'Content-Type': 'application/json',
    },
  });

  // Request interceptor for authentication
  client.interceptors.request.use(
    (config) => {
      const token = localStorage.getItem('auth_token');
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
      return config;
    },
    (error) => Promise.reject(error)
  );

  // Response interceptor for error handling
  client.interceptors.response.use(
    (response) => response,
    (error) => {
      if (error.response?.status === 401) {
        localStorage.removeItem('auth_token');
        window.location.href = '/login';
      }
      return Promise.reject(error);
    }
  );

  return client;
};

const apiClient = createApiClient();

// Configuration API
export const configurationApi = {
  // Get configuration for a specific repository
  getConfiguration: async (
    organization: string,
    project: string,
    repository: string
  ): Promise<RepositoryConfiguration> => {
    const response = await apiClient.get<RepositoryConfiguration>(
      `/configuration/${encodeURIComponent(organization)}/${encodeURIComponent(project)}/${encodeURIComponent(repository)}`
    );
    return response.data;
  },

  // Get effective configuration (includes defaults)
  getEffectiveConfiguration: async (
    organization: string,
    project: string,
    repository: string
  ): Promise<RepositoryConfiguration> => {
    const response = await apiClient.get<RepositoryConfiguration>(
      `/configuration/${encodeURIComponent(organization)}/${encodeURIComponent(project)}/${encodeURIComponent(repository)}/effective`
    );
    return response.data;
  },

  // Get all configurations for an organization
  getOrganizationConfigurations: async (
    organization: string
  ): Promise<RepositoryConfiguration[]> => {
    const response = await apiClient.get<RepositoryConfiguration[]>(
      `/configuration/organization/${encodeURIComponent(organization)}`
    );
    return response.data;
  },

  // Get all configurations for a project
  getProjectConfigurations: async (
    organization: string,
    project: string
  ): Promise<RepositoryConfiguration[]> => {
    const response = await apiClient.get<RepositoryConfiguration[]>(
      `/configuration/project/${encodeURIComponent(organization)}/${encodeURIComponent(project)}`
    );
    return response.data;
  },

  // Create or update configuration
  saveConfiguration: async (
    configuration: RepositoryConfiguration
  ): Promise<RepositoryConfiguration> => {
    const response = await apiClient.post<RepositoryConfiguration>(
      `/configuration`,
      configuration
    );
    return response.data;
  },

  // Update specific configuration
  updateConfiguration: async (
    organization: string,
    project: string,
    repository: string,
    configuration: RepositoryConfiguration
  ): Promise<RepositoryConfiguration> => {
    const response = await apiClient.put<RepositoryConfiguration>(
      `/configuration/${encodeURIComponent(organization)}/${encodeURIComponent(project)}/${encodeURIComponent(repository)}`,
      configuration
    );
    return response.data;
  },

  // Delete configuration
  deleteConfiguration: async (
    organization: string,
    project: string,
    repository: string
  ): Promise<void> => {
    await apiClient.delete(
      `/configuration/${encodeURIComponent(organization)}/${encodeURIComponent(project)}/${encodeURIComponent(repository)}`
    );
  },

  // Create default configuration
  createDefaultConfiguration: async (
    organization: string,
    project: string,
    repository: string
  ): Promise<RepositoryConfiguration> => {
    const response = await apiClient.post<RepositoryConfiguration>(
      `/configuration/${encodeURIComponent(organization)}/${encodeURIComponent(project)}/${encodeURIComponent(repository)}/default`
    );
    return response.data;
  },

  // Clone configuration
  cloneConfiguration: async (
    request: CloneConfigurationRequest
  ): Promise<RepositoryConfiguration> => {
    const response = await apiClient.post<RepositoryConfiguration>(
      `/configuration/clone`,
      request
    );
    return response.data;
  },

  // Validate configuration
  validateConfiguration: async (
    configuration: RepositoryConfiguration
  ): Promise<ConfigurationValidationResult> => {
    const response = await apiClient.post<ConfigurationValidationResult>(
      `/configuration/validate`,
      configuration
    );
    return response.data;
  },

  // Export configuration
  exportConfiguration: async (
    organization: string,
    project: string,
    repository: string
  ): Promise<string> => {
    const response = await apiClient.get(
      `/configuration/${encodeURIComponent(organization)}/${encodeURIComponent(project)}/${encodeURIComponent(repository)}/export`,
      {
        responseType: 'text',
      }
    );
    return response.data;
  },

  // Import configuration
  importConfiguration: async (
    request: ImportConfigurationRequest
  ): Promise<RepositoryConfiguration> => {
    const response = await apiClient.post<RepositoryConfiguration>(
      `/configuration/import`,
      request
    );
    return response.data;
  },
};

// Utility functions
export const handleApiError = (error: any): string => {
  if (error.response?.data?.message) {
    return error.response.data.message;
  }
  if (error.response?.data?.errors) {
    const errors = error.response.data.errors;
    if (Array.isArray(errors)) {
      return errors.map((e: any) => e.message || e).join(', ');
    }
    return 'Validation errors occurred';
  }
  if (error.message) {
    return error.message;
  }
  return 'An unexpected error occurred';
};

export { apiClient };