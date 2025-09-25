import React from 'react';
import { Form, Input, Switch, Card, Typography, Row, Col } from 'antd';
import { Controller, UseFormReturn } from 'react-hook-form';
import type { RepositoryConfiguration } from '../../types/configuration';

const { Text } = Typography;

interface BasicSettingsFormProps {
  form: UseFormReturn<RepositoryConfiguration>;
}

const BasicSettingsForm: React.FC<BasicSettingsFormProps> = ({ form }) => {
  const { control, formState } = form;

  return (
    <div>
      <Card className="configuration-card">
        <div className="form-section">
          <div className="form-section-title">Repository Information</div>

          <Row gutter={16}>
            <Col span={8}>
              <Form.Item
                label="Organization"
                help={formState.errors.organization?.message}
                validateStatus={formState.errors.organization ? 'error' : ''}
              >
                <Controller
                  name="organization"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      placeholder="Azure DevOps organization name"
                      disabled
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={8}>
              <Form.Item
                label="Project"
                help={formState.errors.project?.message}
                validateStatus={formState.errors.project ? 'error' : ''}
              >
                <Controller
                  name="project"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      placeholder="Project name"
                      disabled
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={8}>
              <Form.Item
                label="Repository"
                help={formState.errors.repository?.message}
                validateStatus={formState.errors.repository ? 'error' : ''}
              >
                <Controller
                  name="repository"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      placeholder="Repository name"
                      disabled
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>
        </div>

        <div className="form-section">
          <div className="form-section-title">Configuration Status</div>

          <Form.Item
            label={
              <span>
                Enable AI Reviews
                <br />
                <Text type="secondary" className="text-small">
                  Toggle this to enable or disable AI-powered code reviews for this repository
                </Text>
              </span>
            }
          >
            <Controller
              name="isEnabled"
              control={control}
              render={({ field: { value, onChange } }) => (
                <Switch
                  checked={value}
                  onChange={onChange}
                  checkedChildren="Enabled"
                  unCheckedChildren="Disabled"
                />
              )}
            />
          </Form.Item>
        </div>

        <div className="form-section">
          <div className="form-section-title">Metadata</div>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item label="Created At">
                <Controller
                  name="createdAt"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      value={field.value ? new Date(field.value).toLocaleString() : ''}
                      disabled
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={12}>
              <Form.Item label="Updated At">
                <Controller
                  name="updatedAt"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      value={field.value ? new Date(field.value).toLocaleString() : ''}
                      disabled
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item label="Created By">
                <Controller
                  name="createdBy"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      value={field.value || 'System'}
                      disabled
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={12}>
              <Form.Item label="Version">
                <Controller
                  name="version"
                  control={control}
                  render={({ field }) => (
                    <Input
                      {...field}
                      value={field.value?.toString() || '1'}
                      disabled
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>
        </div>
      </Card>
    </div>
  );
};

export default BasicSettingsForm;