import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { OrganizationProvider, useOrganization } from './contexts/OrganizationContext';
import AppLayout from './components/layout/AppLayout';
import RepositorySelector from './pages/RepositorySelector';
import ConfigurationEditor from './pages/ConfigurationEditor';
import Dashboard from './pages/Dashboard';
import OrganizationSetup from './pages/OrganizationSetup';
import { Spin } from 'antd';

const AppRoutes: React.FC = () => {
  const { organization, isLoading } = useOrganization();

  if (isLoading) {
    return (
      <div style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center'
      }}>
        <Spin size="large" />
      </div>
    );
  }

  if (!organization) {
    return <OrganizationSetup />;
  }

  return (
    <AppLayout>
      <Routes>
        <Route path="/" element={<Navigate to="/repositories" replace />} />
        <Route path="/repositories" element={<RepositorySelector />} />
        <Route path="/configuration/:project/:repository" element={<ConfigurationEditor />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="*" element={<Navigate to="/repositories" replace />} />
      </Routes>
    </AppLayout>
  );
};

function App() {
  return (
    <OrganizationProvider>
      <AppRoutes />
    </OrganizationProvider>
  );
}

export default App;