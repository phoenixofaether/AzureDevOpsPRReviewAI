import React from 'react';
import { Card, Row, Col, Statistic, Typography, Empty } from 'antd';
import {
  SettingOutlined,
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  ClockCircleOutlined,
} from '@ant-design/icons';

const { Title } = Typography;

const Dashboard: React.FC = () => {
  return (
    <div style={{ maxWidth: '1200px', margin: '0 auto' }}>
      <Title level={2} style={{ marginBottom: '24px' }}>
        Configuration Dashboard
      </Title>

      <Row gutter={[16, 16]} style={{ marginBottom: '24px' }}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Total Configurations"
              value={0}
              prefix={<SettingOutlined />}
              valueStyle={{ color: '#1890ff' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Active Configurations"
              value={0}
              prefix={<CheckCircleOutlined />}
              valueStyle={{ color: '#52c41a' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Configurations with Issues"
              value={0}
              prefix={<ExclamationCircleOutlined />}
              valueStyle={{ color: '#faad14' }}
            />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Recently Updated"
              value={0}
              prefix={<ClockCircleOutlined />}
              valueStyle={{ color: '#722ed1' }}
            />
          </Card>
        </Col>
      </Row>

      <Card title="Recent Activity" style={{ marginBottom: '24px' }}>
        <Empty
          description="No recent activity"
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </Card>

      <Card title="Configuration Health">
        <Empty
          description="No configurations to monitor"
          image={Empty.PRESENTED_IMAGE_SIMPLE}
        />
      </Card>
    </div>
  );
};

export default Dashboard;