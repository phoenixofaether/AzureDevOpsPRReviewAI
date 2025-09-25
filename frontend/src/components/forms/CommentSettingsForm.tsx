import React from 'react';
import { Form, Switch, Input, InputNumber, Card, Typography, Row, Col } from 'antd';
import { Controller, UseFormReturn } from 'react-hook-form';
import type { RepositoryConfiguration } from '../../types/configuration';

const { Text } = Typography;

interface CommentSettingsFormProps {
  form: UseFormReturn<RepositoryConfiguration>;
}

const CommentSettingsForm: React.FC<CommentSettingsFormProps> = ({ form }) => {
  const { control, formState } = form;

  return (
    <div>
      <Card className="configuration-card">
        <div className="form-section">
          <div className="form-section-title">Comment Types</div>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Enable line comments
                    <br />
                    <Text type="secondary" className="text-small">
                      Add comments directly to specific lines of code
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="commentSettings.enableLineComments"
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
            </Col>

            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Enable summary comment
                    <br />
                    <Text type="secondary" className="text-small">
                      Add an overall summary comment to the pull request
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="commentSettings.enableSummaryComment"
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
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Group similar issues
                    <br />
                    <Text type="secondary" className="text-small">
                      Combine similar findings into grouped comments
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="commentSettings.groupSimilarIssues"
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
            </Col>

            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Include confidence score
                    <br />
                    <Text type="secondary" className="text-small">
                      Show AI confidence level for each suggestion
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="commentSettings.includeConfidenceScore"
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
            </Col>
          </Row>

          <Form.Item
            label={
              <span>
                Enable reply to comments
                <br />
                <Text type="secondary" className="text-small">
                  Allow AI to reply to user comments and questions
                </Text>
              </span>
            }
          >
            <Controller
              name="commentSettings.enableReplyToComments"
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
          <div className="form-section-title">Comment Formatting</div>

          <Form.Item
            label="Comment prefix"
            help={formState.errors.commentSettings?.commentPrefix?.message}
            validateStatus={formState.errors.commentSettings?.commentPrefix ? 'error' : ''}
          >
            <Controller
              name="commentSettings.commentPrefix"
              control={control}
              render={({ field }) => (
                <Input
                  {...field}
                  placeholder="🤖 AI Code Review"
                  maxLength={100}
                  showCount
                />
              )}
            />
            <Text type="secondary" className="text-small">
              Text that appears at the beginning of AI-generated comments
            </Text>
          </Form.Item>
        </div>

        <div className="form-section">
          <div className="form-section-title">Comment Limits</div>

          <Form.Item
            label="Maximum comments per file"
            help={formState.errors.commentSettings?.maxCommentsPerFile?.message}
            validateStatus={formState.errors.commentSettings?.maxCommentsPerFile ? 'error' : ''}
          >
            <Controller
              name="commentSettings.maxCommentsPerFile"
              control={control}
              render={({ field }) => (
                <InputNumber
                  {...field}
                  min={1}
                  max={50}
                  style={{ width: '100%' }}
                  placeholder="Maximum number of comments per file"
                />
              )}
            />
            <Text type="secondary" className="text-small">
              Prevents comment spam by limiting the number of AI comments per file
            </Text>
          </Form.Item>
        </div>
      </Card>
    </div>
  );
};

export default CommentSettingsForm;