import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
  Card,
  Form,
  Button,
  Breadcrumb,
  Space,
  Typography,
  Alert,
  Modal,
  message,
  Spin,
  Tabs,
  Switch,
  Divider,
} from 'antd';
import {
  ArrowLeftOutlined,
  SaveOutlined,
  DeleteOutlined,
  ExportOutlined,
  ImportOutlined,
  CopyOutlined,
  ReloadOutlined,
} from '@ant-design/icons';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  useConfiguration,
  useCreateDefaultConfiguration,
  useSaveConfiguration,
  useDeleteConfiguration,
  useExportConfiguration,
  useValidateConfiguration,
} from '../hooks/useConfiguration';
import { repositoryConfigurationSchema } from '../utils/validation';
import BasicSettingsForm from '../components/forms/BasicSettingsForm';
import WebhookSettingsForm from '../components/forms/WebhookSettingsForm';
import CommentSettingsForm from '../components/forms/CommentSettingsForm';
import ReviewStrategyForm from '../components/forms/ReviewStrategyForm';
import QuerySettingsForm from '../components/forms/QuerySettingsForm';
import ReviewRulesManager from '../components/rules/ReviewRulesManager';
import FileExclusionRulesManager from '../components/rules/FileExclusionRulesManager';
import CustomPromptsManager from '../components/prompts/CustomPromptsManager';
import ImportExportManager from '../components/import-export/ImportExportManager';
import type { RepositoryConfiguration } from '../types/configuration';

const { Title, Text } = Typography;

const ConfigurationEditor: React.FC = () => {
  const { organization, project, repository } = useParams<{
    organization: string;
    project: string;
    repository: string;
  }>();
  const navigate = useNavigate();
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const [importExportVisible, setImportExportVisible] = useState(false);

  // API hooks
  const {
    data: configuration,
    isLoading,
    error,
  } = useConfiguration(organization!, project!, repository!, {
    enabled: !!(organization && project && repository),
  });

  const createDefaultMutation = useCreateDefaultConfiguration();
  const saveMutation = useSaveConfiguration();
  const deleteMutation = useDeleteConfiguration();
  const exportMutation = useExportConfiguration();
  const validateMutation = useValidateConfiguration();

  // Form setup
  const form = useForm<RepositoryConfiguration>({
    resolver: zodResolver(repositoryConfigurationSchema),
    mode: 'onChange',
  });

  const { handleSubmit, reset, watch, formState } = form;
  const { isDirty, isValid } = formState;

  // Watch for changes to show unsaved changes indicator
  useEffect(() => {
    setHasUnsavedChanges(isDirty);
  }, [isDirty]);

  // Load configuration data into form
  useEffect(() => {
    if (configuration) {
      reset(configuration);
      setHasUnsavedChanges(false);
    }
  }, [configuration, reset]);

  // Handle form submission
  const onSubmit = async (data: RepositoryConfiguration) => {
    try {
      await saveMutation.mutateAsync(data);
      setHasUnsavedChanges(false);
    } catch (error) {
      // Error handling is done in the hook
    }
  };

  // Handle creating default configuration
  const handleCreateDefault = async () => {
    if (!organization || !project || !repository) return;

    try {
      await createDefaultMutation.mutateAsync({
        organization,
        project,
        repository,
      });
    } catch (error) {
      // Error handling is done in the hook
    }
  };

  // Handle configuration deletion
  const handleDelete = () => {
    if (!organization || !project || !repository) return;

    Modal.confirm({
      title: 'Delete Configuration',
      content: `Are you sure you want to delete the configuration for ${organization}/${project}/${repository}? This action cannot be undone.`,
      okText: 'Delete',
      okType: 'danger',
      onOk: async () => {
        try {
          await deleteMutation.mutateAsync({
            organization,
            project,
            repository,
          });
          navigate('/repositories');
        } catch (error) {
          // Error handling is done in the hook
        }
      },
    });
  };

  // Handle configuration export
  const handleExport = async () => {
    if (!organization || !project || !repository) return;

    try {
      await exportMutation.mutateAsync({
        organization,
        project,
        repository,
      });
    } catch (error) {
      // Error handling is done in the hook
    }
  };

  // Handle configuration validation
  const handleValidate = async () => {
    const currentData = form.getValues();
    try {
      const result = await validateMutation.mutateAsync(currentData);
      if (result.isValid) {
        message.success('Configuration is valid');
      } else {
        Modal.error({
          title: 'Configuration Validation Failed',
          content: (
            <div>
              <Text>The following issues were found:</Text>
              <ul>
                {result.errors.map((error, index) => (
                  <li key={index}>
                    <Text strong>{error.propertyPath}:</Text> {error.message}
                  </li>
                ))}
              </ul>
              {result.warnings.length > 0 && (
                <>
                  <Text>Warnings:</Text>
                  <ul>
                    {result.warnings.map((warning, index) => (
                      <li key={index}>
                        <Text strong>{warning.propertyPath}:</Text> {warning.message}
                      </li>
                    ))}
                  </ul>
                </>
              )}
            </div>
          ),
        });
      }
    } catch (error) {
      // Error handling is done in the hook
    }
  };

  // Handle navigation back
  const handleBack = () => {
    if (hasUnsavedChanges) {
      Modal.confirm({
        title: 'Unsaved Changes',
        content: 'You have unsaved changes. Are you sure you want to leave?',
        onOk: () => navigate('/repositories'),
      });
    } else {
      navigate('/repositories');
    }
  };

  if (!organization || !project || !repository) {
    return (
      <Alert
        message="Invalid Parameters"
        description="Organization, project, and repository parameters are required."
        type="error"
        showIcon
      />
    );
  }

  if (isLoading) {
    return (
      <div className="loading-center" style={{ height: '400px' }}>
        <Spin size="large" />
        <Text style={{ marginTop: '16px', display: 'block', textAlign: 'center' }}>
          Loading configuration...
        </Text>
      </div>
    );
  }

  if (error && (error as any)?.response?.status === 404) {
    return (
      <Card>
        <div style={{ textAlign: 'center', padding: '40px' }}>
          <Title level={3}>Configuration Not Found</Title>
          <Text type="secondary" style={{ display: 'block', marginBottom: '24px' }}>
            No configuration exists for {organization}/{project}/{repository}
          </Text>
          <Space>
            <Button icon={<ArrowLeftOutlined />} onClick={handleBack}>
              Back to Repositories
            </Button>
            <Button
              type="primary"
              icon={<CopyOutlined />}
              onClick={handleCreateDefault}
              loading={createDefaultMutation.isPending}
            >
              Create Default Configuration
            </Button>
          </Space>
        </div>
      </Card>
    );
  }

  if (error) {
    return (
      <Alert
        message="Error Loading Configuration"
        description="Failed to load the configuration. Please try again."
        type="error"
        showIcon
        action={
          <Button size="small" onClick={() => window.location.reload()}>
            Retry
          </Button>
        }
      />
    );
  }

  const tabItems = [
    {
      key: 'basic',
      label: 'Basic Settings',
      children: <BasicSettingsForm form={form} />,
    },
    {
      key: 'webhook',
      label: 'Webhook Settings',
      children: <WebhookSettingsForm form={form} />,
    },
    {
      key: 'comments',
      label: 'Comment Settings',
      children: <CommentSettingsForm form={form} />,
    },
    {
      key: 'strategy',
      label: 'Review Strategy',
      children: <ReviewStrategyForm form={form} />,
    },
    {
      key: 'query',
      label: 'Query Settings',
      children: <QuerySettingsForm form={form} />,
    },
    {
      key: 'rules',
      label: 'Review Rules',
      children: <ReviewRulesManager form={form} />,
    },
    {
      key: 'exclusions',
      label: 'File Exclusions',
      children: <FileExclusionRulesManager form={form} />,
    },
    {
      key: 'prompts',
      label: 'Custom Prompts',
      children: <CustomPromptsManager form={form} />,
    },
  ];

  return (
    <div style={{ maxWidth: '1200px', margin: '0 auto' }}>
      {/* Breadcrumb Navigation */}
      <div className="repository-breadcrumb">
        <Breadcrumb
          items={[
            { title: 'Repositories', onClick: handleBack, className: 'cursor-pointer' },
            { title: organization },
            { title: project },
            { title: repository },
          ]}
        />
      </div>

      {/* Header */}
      <Card style={{ marginBottom: '16px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div>
            <Title level={3} style={{ margin: 0 }}>
              Configuration Editor
            </Title>
            <Text type="secondary">
              {organization}/{project}/{repository}
              {hasUnsavedChanges && (
                <Text type="warning" style={{ marginLeft: '8px' }}>
                  • Unsaved changes
                </Text>
              )}
            </Text>
          </div>

          <Space>
            <Switch
              checked={watch('isEnabled')}
              onChange={(checked) => form.setValue('isEnabled', checked)}
              checkedChildren="Enabled"
              unCheckedChildren="Disabled"
            />
            <Button icon={<ArrowLeftOutlined />} onClick={handleBack}>
              Back
            </Button>
            <Button
              icon={<ReloadOutlined />}
              onClick={handleValidate}
              loading={validateMutation.isPending}
            >
              Validate
            </Button>
            <Button
              icon={<ImportOutlined />}
              onClick={() => setImportExportVisible(true)}
            >
              Import/Export
            </Button>
            <Button
              type="primary"
              icon={<SaveOutlined />}
              onClick={handleSubmit(onSubmit)}
              loading={saveMutation.isPending}
              disabled={!isValid}
            >
              Save Configuration
            </Button>
          </Space>
        </div>
      </Card>

      {/* Configuration Form */}
      <Card>
        <Form layout="vertical">
          <Tabs items={tabItems} />
        </Form>
      </Card>

      {/* Danger Zone */}
      {configuration && (
        <Card title="Danger Zone" style={{ marginTop: '24px', borderColor: '#ff4d4f' }}>
          <Space direction="vertical" style={{ width: '100%' }}>
            <Text type="secondary">
              Permanently delete this configuration. This action cannot be undone.
            </Text>
            <Button
              type="primary"
              danger
              icon={<DeleteOutlined />}
              onClick={handleDelete}
              loading={deleteMutation.isPending}
            >
              Delete Configuration
            </Button>
          </Space>
        </Card>
      )}

      {/* Import/Export Modal */}
      <ImportExportManager
        visible={importExportVisible}
        onClose={() => setImportExportVisible(false)}
        currentConfig={configuration}
      />
    </div>
  );
};

export default ConfigurationEditor;