import React, { createContext, useContext, useState, useEffect } from 'react';

interface OrganizationContextType {
  organization: string | null;
  setOrganization: (org: string) => void;
  clearOrganization: () => void;
  isLoading: boolean;
}

const OrganizationContext = createContext<OrganizationContextType | undefined>(undefined);

export const useOrganization = () => {
  const context = useContext(OrganizationContext);
  if (context === undefined) {
    throw new Error('useOrganization must be used within an OrganizationProvider');
  }
  return context;
};

const ORGANIZATION_STORAGE_KEY = 'azure-devops-organization';

export const OrganizationProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [organization, setOrganizationState] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    // Load organization from localStorage on app start
    const storedOrganization = localStorage.getItem(ORGANIZATION_STORAGE_KEY);
    if (storedOrganization) {
      setOrganizationState(storedOrganization);
    }
    setIsLoading(false);
  }, []);

  const setOrganization = (org: string) => {
    localStorage.setItem(ORGANIZATION_STORAGE_KEY, org);
    setOrganizationState(org);
  };

  const clearOrganization = () => {
    localStorage.removeItem(ORGANIZATION_STORAGE_KEY);
    setOrganizationState(null);
  };

  return (
    <OrganizationContext.Provider
      value={{
        organization,
        setOrganization,
        clearOrganization,
        isLoading,
      }}
    >
      {children}
    </OrganizationContext.Provider>
  );
};