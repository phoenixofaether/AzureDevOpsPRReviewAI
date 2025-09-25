import React, { useState } from 'react';
import {
  Card,
  Button,
  List,
  Space,
  Typography,
  Tag,
  Switch,
  Modal,
  Form,
  Input,
  Select,
  InputNumber,
  Alert,
  Empty,
  Tooltip,
  Collapse,
} from 'antd';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  InfoCircleOutlined,
  EyeOutlined,
  CodeOutlined,
  UpOutlined,
  DownOutlined,
} from '@ant-design/icons';
import { UseFormReturn, useFieldArray } from 'react-hook-form';
import { CustomPrompt, PromptType, PromptScope } from '../../types/configuration';
import type { RepositoryConfiguration } from '../../types/configuration';

const { Text, Paragraph } = Typography;
const { Option } = Select;
const { TextArea } = Input;
const { Panel } = Collapse;

interface CustomPromptsManagerProps {
  form: UseFormReturn<RepositoryConfiguration>;
}

interface PromptFormData {
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
}

const CustomPromptsManager: React.FC<CustomPromptsManagerProps> = ({ form }) => {
  const { control } = form;
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [previewVisible, setPreviewVisible] = useState(false);
  const [previewContent, setPreviewContent] = useState('');
  const [modalForm] = Form.useForm();

  const { fields, append, update, remove, move } = useFieldArray({
    control,
    name: 'customPrompts',
  });

  const promptTypeDescriptions = {
    [PromptType.CodeAnalysis]: 'General code analysis and quality review',
    [PromptType.SecurityAnalysis]: 'Security vulnerability detection and analysis',
    [PromptType.PerformanceAnalysis]: 'Performance optimization and efficiency review',
    [PromptType.DocumentationReview]: 'Documentation quality and completeness review',
    [PromptType.TestAnalysis]: 'Test coverage and testing best practices',
    [PromptType.ArchitectureReview]: 'Architectural patterns and design review',
    [PromptType.StyleReview]: 'Code style and formatting review',
    [PromptType.Summary]: 'Summary generation for pull requests',
    [PromptType.Custom]: 'User-defined custom analysis',
  };

  const promptScopeDescriptions = {
    [PromptScope.Organization]: 'Applied across all repositories in the organization',
    [PromptScope.Project]: 'Applied to all repositories in the project',
    [PromptScope.Repository]: 'Applied only to this repository',
    [PromptScope.FileType]: 'Applied to specific file types only',
  };

  const promptTypeColors = {
    [PromptType.CodeAnalysis]: 'blue',
    [PromptType.SecurityAnalysis]: 'red',
    [PromptType.PerformanceAnalysis]: 'orange',
    [PromptType.DocumentationReview]: 'green',
    [PromptType.TestAnalysis]: 'purple',
    [PromptType.ArchitectureReview]: 'geekblue',
    [PromptType.StyleReview]: 'cyan',
    [PromptType.Summary]: 'magenta',
    [PromptType.Custom]: 'default',
  };

  const promptScopeColors = {
    [PromptScope.Organization]: 'gold',
    [PromptScope.Project]: 'lime',
    [PromptScope.Repository]: 'blue',
    [PromptScope.FileType]: 'volcano',
  };

  const defaultPromptTemplates = {
    [PromptType.CodeAnalysis]: `You are a code reviewer analyzing the following code changes. Please review the code for:

1. **Code Quality**: Look for maintainability, readability, and best practices
2. **Potential Bugs**: Identify any logical errors or potential runtime issues
3. **Design Patterns**: Check if appropriate patterns are used
4. **Error Handling**: Verify proper error handling and edge cases

Variables available:
- {{fileName}}: Current file name
- {{fileType}}: File extension
- {{changedLines}}: Number of changed lines
- {{author}}: Pull request author

Provide specific, actionable feedback with line references when possible.`,

    [PromptType.SecurityAnalysis]: `You are a security-focused code reviewer. Analyze the following code changes for:

1. **Security Vulnerabilities**: SQL injection, XSS, CSRF, etc.
2. **Authentication & Authorization**: Proper access controls
3. **Data Validation**: Input sanitization and validation
4. **Secrets Management**: Check for hardcoded secrets or credentials
5. **Dependencies**: Identify vulnerable or outdated packages

Variables available:
- {{fileName}}: Current file name
- {{sensitivePatterns}}: Patterns that might contain secrets

Focus on high-impact security issues and provide remediation suggestions.`,

    [PromptType.PerformanceAnalysis]: `You are a performance optimization specialist reviewing code changes. Focus on:

1. **Algorithm Efficiency**: Time and space complexity
2. **Database Queries**: N+1 problems, missing indexes
3. **Caching Opportunities**: Where caching could improve performance
4. **Resource Management**: Memory leaks, connection pooling
5. **Async/Parallel Processing**: Opportunities for concurrency

Variables available:
- {{complexity}}: Cyclomatic complexity score
- {{linesOfCode}}: Total lines in the file

Suggest specific optimizations with measurable impact.`,
  };

  const commonVariables = [
    '{{fileName}}',
    '{{fileType}}',
    '{{changedLines}}',
    '{{author}}',
    '{{prTitle}}',
    '{{prDescription}}',
    '{{branchName}}',
    '{{complexity}}',
    '{{linesOfCode}}',
  ];

  const openModal = (index?: number) => {
    setEditingIndex(index !== undefined ? index : null);

    if (index !== undefined) {
      const prompt = fields[index];
      modalForm.setFieldsValue({
        name: prompt.name,
        description: prompt.description,
        type: prompt.type,
        template: prompt.template,
        isEnabled: prompt.isEnabled,
        supportedLanguages: prompt.supportedLanguages.join(', '),
        supportedFileExtensions: prompt.supportedFileExtensions.join(', '),
        variables: JSON.stringify(prompt.variables, null, 2),
        scope: prompt.scope,
        priority: prompt.priority,
      });
    } else {
      modalForm.resetFields();
      modalForm.setFieldsValue({
        type: PromptType.CodeAnalysis,
        isEnabled: true,
        scope: PromptScope.Repository,
        priority: 0,
        template: defaultPromptTemplates[PromptType.CodeAnalysis],
        variables: '{}',
      });
    }

    setIsModalVisible(true);
  };

  const handleModalOk = async () => {
    try {
      const values = await modalForm.validateFields();

      const promptData: CustomPrompt = {
        id: editingIndex !== null ? fields[editingIndex].id : `prompt_${Date.now()}`,
        name: values.name,
        description: values.description,
        type: values.type,
        template: values.template,
        isEnabled: values.isEnabled,
        supportedLanguages: values.supportedLanguages
          ? values.supportedLanguages.split(',').map((lang: string) => lang.trim()).filter(Boolean)
          : [],
        supportedFileExtensions: values.supportedFileExtensions
          ? values.supportedFileExtensions.split(',').map((ext: string) => ext.trim()).filter(Boolean)
          : [],
        variables: values.variables ? JSON.parse(values.variables) : {},
        scope: values.scope,
        priority: values.priority,
        createdAt: editingIndex !== null ? fields[editingIndex].createdAt : new Date().toISOString(),
        updatedAt: new Date().toISOString(),
        createdBy: editingIndex !== null ? fields[editingIndex].createdBy : undefined,
        updatedBy: 'User',
      };

      if (editingIndex !== null) {
        update(editingIndex, promptData);
      } else {
        append(promptData);
      }

      setIsModalVisible(false);
      modalForm.resetFields();
      setEditingIndex(null);
    } catch (error) {
      console.error('Form validation failed:', error);
    }
  };

  const handleDelete = (index: number) => {
    Modal.confirm({
      title: 'Delete Custom Prompt',
      content: `Are you sure you want to delete the prompt "${fields[index].name}"?`,
      okText: 'Delete',
      okType: 'danger',
      onOk: () => remove(index),
    });
  };

  const togglePrompt = (index: number) => {
    const prompt = fields[index];
    update(index, { ...prompt, isEnabled: !prompt.isEnabled });
  };

  const movePrompt = (fromIndex: number, toIndex: number) => {
    if (toIndex >= 0 && toIndex < fields.length) {
      move(fromIndex, toIndex);
    }
  };

  const showPreview = (template: string) => {
    setPreviewContent(template);
    setPreviewVisible(true);
  };

  const handleTypeChange = (type: PromptType) => {
    if (defaultPromptTemplates[type]) {
      Modal.confirm({
        title: 'Load Default Template',
        content: `Would you like to load the default template for ${type}? This will replace the current template content.`,
        onOk: () => {
          modalForm.setFieldsValue({
            template: defaultPromptTemplates[type],
          });
        },
      });
    }
  };

  const selectedType = Form.useWatch('type', modalForm);

  return (
    <div>
      <Card className="configuration-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <div>
            <div className="form-section-title" style={{ marginBottom: '8px' }}>
              Custom Prompts ({fields.length})
            </div>
            <Text type="secondary">
              Define custom AI prompts for specialized code review tasks
            </Text>
          </div>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openModal()}>
            Add Custom Prompt
          </Button>
        </div>

        <Alert
          message="Prompt Variables"
          description={
            <div>
              <Text>You can use these variables in your prompt templates: </Text>
              <br />
              <Text code>{commonVariables.join(', ')}</Text>
            </div>
          }
          type="info"
          style={{ marginBottom: '16px' }}
          showIcon
        />

        {fields.length === 0 ? (
          <Empty
            description="No custom prompts configured"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          >
            <Button type="primary" icon={<PlusOutlined />} onClick={() => openModal()}>
              Create First Prompt
            </Button>
          </Empty>
        ) : (
          <List
            dataSource={fields}
            renderItem={(prompt, index) => (
              <List.Item
                className={`rule-list-item ${!prompt.isEnabled ? 'rule-list-item-disabled' : ''}`}
                actions={[
                  <Button
                    key="toggle"
                    type="text"
                    onClick={() => togglePrompt(index)}
                  >
                    <Switch
                      checked={prompt.isEnabled}
                      size="small"
                      onClick={(checked, e) => {
                        e.stopPropagation();
                        togglePrompt(index);
                      }}
                    />
                  </Button>,
                  <Button
                    key="preview"
                    type="text"
                    icon={<EyeOutlined />}
                    onClick={() => showPreview(prompt.template)}
                    size="small"
                  />,
                  <Button
                    key="up"
                    type="text"
                    icon={<UpOutlined />}
                    onClick={() => movePrompt(index, index - 1)}
                    disabled={index === 0}
                    size="small"
                  />,
                  <Button
                    key="down"
                    type="text"
                    icon={<DownOutlined />}
                    onClick={() => movePrompt(index, index + 1)}
                    disabled={index === fields.length - 1}
                    size="small"
                  />,
                  <Button
                    key="edit"
                    type="text"
                    icon={<EditOutlined />}
                    onClick={() => openModal(index)}
                    size="small"
                  />,
                  <Button
                    key="delete"
                    type="text"
                    danger
                    icon={<DeleteOutlined />}
                    onClick={() => handleDelete(index)}
                    size="small"
                  />,
                ]}
                style={{ padding: '12px 16px' }}
              >
                <List.Item.Meta
                  title={
                    <Space>
                      <Text strong={prompt.isEnabled} type={prompt.isEnabled ? 'default' : 'secondary'}>
                        {prompt.name}
                      </Text>
                      <Tag color={promptTypeColors[prompt.type]}>
                        {prompt.type}
                      </Tag>
                      <Tag color={promptScopeColors[prompt.scope]}>
                        {prompt.scope}
                      </Tag>
                      <Tag color="blue">
                        Priority: {prompt.priority}
                      </Tag>
                    </Space>
                  }
                  description={
                    <Space direction="vertical" size="small" style={{ width: '100%' }}>
                      {prompt.description && (
                        <Text type="secondary" className="text-small">
                          {prompt.description}
                        </Text>
                      )}
                      <div>
                        <Text className="text-small">
                          <strong>Template Length:</strong> {prompt.template.length} characters
                        </Text>
                      </div>
                      {prompt.supportedLanguages.length > 0 && (
                        <div>
                          <Text className="text-small">
                            <strong>Languages:</strong> {prompt.supportedLanguages.join(', ')}
                          </Text>
                        </div>
                      )}
                      {prompt.supportedFileExtensions.length > 0 && (
                        <div>
                          <Text className="text-small">
                            <strong>File Types:</strong> {prompt.supportedFileExtensions.join(', ')}
                          </Text>
                        </div>
                      )}
                      {Object.keys(prompt.variables || {}).length > 0 && (
                        <div>
                          <Text className="text-small">
                            <strong>Custom Variables:</strong> {Object.keys(prompt.variables).length} defined
                          </Text>
                        </div>
                      )}
                    </Space>
                  }
                />
              </List.Item>
            )}
          />
        )}
      </Card>

      <Modal
        title={editingIndex !== null ? 'Edit Custom Prompt' : 'Add Custom Prompt'}
        open={isModalVisible}
        onOk={handleModalOk}
        onCancel={() => {
          setIsModalVisible(false);
          modalForm.resetFields();
          setEditingIndex(null);
        }}
        width={1000}
        destroyOnClose
      >
        <Form form={modalForm} layout="vertical">
          <Form.Item
            name="name"
            label="Prompt Name"
            rules={[
              { required: true, message: 'Please enter a prompt name' },
              { max: 100, message: 'Name must be less than 100 characters' },
            ]}
          >
            <Input placeholder="Enter descriptive prompt name" />
          </Form.Item>

          <Form.Item name="description" label="Description">
            <TextArea
              placeholder="Describe the purpose and use case of this prompt"
              rows={2}
              maxLength={500}
              showCount
            />
          </Form.Item>

          <div style={{ display: 'flex', gap: '16px' }}>
            <Form.Item
              name="type"
              label="Prompt Type"
              rules={[{ required: true, message: 'Please select a prompt type' }]}
              style={{ flex: 1 }}
            >
              <Select placeholder="Select prompt type" onChange={handleTypeChange}>
                {Object.values(PromptType).map((type) => (
                  <Option key={type} value={type}>
                    <Space>
                      <Tag color={promptTypeColors[type]} style={{ margin: 0 }}>
                        {type}
                      </Tag>
                      <span>{promptTypeDescriptions[type]}</span>
                    </Space>
                  </Option>
                ))}
              </Select>
            </Form.Item>

            <Form.Item
              name="scope"
              label="Prompt Scope"
              rules={[{ required: true, message: 'Please select a scope' }]}
              style={{ flex: 1 }}
            >
              <Select placeholder="Select scope">
                {Object.values(PromptScope).map((scope) => (
                  <Option key={scope} value={scope}>
                    <Space>
                      <Tag color={promptScopeColors[scope]} style={{ margin: 0 }}>
                        {scope}
                      </Tag>
                      <span>{promptScopeDescriptions[scope]}</span>
                    </Space>
                  </Option>
                ))}
              </Select>
            </Form.Item>

            <Form.Item
              name="priority"
              label="Priority"
              rules={[{ required: true, message: 'Please set priority' }]}
              style={{ flex: '0 0 150px' }}
            >
              <InputNumber
                min={-100}
                max={100}
                style={{ width: '100%' }}
                placeholder="Priority"
              />
            </Form.Item>
          </div>

          <Form.Item name="isEnabled" valuePropName="checked" style={{ marginBottom: '16px' }}>
            <Switch checkedChildren="Enabled" unCheckedChildren="Disabled" />
            <span style={{ marginLeft: '8px' }}>Prompt is active</span>
          </Form.Item>

          <Form.Item
            name="template"
            label={
              <Space>
                Prompt Template
                <Tooltip title="Write your prompt using variables like {{fileName}}, {{changedLines}}, etc.">
                  <InfoCircleOutlined />
                </Tooltip>
              </Space>
            }
            rules={[
              { required: true, message: 'Please enter a prompt template' },
              { min: 10, message: 'Template must be at least 10 characters long' },
            ]}
          >
            <TextArea
              placeholder="Enter your prompt template with variables..."
              rows={12}
              className="code-editor"
              maxLength={10000}
              showCount
            />
          </Form.Item>

          <Collapse ghost>
            <Panel header="Advanced Settings" key="advanced">
              <div style={{ display: 'flex', gap: '16px' }}>
                <Form.Item
                  name="supportedLanguages"
                  label="Supported Languages"
                  help="Comma-separated list (e.g., JavaScript, TypeScript, Python)"
                  style={{ flex: 1 }}
                >
                  <Input placeholder="JavaScript, TypeScript, Python" />
                </Form.Item>

                <Form.Item
                  name="supportedFileExtensions"
                  label="File Extensions"
                  help="Comma-separated list (e.g., .js, .ts, .py)"
                  style={{ flex: 1 }}
                >
                  <Input placeholder=".js, .ts, .jsx, .tsx" />
                </Form.Item>
              </div>

              <Form.Item
                name="variables"
                label="Custom Variables (JSON)"
                help="Define custom variables as key-value pairs in JSON format"
                rules={[
                  {
                    validator: (_, value) => {
                      if (!value || value.trim() === '') return Promise.resolve();
                      try {
                        const parsed = JSON.parse(value);
                        if (typeof parsed !== 'object' || Array.isArray(parsed)) {
                          return Promise.reject(new Error('Variables must be a JSON object'));
                        }
                        return Promise.resolve();
                      } catch {
                        return Promise.reject(new Error('Invalid JSON format'));
                      }
                    },
                  },
                ]}
              >
                <TextArea
                  placeholder='{ "maxComplexity": "10", "teamName": "Frontend Team" }'
                  rows={4}
                  className="code-editor"
                />
              </Form.Item>
            </Panel>
          </Collapse>
        </Form>
      </Modal>

      <Modal
        title="Prompt Template Preview"
        open={previewVisible}
        onCancel={() => setPreviewVisible(false)}
        footer={[
          <Button key="close" onClick={() => setPreviewVisible(false)}>
            Close
          </Button>,
        ]}
        width={800}
      >
        <div style={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace', fontSize: '13px' }}>
          {previewContent}
        </div>
      </Modal>
    </div>
  );
};

export default CustomPromptsManager;