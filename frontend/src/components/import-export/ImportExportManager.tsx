import React, { useState } from 'react';
import {
  Modal,
  Button,
  Upload,
  message,
  Form,
  Input,
  Select,
  Space,
  Typography,
  Alert,
  Divider,
  Card,
} from 'antd';
import {
  UploadOutlined,
  DownloadOutlined,
  CopyOutlined,
  ImportOutlined,
  ExportOutlined,
} from '@ant-design/icons';
import type { UploadProps } from 'antd';
import {
  useImportConfiguration,
  useExportConfiguration,
  useCloneConfiguration,
} from '../../hooks/useConfiguration';
import { validateConfiguration } from '../../utils/validation';
import type { RepositoryConfiguration, CloneConfigurationRequest } from '../../types/configuration';

const { Text, Paragraph } = Typography;
const { TextArea } = Input;
const { Option } = Select;

interface ImportExportManagerProps {
  visible: boolean;
  onClose: () => void;
  currentConfig?: RepositoryConfiguration;
}

const ImportExportManager: React.FC<ImportExportManagerProps> = ({
  visible,
  onClose,
  currentConfig,
}) => {
  const [activeTab, setActiveTab] = useState<'import' | 'export' | 'clone'>('import');
  const [importText, setImportText] = useState('');
  const [cloneForm] = Form.useForm();

  const importMutation = useImportConfiguration();
  const exportMutation = useExportConfiguration();
  const cloneMutation = useCloneConfiguration();

  const handleFileUpload: UploadProps['beforeUpload'] = (file) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      const content = e.target?.result as string;
      setImportText(content);
      message.success(`${file.name} loaded successfully`);
    };
    reader.readAsText(file);
    return false; // Prevent automatic upload
  };

  const handleImport = async () => {
    if (!importText.trim()) {
      message.error('Please provide configuration JSON to import');
      return;
    }

    try {
      // Validate JSON first
      const parsedConfig = JSON.parse(importText);
      const validationResult = validateConfiguration(parsedConfig);

      if (!validationResult.success) {
        Modal.error({
          title: 'Invalid Configuration',
          content: (
            <div>
              <Text>The configuration contains validation errors:</Text>
              <ul>
                {validationResult.errors?.map((error, index) => (
                  <li key={index}>
                    <Text strong>{error.path}:</Text> {error.message}
                  </li>
                ))}
              </ul>
            </div>
          ),
        });
        return;
      }

      await importMutation.mutateAsync({ configurationJson: importText });
      onClose();
      setImportText('');
    } catch (error) {
      if (error instanceof SyntaxError) {
        message.error('Invalid JSON format. Please check your configuration file.');
      } else {
        message.error('Failed to import configuration');
      }
    }
  };

  const handleExport = async () => {
    if (!currentConfig) {
      message.error('No configuration to export');
      return;
    }

    try {
      await exportMutation.mutateAsync({
        organization: currentConfig.organization,
        project: currentConfig.project,
        repository: currentConfig.repository,
      });
    } catch (error) {
      message.error('Failed to export configuration');
    }
  };

  const handleClone = async () => {
    try {
      const values = await cloneForm.validateFields();
      const request: CloneConfigurationRequest = {
        sourceOrganization: values.sourceOrganization,
        sourceProject: values.sourceProject,
        sourceRepository: values.sourceRepository,
        targetOrganization: values.targetOrganization,
        targetProject: values.targetProject,
        targetRepository: values.targetRepository,
        overwriteExisting: values.overwriteExisting || false,
      };

      await cloneMutation.mutateAsync(request);
      onClose();
      cloneForm.resetFields();
    } catch (error) {
      console.error('Clone validation failed:', error);
    }
  };

  const exportToClipboard = async () => {
    if (!currentConfig) {
      message.error('No configuration to export');
      return;
    }

    try {
      const configJson = JSON.stringify(currentConfig, null, 2);
      await navigator.clipboard.writeText(configJson);
      message.success('Configuration copied to clipboard');
    } catch (error) {
      message.error('Failed to copy to clipboard');
    }
  };

  const tabContent = {
    import: (
      <div>
        <Alert
          message="Import Configuration"
          description="Import a configuration from a JSON file or text. This will overwrite the current configuration."
          type="warning"
          style={{ marginBottom: '16px' }}
          showIcon
        />

        <Space direction="vertical" style={{ width: '100%' }}>
          <div>
            <Text strong>Upload from file:</Text>
            <Upload
              accept=".json"
              beforeUpload={handleFileUpload}
              showUploadList={false}
              style={{ marginTop: '8px' }}
            >
              <Button icon={<UploadOutlined />}>Select JSON File</Button>
            </Upload>
          </div>

          <Divider>or</Divider>

          <div>
            <Text strong>Paste configuration JSON:</Text>
            <TextArea
              value={importText}
              onChange={(e) => setImportText(e.target.value)}
              placeholder="Paste your configuration JSON here..."
              rows={12}
              style={{ marginTop: '8px' }}
              className="code-editor"
            />
          </div>

          <Space style={{ marginTop: '16px' }}>
            <Button
              type="primary"
              icon={<ImportOutlined />}
              onClick={handleImport}
              loading={importMutation.isPending}
              disabled={!importText.trim()}
            >
              Import Configuration
            </Button>
            <Button onClick={() => setImportText('')}>Clear</Button>
          </Space>
        </Space>
      </div>
    ),

    export: (
      <div>
        <Alert
          message="Export Configuration"
          description="Export the current configuration as a JSON file or copy it to the clipboard."
          type="info"
          style={{ marginBottom: '16px' }}
          showIcon
        />

        <Space direction="vertical" style={{ width: '100%' }}>
          <Card>
            <Space direction="vertical" style={{ width: '100%' }}>
              <Text strong>Export Options:</Text>

              <Space wrap>
                <Button
                  type="primary"
                  icon={<ExportOutlined />}
                  onClick={handleExport}
                  loading={exportMutation.isPending}
                  disabled={!currentConfig}
                >
                  Download as File
                </Button>

                <Button
                  icon={<CopyOutlined />}
                  onClick={exportToClipboard}
                  disabled={!currentConfig}
                >
                  Copy to Clipboard
                </Button>
              </Space>

              {currentConfig && (
                <div style={{ marginTop: '16px' }}>
                  <Text type="secondary">
                    Configuration: {currentConfig.organization}/{currentConfig.project}/{currentConfig.repository}
                    <br />
                    Version: {currentConfig.version} | Last updated: {new Date(currentConfig.updatedAt).toLocaleDateString()}
                  </Text>
                </div>
              )}
            </Space>
          </Card>
        </Space>
      </div>
    ),

    clone: (
      <div>
        <Alert
          message="Clone Configuration"
          description="Copy a configuration from one repository to another. This is useful for applying the same settings across multiple repositories."
          type="info"
          style={{ marginBottom: '16px' }}
          showIcon
        />

        <Form form={cloneForm} layout="vertical">
          <div style={{ marginBottom: '24px' }}>
            <Text strong>Source Repository:</Text>
            <div style={{ display: 'flex', gap: '8px', marginTop: '8px' }}>
              <Form.Item
                name="sourceOrganization"
                rules={[{ required: true, message: 'Required' }]}
                style={{ flex: 1, marginBottom: 0 }}
              >
                <Input placeholder="Source organization" />
              </Form.Item>
              <Form.Item
                name="sourceProject"
                rules={[{ required: true, message: 'Required' }]}
                style={{ flex: 1, marginBottom: 0 }}
              >
                <Input placeholder="Source project" />
              </Form.Item>
              <Form.Item
                name="sourceRepository"
                rules={[{ required: true, message: 'Required' }]}
                style={{ flex: 1, marginBottom: 0 }}
              >
                <Input placeholder="Source repository" />
              </Form.Item>
            </div>
          </div>

          <div style={{ marginBottom: '24px' }}>
            <Text strong>Target Repository:</Text>
            <div style={{ display: 'flex', gap: '8px', marginTop: '8px' }}>
              <Form.Item
                name="targetOrganization"
                rules={[{ required: true, message: 'Required' }]}
                style={{ flex: 1, marginBottom: 0 }}
              >
                <Input placeholder="Target organization" />
              </Form.Item>
              <Form.Item
                name="targetProject"
                rules={[{ required: true, message: 'Required' }]}
                style={{ flex: 1, marginBottom: 0 }}
              >
                <Input placeholder="Target project" />
              </Form.Item>
              <Form.Item
                name="targetRepository"
                rules={[{ required: true, message: 'Required' }]}
                style={{ flex: 1, marginBottom: 0 }}
              >
                <Input placeholder="Target repository" />
              </Form.Item>
            </div>
          </div>

          <Form.Item
            name="overwriteExisting"
            valuePropName="checked"
            style={{ marginBottom: '24px' }}
          >
            <input type="checkbox" id="overwrite" style={{ marginRight: '8px' }} />
            <label htmlFor="overwrite">
              Overwrite existing configuration (if target already has a configuration)
            </label>
          </Form.Item>

          <Space>
            <Button
              type="primary"
              icon={<CopyOutlined />}
              onClick={handleClone}
              loading={cloneMutation.isPending}
            >
              Clone Configuration
            </Button>
            <Button onClick={() => cloneForm.resetFields()}>Reset</Button>
          </Space>
        </Form>
      </div>
    ),
  };

  return (
    <Modal
      title="Import / Export / Clone Configuration"
      open={visible}
      onCancel={onClose}
      footer={null}
      width={800}
      destroyOnClose
    >
      <div style={{ marginBottom: '16px' }}>
        <Space>
          <Button
            type={activeTab === 'import' ? 'primary' : 'default'}
            onClick={() => setActiveTab('import')}
            icon={<ImportOutlined />}
          >
            Import
          </Button>
          <Button
            type={activeTab === 'export' ? 'primary' : 'default'}
            onClick={() => setActiveTab('export')}
            icon={<ExportOutlined />}
          >
            Export
          </Button>
          <Button
            type={activeTab === 'clone' ? 'primary' : 'default'}
            onClick={() => setActiveTab('clone')}
            icon={<CopyOutlined />}
          >
            Clone
          </Button>
        </Space>
      </div>

      <Divider />

      {tabContent[activeTab]}
    </Modal>
  );
};

export default ImportExportManager;