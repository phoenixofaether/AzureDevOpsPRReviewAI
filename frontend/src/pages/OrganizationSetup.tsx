import { TeamOutlined } from "@ant-design/icons";
import { Button, Card, Form, Input, Typography } from "antd";
import React from "react";
import { useOrganization } from "../contexts/OrganizationContext";

const { Title, Text } = Typography;

const OrganizationSetup: React.FC = () => {
  const { setOrganization } = useOrganization();
  const [form] = Form.useForm();

  const handleSubmit = (values: { organization: string }) => {
    setOrganization(values.organization.trim());
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

        <Form
          form={form}
          onFinish={handleSubmit}
          layout="vertical"
          size="large"
        >
          <Form.Item
            name="organization"
            label="Azure DevOps Organization"
            rules={[
              {
                required: true,
                message: "Please enter your organization name",
              },
              { min: 1, message: "Organization name cannot be empty" },
              {
                pattern: /^[a-zA-Z0-9\-_]+$/,
                message:
                  "Only letters, numbers, hyphens, and underscores are allowed",
              },
            ]}
            extra="Enter the organization name from your Azure DevOps URL (e.g., 'mycompany' from https://dev.azure.com/mycompany)"
          >
            <Input
              placeholder="Enter organization name"
              prefix={<TeamOutlined />}
              autoFocus
            />
          </Form.Item>

          <Form.Item style={{ marginBottom: 0 }}>
            <Button type="primary" htmlType="submit" block size="large">
              Continue to Dashboard
            </Button>
          </Form.Item>
        </Form>

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
