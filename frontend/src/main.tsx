import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ConfigProvider } from "antd";
import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App.tsx";
import "./styles/global.css";

// Create a QueryClient instance
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 3,
      staleTime: 5 * 60 * 1000, // 5 minutes
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: 1,
    },
  },
});

// Ant Design theme configuration
const theme = {
  token: {
    colorPrimary: "#1890ff",
    colorSuccess: "#52c41a",
    colorWarning: "#faad14",
    colorError: "#ff4d4f",
    borderRadius: 6,
    fontSize: 14,
  },
  components: {
    Layout: {
      headerBg: "#001529",
      siderBg: "#001529",
    },
    Menu: {
      darkItemBg: "#001529",
      darkSubMenuItemBg: "#001529",
    },
  },
};

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <ConfigProvider theme={theme}>
        <BrowserRouter>
          <App />
        </BrowserRouter>
      </ConfigProvider>
    </QueryClientProvider>
  </React.StrictMode>
);
