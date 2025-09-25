# Azure DevOps PR Review AI - Frontend

A modern React TypeScript frontend application for managing AI-powered code review configurations in Azure DevOps repositories.

## Features

- **Repository Management**: Browse and select repositories for configuration
- **Configuration Editor**: Comprehensive form-based editor for all AI review settings
- **Review Rules**: Define custom rules for code quality, security, performance analysis
- **File Exclusions**: Configure patterns to exclude files from AI review
- **Custom Prompts**: Create specialized AI prompts for different review scenarios
- **Import/Export**: Backup and share configurations via JSON export/import
- **Configuration Cloning**: Copy settings between repositories
- **Real-time Validation**: Client-side and server-side validation with helpful error messages

## Technology Stack

- **Framework**: React 18 with TypeScript
- **Build Tool**: Vite for fast development and builds
- **UI Library**: Ant Design (antd) for professional enterprise components
- **Form Management**: React Hook Form with Zod validation
- **State Management**: TanStack Query for server state + React Context
- **HTTP Client**: Axios with interceptors
- **Routing**: React Router v6
- **Styling**: CSS-in-JS with Ant Design theming

## Getting Started

### Prerequisites

- Node.js 18+
- npm or yarn
- Azure DevOps PR Review AI backend running

### Installation

1. Navigate to the frontend directory:
```bash
cd frontend
```

2. Install dependencies:
```bash
npm install
```

3. Configure environment variables:
```bash
cp .env.example .env
```

Edit `.env` to set your backend API URL:
```
VITE_API_BASE_URL=http://localhost:5000/api
```

4. Start the development server:
```bash
npm run dev
```

The application will be available at `http://localhost:5173`

### Building for Production

```bash
npm run build
```

The built files will be in the `dist` directory.

## Project Structure

```
src/
├── components/          # Reusable UI components
│   ├── forms/          # Configuration form components
│   ├── layout/         # Layout components (header, sidebar)
│   ├── prompts/        # Custom prompt management
│   ├── rules/          # Rule management components
│   └── import-export/  # Import/export functionality
├── hooks/              # Custom React hooks
├── pages/              # Route components
├── services/           # API service layer
├── types/              # TypeScript type definitions
├── utils/              # Utility functions and validation
├── styles/             # Global styles
└── contexts/           # React contexts
```

## Key Components

### Repository Selector
- Search and browse Azure DevOps repositories
- Quick access to existing configurations
- Create new configurations from templates

### Configuration Editor
- Tabbed interface for different configuration sections
- Real-time validation and error handling
- Auto-save functionality with unsaved changes warning

### Rule Management
- Visual rule editor with drag-and-drop reordering
- Support for different rule types (security, performance, style)
- File pattern matching and exclusion patterns

### Import/Export System
- JSON-based configuration backup/restore
- Configuration cloning between repositories
- Validation of imported configurations

## API Integration

The frontend communicates with the ASP.NET Core backend via REST APIs for full CRUD operations on configurations.

## License

This project is part of the Azure DevOps PR Review AI system.