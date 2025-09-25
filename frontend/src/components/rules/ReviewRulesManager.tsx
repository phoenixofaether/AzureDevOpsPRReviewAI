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
  Divider,
  Alert,
  Empty,
} from 'antd';
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  DragOutlined,
  EyeOutlined,
} from '@ant-design/icons';
import { UseFormReturn, useFieldArray, Controller } from 'react-hook-form';
import { ReviewRule, ReviewRuleType, ReviewSeverity } from '../../types/configuration';
import type { RepositoryConfiguration } from '../../types/configuration';

const { Text, Paragraph } = Typography;
const { Option } = Select;
const { TextArea } = Input;

interface ReviewRulesManagerProps {
  form: UseFormReturn<RepositoryConfiguration>;
}

interface ReviewRuleFormData {
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
}

const ReviewRulesManager: React.FC<ReviewRulesManagerProps> = ({ form }) => {
  const { control } = form;
  const [isModalVisible, setIsModalVisible] = useState(false);
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [modalForm] = Form.useForm();

  const { fields, append, update, remove, move } = useFieldArray({
    control,
    name: 'reviewRules',
  });

  const ruleTypeDescriptions = {
    [ReviewRuleType.CodeQuality]: 'Rules for code quality, maintainability, and best practices',
    [ReviewRuleType.Security]: 'Rules for identifying potential security vulnerabilities',
    [ReviewRuleType.Performance]: 'Rules for performance optimization and efficiency',
    [ReviewRuleType.Documentation]: 'Rules for code documentation and comments',
    [ReviewRuleType.Testing]: 'Rules for test coverage and testing best practices',
    [ReviewRuleType.Architecture]: 'Rules for architectural patterns and design principles',
    [ReviewRuleType.Style]: 'Rules for code formatting and style consistency',
    [ReviewRuleType.BestPractices]: 'Rules for language-specific best practices',
    [ReviewRuleType.Custom]: 'Custom user-defined rules',
  };

  const severityColors = {
    [ReviewSeverity.Info]: 'blue',
    [ReviewSeverity.Warning]: 'orange',
    [ReviewSeverity.Error]: 'red',
  };

  const openModal = (index?: number) => {
    setEditingIndex(index !== undefined ? index : null);

    if (index !== undefined) {
      const rule = fields[index];
      modalForm.setFieldsValue({
        name: rule.name,
        description: rule.description,
        type: rule.type,
        isEnabled: rule.isEnabled,
        minimumSeverity: rule.minimumSeverity,
        maximumSeverity: rule.maximumSeverity,
        filePatterns: rule.filePatterns.join('\n'),
        excludeFilePatterns: rule.excludeFilePatterns.join('\n'),
        parameters: JSON.stringify(rule.parameters, null, 2),
        priority: rule.priority,
      });
    } else {
      modalForm.resetFields();
      modalForm.setFieldsValue({
        type: ReviewRuleType.CodeQuality,
        isEnabled: true,
        minimumSeverity: ReviewSeverity.Info,
        maximumSeverity: ReviewSeverity.Error,
        priority: 0,
        parameters: '{}',
      });
    }

    setIsModalVisible(true);
  };

  const handleModalOk = async () => {
    try {
      const values = await modalForm.validateFields();

      const ruleData: ReviewRule = {
        id: editingIndex !== null ? fields[editingIndex].id : `rule_${Date.now()}`,
        name: values.name,
        description: values.description,
        type: values.type,
        isEnabled: values.isEnabled,
        minimumSeverity: values.minimumSeverity,
        maximumSeverity: values.maximumSeverity,
        filePatterns: values.filePatterns
          ? values.filePatterns.split('\n').map((p: string) => p.trim()).filter(Boolean)
          : [],
        excludeFilePatterns: values.excludeFilePatterns
          ? values.excludeFilePatterns.split('\n').map((p: string) => p.trim()).filter(Boolean)
          : [],
        parameters: values.parameters ? JSON.parse(values.parameters) : {},
        priority: values.priority,
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
      title: 'Delete Review Rule',
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

  const moveRule = (fromIndex: number, toIndex: number) => {
    if (toIndex >= 0 && toIndex < fields.length) {
      move(fromIndex, toIndex);
    }
  };

  return (
    <div>
      <Card className="configuration-card">
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
          <div>
            <div className="form-section-title" style={{ marginBottom: '8px' }}>
              Review Rules ({fields.length})
            </div>
            <Text type="secondary">
              Configure rules that determine what the AI should review and comment on
            </Text>
          </div>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => openModal()}>
            Add Rule
          </Button>
        </div>

        {fields.length === 0 ? (
          <Empty
            description="No review rules configured"
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
                    key="up"
                    type="text"
                    icon={<DragOutlined />}
                    onClick={() => moveRule(index, index - 1)}
                    disabled={index === 0}
                    size="small"
                  />,
                  <Button
                    key="down"
                    type="text"
                    icon={<DragOutlined style={{ transform: 'rotate(180deg)' }} />}
                    onClick={() => moveRule(index, index + 1)}
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
                      <Text strong={rule.isEnabled} type={rule.isEnabled ? 'default' : 'secondary'}>
                        {rule.name}
                      </Text>
                      <Tag color={severityColors[rule.type as keyof typeof ReviewRuleType] || 'default'}>
                        {rule.type}
                      </Tag>
                      <Tag color="blue">
                        Priority: {rule.priority}
                      </Tag>
                      <Tag color={severityColors[rule.minimumSeverity]}>
                        Min: {rule.minimumSeverity}
                      </Tag>
                      <Tag color={severityColors[rule.maximumSeverity]}>
                        Max: {rule.maximumSeverity}
                      </Tag>
                    </Space>
                  }
                  description={
                    <Space direction="vertical" size="small" style={{ width: '100%' }}>
                      {rule.description && (
                        <Text type="secondary" className="text-small">
                          {rule.description}
                        </Text>
                      )}
                      {rule.filePatterns.length > 0 && (
                        <div>
                          <Text className="text-small">
                            <strong>Include:</strong> {rule.filePatterns.join(', ')}
                          </Text>
                        </div>
                      )}
                      {rule.excludeFilePatterns.length > 0 && (
                        <div>
                          <Text className="text-small">
                            <strong>Exclude:</strong> {rule.excludeFilePatterns.join(', ')}
                          </Text>
                        </div>
                      )}
                      {Object.keys(rule.parameters || {}).length > 0 && (
                        <div>
                          <Text className="text-small">
                            <strong>Parameters:</strong> {Object.keys(rule.parameters).length} configured
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
        title={editingIndex !== null ? 'Edit Review Rule' : 'Add Review Rule'}
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
            <Input placeholder="Enter rule name" />
          </Form.Item>

          <Form.Item name="description" label="Description">
            <TextArea
              placeholder="Describe what this rule checks for"
              rows={2}
              maxLength={500}
              showCount
            />
          </Form.Item>

          <div style={{ display: 'flex', gap: '16px' }}>
            <Form.Item
              name="type"
              label="Rule Type"
              rules={[{ required: true, message: 'Please select a rule type' }]}
              style={{ flex: 1 }}
            >
              <Select placeholder="Select rule type">
                {Object.values(ReviewRuleType).map((type) => (
                  <Option key={type} value={type}>
                    {type}
                  </Option>
                ))}
              </Select>
            </Form.Item>

            <Form.Item
              name="priority"
              label="Priority"
              rules={[{ required: true, message: 'Please set priority' }]}
              style={{ flex: 1 }}
            >
              <InputNumber
                min={-100}
                max={100}
                style={{ width: '100%' }}
                placeholder="Priority (-100 to 100)"
              />
            </Form.Item>
          </div>

          <div style={{ display: 'flex', gap: '16px' }}>
            <Form.Item
              name="minimumSeverity"
              label="Minimum Severity"
              rules={[{ required: true, message: 'Please select minimum severity' }]}
              style={{ flex: 1 }}
            >
              <Select>
                {Object.values(ReviewSeverity).map((severity) => (
                  <Option key={severity} value={severity}>
                    <Tag color={severityColors[severity]} style={{ margin: 0 }}>
                      {severity}
                    </Tag>
                  </Option>
                ))}
              </Select>
            </Form.Item>

            <Form.Item
              name="maximumSeverity"
              label="Maximum Severity"
              rules={[{ required: true, message: 'Please select maximum severity' }]}
              style={{ flex: 1 }}
            >
              <Select>
                {Object.values(ReviewSeverity).map((severity) => (
                  <Option key={severity} value={severity}>
                    <Tag color={severityColors[severity]} style={{ margin: 0 }}>
                      {severity}
                    </Tag>
                  </Option>
                ))}
              </Select>
            </Form.Item>
          </div>

          <Form.Item name="isEnabled" valuePropName="checked" style={{ marginBottom: '16px' }}>
            <Switch checkedChildren="Enabled" unCheckedChildren="Disabled" />
            <span style={{ marginLeft: '8px' }}>Rule is active</span>
          </Form.Item>

          <Divider />

          <Form.Item
            name="filePatterns"
            label="File Patterns to Include"
            help="One pattern per line (e.g., *.ts, src/**/*.js)"
          >
            <TextArea
              placeholder="*.ts&#10;*.tsx&#10;src/**/*.js"
              rows={3}
            />
          </Form.Item>

          <Form.Item
            name="excludeFilePatterns"
            label="File Patterns to Exclude"
            help="One pattern per line (e.g., *.test.ts, **/*.min.js)"
          >
            <TextArea
              placeholder="*.test.ts&#10;*.spec.ts&#10;**/*.min.js"
              rows={3}
            />
          </Form.Item>

          <Form.Item
            name="parameters"
            label="Rule Parameters (JSON)"
            help="Additional configuration parameters in JSON format"
            rules={[
              {
                validator: (_, value) => {
                  if (!value || value.trim() === '') return Promise.resolve();
                  try {
                    JSON.parse(value);
                    return Promise.resolve();
                  } catch {
                    return Promise.reject(new Error('Invalid JSON format'));
                  }
                },
              },
            ]}
          >
            <TextArea
              placeholder='{ "maxComplexity": 10, "allowedPatterns": ["GET", "POST"] }'
              rows={4}
              className="code-editor"
            />
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
};

export default ReviewRulesManager;