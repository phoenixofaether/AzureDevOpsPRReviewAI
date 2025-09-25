import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { message } from 'antd';
import { configurationApi, handleApiError } from '../services/api';
import type {
  RepositoryConfiguration,
  CloneConfigurationRequest,
  ImportConfigurationRequest,
} from '../types/configuration';

// Hook for getting a single configuration
export const useConfiguration = (
  organization: string,
  project: string,
  repository: string,
  options?: { enabled?: boolean }
) => {
  return useQuery({
    queryKey: ['configuration', organization, project, repository],
    queryFn: () => configurationApi.getConfiguration(organization, project, repository),
    enabled: options?.enabled ?? true,
    retry: (failureCount, error: any) => {
      // Don't retry on 404 errors
      if (error?.response?.status === 404) return false;
      return failureCount < 3;
    },
  });
};

// Hook for getting effective configuration (with defaults)
export const useEffectiveConfiguration = (
  organization: string,
  project: string,
  repository: string
) => {
  return useQuery({
    queryKey: ['effective-configuration', organization, project, repository],
    queryFn: () =>
      configurationApi.getEffectiveConfiguration(organization, project, repository),
  });
};

// Hook for saving configuration
export const useSaveConfiguration = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (configuration: RepositoryConfiguration) =>
      configurationApi.saveConfiguration(configuration),
    onSuccess: (data) => {
      // Invalidate and refetch configuration queries
      queryClient.invalidateQueries({
        queryKey: ['configuration', data.organization, data.project, data.repository],
      });
      queryClient.invalidateQueries({
        queryKey: [
          'effective-configuration',
          data.organization,
          data.project,
          data.repository,
        ],
      });
      queryClient.invalidateQueries({
        queryKey: ['organization-configs', data.organization],
      });
      queryClient.invalidateQueries({
        queryKey: ['project-configs', data.organization, data.project],
      });
      message.success('Configuration saved successfully');
    },
    onError: (error) => {
      const errorMessage = handleApiError(error);
      message.error(errorMessage);
    },
  });
};

// Hook for updating configuration
export const useUpdateConfiguration = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      organization,
      project,
      repository,
      configuration,
    }: {
      organization: string;
      project: string;
      repository: string;
      configuration: RepositoryConfiguration;
    }) => configurationApi.updateConfiguration(organization, project, repository, configuration),
    onSuccess: (data) => {
      queryClient.invalidateQueries({
        queryKey: ['configuration', data.organization, data.project, data.repository],
      });
      queryClient.invalidateQueries({
        queryKey: [
          'effective-configuration',
          data.organization,
          data.project,
          data.repository,
        ],
      });
      message.success('Configuration updated successfully');
    },
    onError: (error) => {
      const errorMessage = handleApiError(error);
      message.error(errorMessage);
    },
  });
};

// Hook for deleting configuration
export const useDeleteConfiguration = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      organization,
      project,
      repository,
    }: {
      organization: string;
      project: string;
      repository: string;
    }) => configurationApi.deleteConfiguration(organization, project, repository),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({
        queryKey: ['configuration', variables.organization, variables.project, variables.repository],
      });
      queryClient.invalidateQueries({
        queryKey: ['organization-configs', variables.organization],
      });
      queryClient.invalidateQueries({
        queryKey: ['project-configs', variables.organization, variables.project],
      });
      message.success('Configuration deleted successfully');
    },
    onError: (error) => {
      const errorMessage = handleApiError(error);
      message.error(errorMessage);
    },
  });
};

// Hook for creating default configuration
export const useCreateDefaultConfiguration = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({
      organization,
      project,
      repository,
    }: {
      organization: string;
      project: string;
      repository: string;
    }) => configurationApi.createDefaultConfiguration(organization, project, repository),
    onSuccess: (data) => {
      queryClient.setQueryData(
        ['configuration', data.organization, data.project, data.repository],
        data
      );
      queryClient.invalidateQueries({
        queryKey: ['organization-configs', data.organization],
      });
      queryClient.invalidateQueries({
        queryKey: ['project-configs', data.organization, data.project],
      });
      message.success('Default configuration created successfully');
    },
    onError: (error) => {
      const errorMessage = handleApiError(error);
      message.error(errorMessage);
    },
  });
};

// Hook for cloning configuration
export const useCloneConfiguration = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CloneConfigurationRequest) =>
      configurationApi.cloneConfiguration(request),
    onSuccess: (data) => {
      queryClient.setQueryData(
        ['configuration', data.organization, data.project, data.repository],
        data
      );
      queryClient.invalidateQueries({
        queryKey: ['organization-configs', data.organization],
      });
      queryClient.invalidateQueries({
        queryKey: ['project-configs', data.organization, data.project],
      });
      message.success('Configuration cloned successfully');
    },
    onError: (error) => {
      const errorMessage = handleApiError(error);
      message.error(errorMessage);
    },
  });
};

// Hook for validating configuration
export const useValidateConfiguration = () => {
  return useMutation({
    mutationFn: (configuration: RepositoryConfiguration) =>
      configurationApi.validateConfiguration(configuration),
    onError: (error) => {
      const errorMessage = handleApiError(error);
      message.error(errorMessage);
    },
  });
};

// Hook for exporting configuration
export const useExportConfiguration = () => {
  return useMutation({
    mutationFn: ({
      organization,
      project,
      repository,
    }: {
      organization: string;
      project: string;
      repository: string;
    }) => configurationApi.exportConfiguration(organization, project, repository),
    onSuccess: (data, variables) => {
      // Create and trigger download
      const blob = new Blob([data], { type: 'application/json' });
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${variables.organization}_${variables.project}_${variables.repository}_config.json`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
      message.success('Configuration exported successfully');
    },
    onError: (error) => {
      const errorMessage = handleApiError(error);
      message.error(errorMessage);
    },
  });
};

// Hook for importing configuration
export const useImportConfiguration = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: ImportConfigurationRequest) =>
      configurationApi.importConfiguration(request),
    onSuccess: (data) => {
      queryClient.setQueryData(
        ['configuration', data.organization, data.project, data.repository],
        data
      );
      queryClient.invalidateQueries({
        queryKey: ['organization-configs', data.organization],
      });
      queryClient.invalidateQueries({
        queryKey: ['project-configs', data.organization, data.project],
      });
      message.success('Configuration imported successfully');
    },
    onError: (error) => {
      const errorMessage = handleApiError(error);
      message.error(errorMessage);
    },
  });
};