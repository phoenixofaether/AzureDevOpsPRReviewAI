import { TeamOutlined } from "@ant-design/icons";
import { Button, Card, Input, Typography } from "antd";
import React, { useState } from "react";
import { useOrganization } from "../contexts/OrganizationContext";

const { Title, Text } = Typography;

const OrganizationSetup: React.FC = () => {
  const { setOrganization } = useOrganization();
  const [organizationName, setOrganizationName] = useState("");
  const [error, setError] = useState("");

  const validateOrganization = (value: string): string => {
    if (!value || value.trim().length === 0) {
      return "Please enter your organization name";
    }
    if (value.trim().length < 1) {
      return "Organization name cannot be empty";
    }
    if (!/^[a-zA-Z0-9\-_]+$/.test(value.trim())) {
      return "Only letters, numbers, hyphens, and underscores are allowed";
    }
    return "";
  };

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setOrganizationName(value);
    setError("");
  };

  const handleSubmit = () => {
    const validationError = validateOrganization(organizationName);
    if (validationError) {
      setError(validationError);
      return;
    }
    setOrganization(organizationName.trim());
  };

  return (
    <div
      style={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        background: "#f0f2f5",
        padding: "20px",
      }}
    >
      <Card style={{ width: "100%", maxWidth: "500px" }}>
        <div style={{ textAlign: "center", marginBottom: "32px" }}>
          <TeamOutlined
            style={{ fontSize: "64px", color: "#1890ff", marginBottom: "16px" }}
          />
          <Title level={2}>Welcome to PR Review AI</Title>
          <Text type="secondary">
            Please enter your Azure DevOps organization to get started. This
            will be saved locally and you won't need to enter it again.
          </Text>
        </div>

        <div>
          <div style={{ marginBottom: "16px" }}>
            <label
              style={{
                display: "block",
                marginBottom: "8px",
                fontWeight: "500",
                fontSize: "16px",
              }}
            >
              Azure DevOps Organization
            </label>
            <Input
              value={organizationName}
              onChange={handleInputChange}
              placeholder="Enter organization name"
              prefix={<TeamOutlined />}
              autoFocus
              size="large"
              status={error ? "error" : ""}
            />
            {error && (
              <div
                style={{
                  color: "#ff4d4f",
                  fontSize: "14px",
                  marginTop: "8px",
                }}
              >
                {error}
              </div>
            )}
            <div
              style={{
                color: "rgba(0, 0, 0, 0.45)",
                fontSize: "14px",
                marginTop: "8px",
              }}
            >
              Enter the organization name from your Azure DevOps URL (e.g.,
              'mycompany' from https://dev.azure.com/mycompany)
            </div>
          </div>

          <div style={{ marginBottom: 0 }}>
            <Button type="primary" onClick={handleSubmit} block size="large">
              Continue to Dashboard
            </Button>
          </div>
        </div>

        <div style={{ marginTop: "24px", textAlign: "center" }}>
          <Text type="secondary" style={{ fontSize: "12px" }}>
            Your organization name will be stored locally in your browser
          </Text>
        </div>
      </Card>
    </div>
  );
};

export default OrganizationSetup;
