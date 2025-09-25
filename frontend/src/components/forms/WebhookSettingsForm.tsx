import React from 'react';
import { Form, Switch, InputNumber, Card, Typography, Row, Col, Select } from 'antd';
import { Controller, UseFormReturn, useFieldArray } from 'react-hook-form';
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons';
import { Button, Space, Input } from 'antd';
import type { RepositoryConfiguration } from '../../types/configuration';

const { Text } = Typography;
const { Option } = Select;

interface WebhookSettingsFormProps {
  form: UseFormReturn<RepositoryConfiguration>;
}

const WebhookSettingsForm: React.FC<WebhookSettingsFormProps> = ({ form }) => {
  const { control, formState } = form;

  const { fields, append, remove } = useFieldArray({
    control,
    name: 'webhookSettings.allowedTriggerUsers',
  });

  const addUser = () => {
    append('');
  };

  const removeUser = (index: number) => {
    remove(index);
  };

  return (
    <div>
      <Card className="configuration-card">
        <div className="form-section">
          <div className="form-section-title">Automatic Review Triggers</div>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Auto-review on PR creation
                    <br />
                    <Text type="secondary" className="text-small">
                      Automatically trigger AI review when a new pull request is created
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="webhookSettings.autoReviewOnCreate"
                  control={control}
                  render={({ field: { value, onChange } }) => (
                    <Switch
                      checked={value}
                      onChange={onChange}
                      checkedChildren="On"
                      unCheckedChildren="Off"
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Auto-review on PR update
                    <br />
                    <Text type="secondary" className="text-small">
                      Automatically trigger AI review when a pull request is updated
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="webhookSettings.autoReviewOnUpdate"
                  control={control}
                  render={({ field: { value, onChange } }) => (
                    <Switch
                      checked={value}
                      onChange={onChange}
                      checkedChildren="On"
                      unCheckedChildren="Off"
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            label={
              <span>
                Require comment trigger
                <br />
                <Text type="secondary" className="text-small">
                  Require a specific comment to trigger AI review (e.g., "@ai review")
                </Text>
              </span>
            }
          >
            <Controller
              name="webhookSettings.requireCommentTrigger"
              control={control}
              render={({ field: { value, onChange } }) => (
                <Switch
                  checked={value}
                  onChange={onChange}
                  checkedChildren="Required"
                  unCheckedChildren="Not Required"
                />
              )}
            />
          </Form.Item>
        </div>

        <div className="form-section">
          <div className="form-section-title">Review Limits</div>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label="Maximum files for auto-review"
                help={formState.errors.webhookSettings?.maxFilesForAutoReview?.message}
                validateStatus={formState.errors.webhookSettings?.maxFilesForAutoReview ? 'error' : ''}
              >
                <Controller
                  name="webhookSettings.maxFilesForAutoReview"
                  control={control}
                  render={({ field }) => (
                    <InputNumber
                      {...field}
                      min={1}
                      max={1000}
                      style={{ width: '100%' }}
                      placeholder="Maximum files to review automatically"
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={12}>
              <Form.Item
                label="Maximum diff size (bytes)"
                help={formState.errors.webhookSettings?.maxDiffSizeBytes?.message}
                validateStatus={formState.errors.webhookSettings?.maxDiffSizeBytes ? 'error' : ''}
              >
                <Controller
                  name="webhookSettings.maxDiffSizeBytes"
                  control={control}
                  render={({ field }) => (
                    <InputNumber
                      {...field}
                      min={1024}
                      max={100 * 1024 * 1024}
                      style={{ width: '100%' }}
                      formatter={(value) => {
                        if (!value) return '';
                        const kb = Math.round(Number(value) / 1024);
                        if (kb >= 1024) {
                          return `${Math.round(kb / 1024)} MB`;
                        }
                        return `${kb} KB`;
                      }}
                      parser={(value) => {
                        if (!value) return 0;
                        const match = value.match(/(\d+(?:\.\d+)?)\s*(KB|MB)/i);
                        if (match) {
                          const num = parseFloat(match[1]);
                          const unit = match[2].toUpperCase();
                          return unit === 'MB' ? num * 1024 * 1024 : num * 1024;
                        }
                        return parseInt(value.replace(/[^\d]/g, ''), 10) || 0;
                      }}
                      placeholder="Maximum diff size for auto-review"
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>
        </div>

        <div className="form-section">
          <div className="form-section-title">
            Allowed Trigger Users
            <Text type="secondary" style={{ fontWeight: 'normal', marginLeft: '8px' }}>
              (Users who can trigger AI reviews via comments)
            </Text>
          </div>

          <Space direction="vertical" style={{ width: '100%' }}>
            {fields.map((field, index) => (
              <Space key={field.id} style={{ display: 'flex', marginBottom: 8 }}>
                <Controller
                  name={`webhookSettings.allowedTriggerUsers.${index}`}
                  control={control}
                  render={({ field: fieldProps }) => (
                    <Input
                      {...fieldProps}
                      placeholder="Enter username or email"
                      style={{ width: '300px' }}
                    />
                  )}
                />
                <Button
                  type="text"
                  danger
                  icon={<DeleteOutlined />}
                  onClick={() => removeUser(index)}
                  size="small"
                />
              </Space>
            ))}

            <Button
              type="dashed"
              onClick={addUser}
              icon={<PlusOutlined />}
              style={{ width: '100%', marginTop: '8px' }}
            >
              Add Allowed User
            </Button>

            <Text type="secondary" className="text-small">
              Leave empty to allow all users to trigger AI reviews via comments.
            </Text>
          </Space>
        </div>
      </Card>
    </div>
  );
};

export default WebhookSettingsForm;