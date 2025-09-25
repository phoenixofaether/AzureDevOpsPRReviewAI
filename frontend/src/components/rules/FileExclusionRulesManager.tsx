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
} from 'antd';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  InfoCircleOutlined,
  EyeOutlined,
} from '@ant-design/icons';
import { UseFormReturn, useFieldArray } from 'react-hook-form';
import { FileExclusionRule, ExclusionType } from '../../types/configuration';
import type { RepositoryConfiguration } from '../../types/configuration';

const { Text } = Typography;
const { Option } = Select;
const { TextArea } = Input;

interface FileExclusionRulesManagerProps {
  form: UseFormReturn<RepositoryConfiguration>;
}

interface ExclusionRuleFormData {
  name: string;
  description?: string;
  pattern: string;
  type: ExclusionType;
  isEnabled: boolean;
  caseSensitive: boolean;
  maxFileSizeBytes?: number;
  fileExtensions: string[];
}

const FileExclusionRulesManager: React.FC<FileExclusionRulesManagerProps> = ({ form }) => {
  const { control } = form;
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [modalForm] = Form.useForm();

  const { fields, append, update, remove } = useFieldArray({
    control,
    name: 'fileExclusionRules',
  });

  const exclusionTypeDescriptions = {
    [ExclusionType.Glob]: 'Use glob patterns like **/*.min.js, dist/**, node_modules/**',
    [ExclusionType.Regex]: 'Use regular expressions for complex pattern matching',
    [ExclusionType.ExactPath]: 'Match exact file paths (e.g., src/config.json)',
    [ExclusionType.Directory]: 'Match directory names or paths',
    [ExclusionType.Extension]: 'Match by file extensions (.dll, .exe, .bin)',
    [ExclusionType.FileSize]: 'Exclude files larger than specified size',
    [ExclusionType.BinaryFiles]: 'Automatically detect and exclude binary files',
  };

  const exclusionTypeExamples = {
    [ExclusionType.Glob]: '**/*.min.js\ndist/**\nnode_modules/**',
    [ExclusionType.Regex]: '.*\\.min\\.(js|css)$\n^temp/.*',
    [ExclusionType.ExactPath]: 'src/config/secrets.json\n.env',
    [ExclusionType.Directory]: 'node_modules\nbin\nobj',
    [ExclusionType.Extension]: '.dll\n.exe\n.bin\n.so',
    [ExclusionType.FileSize]: '10485760', // 10MB in bytes
    [ExclusionType.BinaryFiles]: 'auto-detect',
  };

  const exclusionTypeColors = {
    [ExclusionType.Glob]: 'blue',
    [ExclusionType.Regex]: 'purple',
    [ExclusionType.ExactPath]: 'green',
    [ExclusionType.Directory]: 'orange',
    [ExclusionType.Extension]: 'cyan',
    [ExclusionType.FileSize]: 'red',
    [ExclusionType.BinaryFiles]: 'magenta',
  };

  const openModal = (index?: number) => {
    setEditingIndex(index !== undefined ? index : null);

    if (index !== undefined) {
      const rule = fields[index];
      modalForm.setFieldsValue({
        name: rule.name,
        description: rule.description,
        pattern: rule.pattern,
        type: rule.type,
        isEnabled: rule.isEnabled,
        caseSensitive: rule.caseSensitive,
        maxFileSizeBytes: rule.maxFileSizeBytes,
        fileExtensions: rule.fileExtensions.join('\n'),
      });
    } else {
      modalForm.resetFields();
      modalForm.setFieldsValue({
        type: ExclusionType.Glob,
        isEnabled: true,
        caseSensitive: false,
      });
    }

    setIsModalVisible(true);
  };

  const handleModalOk = async () => {
    try {
      const values = await modalForm.validateFields();

      const ruleData: FileExclusionRule = {
        id: editingIndex !== null ? fields[editingIndex].id : `exclusion_${Date.now()}`,
        name: values.name,
        description: values.description,
        pattern: values.pattern,
        type: values.type,
        isEnabled: values.isEnabled,
        caseSensitive: values.caseSensitive,
        maxFileSizeBytes: values.maxFileSizeBytes,
        fileExtensions: values.fileExtensions
          ? values.fileExtensions.split('\n').map((ext: string) => ext.trim()).filter(Boolean)
          : [],
        createdAt: editingIndex !== null ? fields[editingIndex].createdAt : new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };

      if (editingIndex !== null) {
        update(editingIndex, ruleData);
      } else {
        append(ruleData);
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
      title: 'Delete Exclusion Rule',
      content: `Are you sure you want to delete the rule "${fields[index].name}"?`,
      okText: 'Delete',
      okType: 'danger',
      onOk: () => remove(index),
    });
  };

  const toggleRule = (index: number) => {
    const rule = fields[index];
    update(index, { ...rule, isEnabled: !rule.isEnabled });
  };

  const formatFileSize = (bytes?: number): string => {
    if (!bytes) return 'N/A';
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + ' ' + sizes[i];
  };

  const selectedType = Form.useWatch('type', modalForm);

  return (
    <div>
      <Card className="configuration-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <div>
            <div className="form-section-title" style={{ marginBottom: '8px' }}>
              File Exclusion Rules ({fields.length})
            </div>
            <Text type="secondary">
              Configure patterns to exclude files from AI code review
            </Text>
          </div>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openModal()}>
            Add Exclusion Rule
          </Button>
        </div>

        <Alert
          message="Exclusion Rules Priority"
          description="Files matching any exclusion rule will be skipped during AI review. Rules are processed in the order shown."
          type="info"
          style={{ marginBottom: '16px' }}
          showIcon
        />

        {fields.length === 0 ? (
          <Empty
            description="No exclusion rules configured"
            image={Empty.PRESENTED_IMAGE_SIMPLE}
          >
            <Button type="primary" icon={<PlusOutlined />} onClick={() => openModal()}>
              Create First Rule
            </Button>
          </Empty>
        ) : (
          <List
            dataSource={fields}
            renderItem={(rule, index) => (
              <List.Item
                className={`rule-list-item ${!rule.isEnabled ? 'rule-list-item-disabled' : ''}`}
                actions={[
                  <Button
                    key="toggle"
                    type="text"
                    onClick={() => toggleRule(index)}
                  >
                    <Switch
                      checked={rule.isEnabled}
                      size="small"
                      onClick={(checked, e) => {
                        e.stopPropagation();
                        toggleRule(index);
                      }}
                    />
                  </Button>,
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
                      <Text strong={rule.isEnabled} type={rule.isEnabled ? 'default' : 'secondary'}>
                        {rule.name}
                      </Text>
                      <Tag color={exclusionTypeColors[rule.type]}>
                        {rule.type}
                      </Tag>
                      {rule.caseSensitive && (
                        <Tag color="default">Case Sensitive</Tag>
                      )}
                    </Space>
                  }
                  description={
                    <Space direction="vertical" size="small" style={{ width: '100%' }}>
                      {rule.description && (
                        <Text type="secondary" className="text-small">
                          {rule.description}
                        </Text>
                      )}
                      <div>
                        <Text className="text-small">
                          <strong>Pattern:</strong> <code>{rule.pattern}</code>
                        </Text>
                      </div>
                      {rule.maxFileSizeBytes && (
                        <div>
                          <Text className="text-small">
                            <strong>Max Size:</strong> {formatFileSize(rule.maxFileSizeBytes)}
                          </Text>
                        </div>
                      )}
                      {rule.fileExtensions.length > 0 && (
                        <div>
                          <Text className="text-small">
                            <strong>Extensions:</strong> {rule.fileExtensions.join(', ')}
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
        title={editingIndex !== null ? 'Edit Exclusion Rule' : 'Add Exclusion Rule'}
        open={isModalVisible}
        onOk={handleModalOk}
        onCancel={() => {
          setIsModalVisible(false);
          modalForm.resetFields();
          setEditingIndex(null);
        }}
        width={800}
        destroyOnClose
      >
        <Form form={modalForm} layout="vertical">
          <Form.Item
            name="name"
            label="Rule Name"
            rules={[
              { required: true, message: 'Please enter a rule name' },
              { max: 100, message: 'Name must be less than 100 characters' },
            ]}
          >
            <Input placeholder="Enter descriptive rule name" />
          </Form.Item>

          <Form.Item name="description" label="Description">
            <TextArea
              placeholder="Describe what files this rule excludes"
              rows={2}
              maxLength={500}
              showCount
            />
          </Form.Item>

          <Form.Item
            name="type"
            label={
              <Space>
                Exclusion Type
                <Tooltip title="Choose how the pattern will be matched against file paths">
                  <InfoCircleOutlined />
                </Tooltip>
              </Space>
            }
            rules={[{ required: true, message: 'Please select an exclusion type' }]}
          >
            <Select placeholder="Select exclusion type">
              {Object.values(ExclusionType).map((type) => (
                <Option key={type} value={type}>
                  <Space>
                    <Tag color={exclusionTypeColors[type]} style={{ margin: 0 }}>
                      {type}
                    </Tag>
                    <span>{exclusionTypeDescriptions[type]}</span>
                  </Space>
                </Option>
              ))}
            </Select>
          </Form.Item>

          <Form.Item
            name="pattern"
            label={
              <Space>
                Pattern
                {selectedType && (
                  <Tooltip title={exclusionTypeDescriptions[selectedType]}>
                    <InfoCircleOutlined />
                  </Tooltip>
                )}
              </Space>
            }
            rules={[
              { required: true, message: 'Please enter a pattern' },
              selectedType === ExclusionType.Regex
                ? {
                    validator: (_, value) => {
                      try {
                        new RegExp(value);
                        return Promise.resolve();
                      } catch {
                        return Promise.reject(new Error('Invalid regular expression'));
                      }
                    },
                  }
                : {},
            ]}
            help={
              selectedType && selectedType !== ExclusionType.BinaryFiles && (
                <div>
                  <Text type="secondary" className="text-small">
                    Example: <code>{exclusionTypeExamples[selectedType]}</code>
                  </Text>
                </div>
              )
            }
          >
            {selectedType === ExclusionType.BinaryFiles ? (
              <Input
                value="Binary files will be automatically detected"
                disabled
                placeholder="Auto-detection enabled"
              />
            ) : (
              <TextArea
                placeholder={`Enter ${selectedType?.toLowerCase()} pattern`}
                rows={selectedType === ExclusionType.Glob ? 3 : 2}
              />
            )}
          </Form.Item>

          <div style={{ display: 'flex', gap: '16px', alignItems: 'flex-end' }}>
            <Form.Item name="isEnabled" valuePropName="checked" style={{ marginBottom: 0 }}>
              <Switch checkedChildren="Enabled" unCheckedChildren="Disabled" />
              <span style={{ marginLeft: '8px' }}>Rule is active</span>
            </Form.Item>

            {selectedType !== ExclusionType.BinaryFiles && selectedType !== ExclusionType.FileSize && (
              <Form.Item name="caseSensitive" valuePropName="checked" style={{ marginBottom: 0 }}>
                <Switch checkedChildren="Case Sensitive" unCheckedChildren="Case Insensitive" />
              </Form.Item>
            )}
          </div>

          {(selectedType === ExclusionType.FileSize || selectedType === ExclusionType.Extension) && (
            <Form.Item
              name="maxFileSizeBytes"
              label="Maximum File Size (bytes)"
              rules={[
                selectedType === ExclusionType.FileSize
                  ? { required: true, message: 'Please specify maximum file size' }
                  : {},
              ]}
            >
              <InputNumber
                min={0}
                style={{ width: '100%' }}
                placeholder="Enter file size in bytes"
                formatter={(value) => {
                  if (!value) return '';
                  return formatFileSize(Number(value));
                }}
                parser={(value) => {
                  if (!value) return 0;
                  const match = value.match(/^([\d.]+)\s*(B|KB|MB|GB)$/i);
                  if (match) {
                    const num = parseFloat(match[1]);
                    const unit = match[2].toUpperCase();
                    const multipliers = { B: 1, KB: 1024, MB: 1024 * 1024, GB: 1024 * 1024 * 1024 };
                    return Math.round(num * (multipliers[unit as keyof typeof multipliers] || 1));
                  }
                  return parseInt(value.replace(/[^\d]/g, ''), 10) || 0;
                }}
              />
            </Form.Item>
          )}

          {(selectedType === ExclusionType.Extension || selectedType === ExclusionType.Glob) && (
            <Form.Item
              name="fileExtensions"
              label="Additional File Extensions"
              help="One extension per line (e.g., .min.js, .map, .d.ts)"
            >
              <TextArea
                placeholder=".min.js&#10;.map&#10;.d.ts"
                rows={3}
              />
            </Form.Item>
          )}
        </Form>
      </Modal>
    </div>
  );
};

export default FileExclusionRulesManager;