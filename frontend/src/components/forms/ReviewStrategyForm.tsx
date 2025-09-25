import React from 'react';
import { Form, Select, Switch, InputNumber, Card, Typography, Row, Col, Input, Alert } from 'antd';
import { Controller, UseFormReturn } from 'react-hook-form';
import { ReviewStrategy } from '../../types/configuration';
import { formatDuration, parseDuration } from '../../utils/validation';
import type { RepositoryConfiguration } from '../../types/configuration';

const { Text } = Typography;
const { Option } = Select;

interface ReviewStrategyFormProps {
  form: UseFormReturn<RepositoryConfiguration>;
}

const ReviewStrategyForm: React.FC<ReviewStrategyFormProps> = ({ form }) => {
  const { control, formState, watch } = form;

  const selectedStrategy = watch('reviewStrategySettings.strategy');
  const enableParallel = watch('reviewStrategySettings.enableParallelProcessing');

  const strategyDescriptions = {
    [ReviewStrategy.SingleRequest]: 'Analyzes the entire pull request in one API call. Best for small to medium PRs.',
    [ReviewStrategy.MultipleRequestsPerFile]: 'Processes each changed file individually. Better handling of large PRs with parallel processing.',
    [ReviewStrategy.MultipleRequestsByTokenSize]: 'Splits context by token size across multiple requests. Handles very large PRs.',
    [ReviewStrategy.HybridStrategy]: 'Automatically chooses the best strategy based on PR size and complexity.',
  };

  return (
    <div>
      <Card className="configuration-card">
        <Alert
          message="Review Strategy Configuration"
          description="These settings determine how pull requests are processed by the AI. Different strategies offer trade-offs between speed, context, and resource usage."
          type="info"
          style={{ marginBottom: '24px' }}
          showIcon
        />

        <div className="form-section">
          <div className="form-section-title">Strategy Selection</div>

          <Form.Item
            label="Review Strategy"
            help={formState.errors.reviewStrategySettings?.strategy?.message}
            validateStatus={formState.errors.reviewStrategySettings?.strategy ? 'error' : ''}
          >
            <Controller
              name="reviewStrategySettings.strategy"
              control={control}
              render={({ field }) => (
                <div>
                  <Select {...field} style={{ width: '100%' }} placeholder="Select review strategy">
                    {Object.values(ReviewStrategy).map((strategy) => (
                      <Option key={strategy} value={strategy}>
                        {strategy.replace(/([A-Z])/g, ' $1').trim()}
                      </Option>
                    ))}
                  </Select>
                  {selectedStrategy && (
                    <Text type="secondary" className="text-small" style={{ marginTop: '8px', display: 'block' }}>
                      {strategyDescriptions[selectedStrategy]}
                    </Text>
                  )}
                </div>
              )}
            />
          </Form.Item>

          <Form.Item
            label={
              <span>
                Enable parallel processing
                <br />
                <Text type="secondary" className="text-small">
                  Process multiple files or chunks concurrently (when strategy supports it)
                </Text>
              </span>
            }
          >
            <Controller
              name="reviewStrategySettings.enableParallelProcessing"
              control={control}
              render={({ field: { value, onChange } }) => (
                <Switch
                  checked={value}
                  onChange={onChange}
                  checkedChildren="Enabled"
                  unCheckedChildren="Disabled"
                  disabled={selectedStrategy === ReviewStrategy.SingleRequest}
                />
              )}
            />
          </Form.Item>
        </div>

        <div className="form-section">
          <div className="form-section-title">Processing Limits</div>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label="Max files per request"
                help={formState.errors.reviewStrategySettings?.maxFilesPerRequest?.message}
                validateStatus={formState.errors.reviewStrategySettings?.maxFilesPerRequest ? 'error' : ''}
              >
                <Controller
                  name="reviewStrategySettings.maxFilesPerRequest"
                  control={control}
                  render={({ field }) => (
                    <InputNumber
                      {...field}
                      min={1}
                      max={100}
                      style={{ width: '100%' }}
                      placeholder="Maximum files to process together"
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={12}>
              <Form.Item
                label="Max tokens per request"
                help={formState.errors.reviewStrategySettings?.maxTokensPerRequest?.message}
                validateStatus={formState.errors.reviewStrategySettings?.maxTokensPerRequest ? 'error' : ''}
              >
                <Controller
                  name="reviewStrategySettings.maxTokensPerRequest"
                  control={control}
                  render={({ field }) => (
                    <InputNumber
                      {...field}
                      min={1000}
                      max={1000000}
                      style={{ width: '100%' }}
                      formatter={(value) => value ? `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',') : ''}
                      parser={(value) => value?.replace(/\$\s?|(,*)/g, '') as any}
                      placeholder="Token limit per API call"
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            label="Max tokens per file"
            help={formState.errors.reviewStrategySettings?.maxTokensPerFile?.message}
            validateStatus={formState.errors.reviewStrategySettings?.maxTokensPerFile ? 'error' : ''}
          >
            <Controller
              name="reviewStrategySettings.maxTokensPerFile"
              control={control}
              render={({ field }) => (
                <InputNumber
                  {...field}
                  min={100}
                  max={100000}
                  style={{ width: '100%' }}
                  formatter={(value) => value ? `${value}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',') : ''}
                  parser={(value) => value?.replace(/\$\s?|(,*)/g, '') as any}
                  placeholder="Token limit per individual file"
                />
              )}
            />
          </Form.Item>
        </div>

        <div className="form-section">
          <div className="form-section-title">Multi-Request Options</div>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Include summary when split
                    <br />
                    <Text type="secondary" className="text-small">
                      Add an overall summary when using multiple requests
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="reviewStrategySettings.includeSummaryWhenSplit"
                  control={control}
                  render={({ field: { value, onChange } }) => (
                    <Switch
                      checked={value}
                      onChange={onChange}
                      checkedChildren="Enabled"
                      unCheckedChildren="Disabled"
                      disabled={selectedStrategy === ReviewStrategy.SingleRequest}
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Combine results from multiple requests
                    <br />
                    <Text type="secondary" className="text-small">
                      Merge and deduplicate findings from multiple API calls
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="reviewStrategySettings.combineResultsFromMultipleRequests"
                  control={control}
                  render={({ field: { value, onChange } }) => (
                    <Switch
                      checked={value}
                      onChange={onChange}
                      checkedChildren="Enabled"
                      unCheckedChildren="Disabled"
                      disabled={selectedStrategy === ReviewStrategy.SingleRequest}
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>
        </div>

        <div className="form-section">
          <div className="form-section-title">Performance Settings</div>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label="Max concurrent requests"
                help={formState.errors.reviewStrategySettings?.maxConcurrentRequests?.message}
                validateStatus={formState.errors.reviewStrategySettings?.maxConcurrentRequests ? 'error' : ''}
              >
                <Controller
                  name="reviewStrategySettings.maxConcurrentRequests"
                  control={control}
                  render={({ field }) => (
                    <InputNumber
                      {...field}
                      min={1}
                      max={10}
                      style={{ width: '100%' }}
                      placeholder="Maximum parallel API calls"
                      disabled={!enableParallel || selectedStrategy === ReviewStrategy.SingleRequest}
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={12}>
              <Form.Item
                label="Request timeout"
                help={formState.errors.reviewStrategySettings?.requestTimeout?.message}
                validateStatus={formState.errors.reviewStrategySettings?.requestTimeout ? 'error' : ''}
              >
                <Controller
                  name="reviewStrategySettings.requestTimeout"
                  control={control}
                  render={({ field: { value, onChange } }) => (
                    <Input
                      value={value ? formatDuration(value) : ''}
                      onChange={(e) => {
                        try {
                          const parsed = parseDuration(e.target.value);
                          onChange(parsed);
                        } catch {
                          onChange(e.target.value);
                        }
                      }}
                      placeholder="e.g., 5m, 30s, 10m 30s"
                      addonAfter="duration"
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>

          <Text type="secondary" className="text-small">
            Duration format: Use combinations like "5m", "30s", "2m 30s", or "1h 15m"
          </Text>
        </div>
      </Card>
    </div>
  );
};

export default ReviewStrategyForm;