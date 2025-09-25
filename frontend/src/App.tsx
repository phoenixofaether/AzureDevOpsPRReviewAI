import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import AppLayout from './components/layout/AppLayout';
import RepositorySelector from './pages/RepositorySelector';
import ConfigurationEditor from './pages/ConfigurationEditor';
import Dashboard from './pages/Dashboard';

function App() {
  return (
    <AppLayout>
      <Routes>
        <Route path="/" element={<Navigate to="/repositories" replace />} />
        <Route path="/repositories" element={<RepositorySelector />} />
        <Route path="/configuration/:organization/:project/:repository" element={<ConfigurationEditor />} />
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="*" element={<Navigate to="/repositories" replace />} />
      </Routes>
    </AppLayout>
  );
}

export default App;