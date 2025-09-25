import React from 'react';
import { Layout, Menu, Typography, Button, Space } from 'antd';
import { useNavigate, useLocation } from 'react-router-dom';
import {
  DashboardOutlined,
  SettingOutlined,
  FolderOutlined,
  BranchesOutlined,
  LogoutOutlined,
} from '@ant-design/icons';
import { useOrganization } from '../../contexts/OrganizationContext';

const { Header, Sider } = Layout;
const { Title } = Typography;

interface AppLayoutProps {
  children: React.ReactNode;
}

const AppLayout: React.FC<AppLayoutProps> = ({ children }) => {
  const navigate = useNavigate();
  const location = useLocation();
  const { organization, clearOrganization } = useOrganization();

  const menuItems = [
    {
      key: '/repositories',
      icon: <FolderOutlined />,
      label: 'Repositories',
    },
    {
      key: '/dashboard',
      icon: <DashboardOutlined />,
      label: 'Dashboard',
    },
  ];

  const handleMenuClick = ({ key }: { key: string }) => {
    navigate(key);
  };

  return (
    <Layout className="full-height">
      <Header style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 24px' }}>
        <div style={{ display: 'flex', alignItems: 'center', color: 'white' }}>
          <BranchesOutlined style={{ fontSize: '24px', marginRight: '12px' }} />
          <Title level={3} style={{ color: 'white', margin: 0 }}>
            Azure DevOps AI Review
          </Title>
        </div>
        <Space>
          <span style={{ color: 'white', fontSize: '14px' }}>
            Organization: <strong>{organization}</strong>
          </span>
          <Button
            type="text"
            icon={<LogoutOutlined />}
            onClick={clearOrganization}
            style={{ color: 'white' }}
            size="small"
          >
            Change
          </Button>
        </Space>
      </Header>

      <Layout>
        <Sider width={250} theme="dark">
          <Menu
            mode="inline"
            selectedKeys={[location.pathname]}
            items={menuItems}
            onClick={handleMenuClick}
            theme="dark"
            style={{ height: '100%', borderRight: 0 }}
          />
        </Sider>

        <Layout style={{ padding: '0', backgroundColor: '#f5f5f5' }}>
          {children}
        </Layout>
      </Layout>
    </Layout>
  );
};

export default AppLayout;