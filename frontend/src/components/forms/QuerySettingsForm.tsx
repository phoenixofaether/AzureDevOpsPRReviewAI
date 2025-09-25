import React from 'react';
import { Form, Select, Switch, InputNumber, Card, Typography, Row, Col, Input, Alert, Button, Space } from 'antd';
import { Controller, UseFormReturn, useFieldArray } from 'react-hook-form';
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons';
import { QueryStrategy } from '../../types/configuration';
import { formatDuration, parseDuration } from '../../utils/validation';
import type { RepositoryConfiguration } from '../../types/configuration';

const { Text } = Typography;
const { Option } = Select;

interface QuerySettingsFormProps {
  form: UseFormReturn<RepositoryConfiguration>;
}

const QuerySettingsForm: React.FC<QuerySettingsFormProps> = ({ form }) => {
  const { control, formState, watch } = form;

  const selectedStrategy = watch('querySettings.strategy');
  const enableDirect = watch('querySettings.enableDirectFileAccess');
  const enableVector = watch('querySettings.enableVectorSearch');

  const {
    fields: excludePatternsFields,
    append: appendExcludePattern,
    remove: removeExcludePattern,
  } = useFieldArray({
    control,
    name: 'querySettings.defaultExcludePatterns',
  });

  const {
    fields: excludeDirectoriesFields,
    append: appendExcludeDirectory,
    remove: removeExcludeDirectory,
  } = useFieldArray({
    control,
    name: 'querySettings.defaultExcludeDirectories',
  });

  const strategyDescriptions = {
    [QueryStrategy.VectorOnly]: 'Use semantic search only. Requires vector database setup.',
    [QueryStrategy.DirectOnly]: 'Use direct file system access only. Faster but less contextual.',
    [QueryStrategy.Hybrid]: 'Use both methods and combine results. Best accuracy but slower.',
    [QueryStrategy.DirectFallback]: 'Use vector search with direct fallback if vector search fails.',
  };

  return (
    <div>
      <Card className="configuration-card">
        <Alert
          message="Query Strategy Configuration"
          description="These settings control how the AI system searches and retrieves code context from repositories. Different strategies offer trade-offs between speed, accuracy, and resource requirements."
          type="info"
          style={{ marginBottom: '24px' }}
          showIcon
        />

        <div className="form-section">
          <div className="form-section-title">Query Strategy</div>

          <Form.Item
            label="Query Strategy"
            help={formState.errors.querySettings?.strategy?.message}
            validateStatus={formState.errors.querySettings?.strategy ? 'error' : ''}
          >
            <Controller
              name="querySettings.strategy"
              control={control}
              render={({ field }) => (
                <div>
                  <Select {...field} style={{ width: '100%' }} placeholder="Select query strategy">
                    {Object.values(QueryStrategy).map((strategy) => (
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

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Enable direct file access
                    <br />
                    <Text type="secondary" className="text-small">
                      Allow direct reading from the file system
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="querySettings.enableDirectFileAccess"
                  control={control}
                  render={({ field: { value, onChange } }) => (
                    <Switch
                      checked={value}
                      onChange={onChange}
                      checkedChildren="Enabled"
                      unCheckedChildren="Disabled"
                      disabled={selectedStrategy === QueryStrategy.VectorOnly}
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Enable vector search
                    <br />
                    <Text type="secondary" className="text-small">
                      Allow semantic search using vector database
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="querySettings.enableVectorSearch"
                  control={control}
                  render={({ field: { value, onChange } }) => (
                    <Switch
                      checked={value}
                      onChange={onChange}
                      checkedChildren="Enabled"
                      unCheckedChildren="Disabled"
                      disabled={selectedStrategy === QueryStrategy.DirectOnly}
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>

          {((selectedStrategy === QueryStrategy.DirectOnly && !enableDirect) ||
            (selectedStrategy === QueryStrategy.VectorOnly && !enableVector) ||
            (!enableDirect && !enableVector)) && (
            <Alert
              message="Configuration Warning"
              description="At least one query method must be enabled for the selected strategy."
              type="warning"
              style={{ marginTop: '16px' }}
              showIcon
            />
          )}
        </div>

        <div className="form-section">
          <div className="form-section-title">Search Limits</div>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label="Max direct search results"
                help={formState.errors.querySettings?.maxDirectSearchResults?.message}
                validateStatus={formState.errors.querySettings?.maxDirectSearchResults ? 'error' : ''}
              >
                <Controller
                  name="querySettings.maxDirectSearchResults"
                  control={control}
                  render={({ field }) => (
                    <InputNumber
                      {...field}
                      min={10}
                      max={1000}
                      style={{ width: '100%' }}
                      placeholder="Maximum search results"
                      disabled={!enableDirect}
                    />
                  )}
                />
              </Form.Item>
            </Col>

            <Col span={12}>
              <Form.Item
                label="Max file read size (KB)"
                help={formState.errors.querySettings?.maxFileReadSizeKB?.message}
                validateStatus={formState.errors.querySettings?.maxFileReadSizeKB ? 'error' : ''}
              >
                <Controller
                  name="querySettings.maxFileReadSizeKB"
                  control={control}
                  render={({ field }) => (
                    <InputNumber
                      {...field}
                      min={10}
                      max={10240}
                      style={{ width: '100%' }}
                      formatter={(value) => {
                        if (!value) return '';
                        const mb = Number(value) / 1024;
                        if (mb >= 1) {
                          return `${mb.toFixed(1)} MB`;
                        }
                        return `${value} KB`;
                      }}
                      parser={(value) => {
                        if (!value) return 0;
                        const match = value.match(/(\d+(?:\.\d+)?)\s*(KB|MB)/i);
                        if (match) {
                          const num = parseFloat(match[1]);
                          const unit = match[2].toUpperCase();
                          return unit === 'MB' ? num * 1024 : num;
                        }
                        return parseInt(value.replace(/[^\d.]/g, ''), 10) || 0;
                      }}
                      placeholder="Maximum file size to read"
                      disabled={!enableDirect}
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            label="Default context lines"
            help={formState.errors.querySettings?.defaultContextLines?.message}
            validateStatus={formState.errors.querySettings?.defaultContextLines ? 'error' : ''}
          >
            <Controller
              name="querySettings.defaultContextLines"
              control={control}
              render={({ field }) => (
                <InputNumber
                  {...field}
                  min={0}
                  max={20}
                  style={{ width: '100%' }}
                  placeholder="Number of context lines around search results"
                />
              )}
            />
            <Text type="secondary" className="text-small">
              Number of lines to include before and after search matches for context
            </Text>
          </Form.Item>
        </div>

        <div className="form-section">
          <div className="form-section-title">
            Default Exclude Patterns
            <Text type="secondary" style={{ fontWeight: 'normal', marginLeft: '8px' }}>
              (File patterns to exclude from searches)
            </Text>
          </div>

          <Space direction="vertical" style={{ width: '100%' }}>
            {excludePatternsFields.map((field, index) => (
              <Space key={field.id} style={{ display: 'flex', marginBottom: 8 }}>
                <Controller
                  name={`querySettings.defaultExcludePatterns.${index}`}
                  control={control}
                  render={({ field: fieldProps }) => (
                    <Input
                      {...fieldProps}
                      placeholder="e.g., *.min.js, *.bundle.js"
                      style={{ width: '300px' }}
                    />
                  )}
                />
                <Button
                  type="text"
                  danger
                  icon={<DeleteOutlined />}
                  onClick={() => removeExcludePattern(index)}
                  size="small"
                />
              </Space>
            ))}

            <Button
              type="dashed"
              onClick={() => appendExcludePattern('')}
              icon={<PlusOutlined />}
              style={{ width: '100%', marginTop: '8px' }}
            >
              Add Exclude Pattern
            </Button>
          </Space>
        </div>

        <div className="form-section">
          <div className="form-section-title">
            Default Exclude Directories
            <Text type="secondary" style={{ fontWeight: 'normal', marginLeft: '8px' }}>
              (Directories to exclude from searches)
            </Text>
          </div>

          <Space direction="vertical" style={{ width: '100%' }}>
            {excludeDirectoriesFields.map((field, index) => (
              <Space key={field.id} style={{ display: 'flex', marginBottom: 8 }}>
                <Controller
                  name={`querySettings.defaultExcludeDirectories.${index}`}
                  control={control}
                  render={({ field: fieldProps }) => (
                    <Input
                      {...fieldProps}
                      placeholder="e.g., node_modules, bin, obj"
                      style={{ width: '300px' }}
                    />
                  )}
                />
                <Button
                  type="text"
                  danger
                  icon={<DeleteOutlined />}
                  onClick={() => removeExcludeDirectory(index)}
                  size="small"
                />
              </Space>
            ))}

            <Button
              type="dashed"
              onClick={() => appendExcludeDirectory('')}
              icon={<PlusOutlined />}
              style={{ width: '100%', marginTop: '8px' }}
            >
              Add Exclude Directory
            </Button>
          </Space>
        </div>

        <div className="form-section">
          <div className="form-section-title">Caching Settings</div>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                label={
                  <span>
                    Enable search result caching
                    <br />
                    <Text type="secondary" className="text-small">
                      Cache search results to improve performance
                    </Text>
                  </span>
                }
              >
                <Controller
                  name="querySettings.enableSearchResultCaching"
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
                label="Cache expiration"
                help={formState.errors.querySettings?.cacheExpiration?.message}
                validateStatus={formState.errors.querySettings?.cacheExpiration ? 'error' : ''}
              >
                <Controller
                  name="querySettings.cacheExpiration"
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
                      placeholder="e.g., 30m, 1h, 2h 30m"
                      addonAfter="duration"
                      disabled={!watch('querySettings.enableSearchResultCaching')}
                    />
                  )}
                />
              </Form.Item>
            </Col>
          </Row>

          <Text type="secondary" className="text-small">
            Duration format: Use combinations like "30m", "1h", "2h 30m"
          </Text>
        </div>
      </Card>
    </div>
  );
};

export default QuerySettingsForm;