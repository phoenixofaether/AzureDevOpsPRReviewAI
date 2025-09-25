import {
  FolderOutlined,
  PlusOutlined,
  SearchOutlined,
  SettingOutlined,
} from "@ant-design/icons";
import { useQuery } from "@tanstack/react-query";
import {
  Alert,
  Button,
  Card,
  Divider,
  Empty,
  Form,
  Input,
  List,
  Space,
  Tag,
  Typography,
} from "antd";
import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import { configurationApi } from "../services/api";
import type { RepositoryConfiguration } from "../types/configuration";

const { Title, Text } = Typography;

const RepositorySelector: React.FC = () => {
  const navigate = useNavigate();
  const [form] = Form.useForm();
  const [searchQuery, setSearchQuery] = useState("");

  const { data: organizationConfigs, isLoading: isLoadingOrg } = useQuery({
    queryKey: ["organization-configs", searchQuery],
    queryFn: async () => {
      if (!searchQuery) return [];
      try {
        return await configurationApi.getOrganizationConfigurations(
          searchQuery
        );
      } catch {
        return [];
      }
    },
    enabled: !!searchQuery,
  });

  const handleSearch = async (values: { organization: string }) => {
    setSearchQuery(values.organization);
  };

  const handleNavigateToConfig = (config: RepositoryConfiguration) => {
    navigate(
      `/configuration/${encodeURIComponent(
        config.organization
      )}/${encodeURIComponent(config.project)}/${encodeURIComponent(
        config.repository
      )}`
    );
  };

  const handleCreateNew = () => {
    const values = form.getFieldsValue();
    if (values.organization && values.project && values.repository) {
      navigate(
        `/configuration/${encodeURIComponent(
          values.organization
        )}/${encodeURIComponent(values.project)}/${encodeURIComponent(
          values.repository
        )}`
      );
    }
  };

  return (
    <div style={{ maxWidth: "800px", margin: "0 auto" }}>
      <Card>
        <Space direction="vertical" size="large" style={{ width: "100%" }}>
          <div style={{ textAlign: "center" }}>
            <Title level={2}>
              <FolderOutlined /> Repository Configuration Manager
            </Title>
            <Text type="secondary">
              Select or create a repository configuration for AI code reviews
            </Text>
          </div>

          <Divider />

          <div>
            <Title level={4}>Search Existing Configurations</Title>
            <Form form={form} onFinish={handleSearch} layout="vertical">
              <Space
                direction="vertical"
                size="middle"
                style={{ width: "100%" }}
              >
                <Form.Item
                  name="organization"
                  label="Organization"
                  rules={[
                    {
                      required: true,
                      message: "Please enter an organization name",
                    },
                  ]}
                >
                  <Input
                    placeholder="Enter Azure DevOps organization name"
                    prefix={<SearchOutlined />}
                  />
                </Form.Item>

                <Button
                  type="primary"
                  htmlType="submit"
                  icon={<SearchOutlined />}
                  loading={isLoadingOrg}
                >
                  Search Configurations
                </Button>
              </Space>
            </Form>
          </div>

          {searchQuery && (
            <>
              <Divider />
              <div>
                <Title level={4}>Configurations for "{searchQuery}"</Title>
                {isLoadingOrg ? (
                  <div className="loading-center" style={{ height: "200px" }}>
                    Loading configurations...
                  </div>
                ) : organizationConfigs && organizationConfigs.length > 0 ? (
                  <List
                    dataSource={organizationConfigs}
                    renderItem={(config) => (
                      <List.Item
                        actions={[
                          <Button
                            key="configure"
                            type="primary"
                            icon={<SettingOutlined />}
                            onClick={() => handleNavigateToConfig(config)}
                          >
                            Configure
                          </Button>,
                        ]}
                      >
                        <List.Item.Meta
                          avatar={
                            <FolderOutlined style={{ fontSize: "24px" }} />
                          }
                          title={
                            <Space>
                              <Text strong>{config.repository}</Text>
                              <Tag color={config.isEnabled ? "green" : "red"}>
                                {config.isEnabled ? "Enabled" : "Disabled"}
                              </Tag>
                            </Space>
                          }
                          description={
                            <Space direction="vertical" size="small">
                              <Text type="secondary">
                                Project: {config.project}
                              </Text>
                              <Text type="secondary" className="text-small">
                                Last updated:{" "}
                                {new Date(
                                  config.updatedAt
                                ).toLocaleDateString()}
                                {config.updatedBy && ` by ${config.updatedBy}`}
                              </Text>
                            </Space>
                          }
                        />
                      </List.Item>
                    )}
                  />
                ) : (
                  <Empty
                    description="No configurations found for this organization"
                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                  />
                )}
              </div>
            </>
          )}

          <Divider />

          <div>
            <Title level={4}>Create New Configuration</Title>
            <Alert
              message="Create a new configuration for a repository"
              description="Enter the organization, project, and repository details to create a new AI review configuration."
              type="info"
              style={{ marginBottom: "16px" }}
            />

            <Space direction="vertical" size="middle" style={{ width: "100%" }}>
              <Form.Item
                name="project"
                label="Project"
                rules={[
                  { required: true, message: "Please enter a project name" },
                ]}
              >
                <Input placeholder="Enter Azure DevOps project name" />
              </Form.Item>

              <Form.Item
                name="repository"
                label="Repository"
                rules={[
                  { required: true, message: "Please enter a repository name" },
                ]}
              >
                <Input placeholder="Enter repository name" />
              </Form.Item>

              <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={handleCreateNew}
                disabled={!form.getFieldValue("organization")}
              >
                Create New Configuration
              </Button>
            </Space>
          </div>
        </Space>
      </Card>
    </div>
  );
};

export default RepositorySelector;
