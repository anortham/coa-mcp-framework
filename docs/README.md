# COA MCP Framework Documentation

Welcome to the comprehensive documentation for the COA MCP Framework - a complete .NET solution for building Model Context Protocol servers and clients.

## 📚 Documentation Index

### Getting Started
- [Framework Overview](../README.md) - Main project README with quick start
- [Installation Guide](#installation) - How to install and configure packages
- [Your First Tool](#first-tool) - Step-by-step tutorial
- [Project Templates](../src/COA.Mcp.Framework.Templates/README.md) - Quick project scaffolding
- [Migration to v2 Global Middleware](MIGRATION_GLOBAL_MIDDLEWARE.md) - Quick reference for constructor + middleware changes

### Core Packages

#### Essential
- [**COA.Mcp.Framework**](../src/COA.Mcp.Framework/README.md) - Core framework for building MCP tools
  - Base classes, validation, error handling
  - Prompts system, service management
  - Transport options (stdio, HTTP, WebSocket)
  
- [**COA.Mcp.Protocol**](../src/COA.Mcp.Protocol/README.md) - Low-level protocol implementation
  - JSON-RPC 2.0 messaging
  - MCP types and structures
  - Protocol compliance

#### Client Development
- [**COA.Mcp.Client**](../src/COA.Mcp.Client/README.md) - .NET client for MCP servers
  - Strongly-typed client operations
  - Fluent configuration API
  - Retry and timeout handling

#### Advanced Features
- [**COA.Mcp.Framework.TokenOptimization**](../src/COA.Mcp.Framework.TokenOptimization/README.md) - Token management
  - Token estimation and budgeting
  - Progressive reduction strategies
  - AI-optimized response formats
  - Response caching and storage

- [**COA.Mcp.Framework.Testing**](../src/COA.Mcp.Framework.Testing/README.md) - Testing utilities
  - Base test classes
  - Fluent assertions
  - Mock implementations
  - Performance benchmarks

#### Development Tools
- [**COA.Mcp.Framework.Templates**](../src/COA.Mcp.Framework.Templates/README.md) - Project templates
  - `dotnet new mcp-server` command
  - Pre-configured project structure
  - Docker and CI/CD support

- [**COA.Mcp.Framework.Migration**](../src/COA.Mcp.Framework.Migration/README.md) - Migration tools
  - Upgrade from older versions
  - Pattern analysis and migration
  - Breaking change detection

### Technical Documentation
- [Lifecycle Hooks & Middleware](lifecycle-hooks.md) - Comprehensive guide to middleware and tool execution hooks
- [Token Optimization Strategies](TOKEN_OPTIMIZATION_STRATEGIES.md) - Comprehensive token management guide  
- [Migration Example](MIGRATION_EXAMPLE.md) - Step-by-step migration guide with updated patterns
- [Validation & Error Handling](VALIDATION_AND_ERROR_HANDLING.md) - Best practices for tool validation
- [Common Pitfalls](COMMON_PITFALLS.md) - Known issues and solutions
- [Logging Configuration](LOGGING_CONFIGURATION.md) - Logging setup and best practices
- [Visualization Protocol](VISUALIZATION_PROTOCOL.md) - Rich UI data support
- [Transport Selection](WHICH_TRANSPORT.md) - Choosing stdio vs HTTP vs WebSocket

### Performance & Analysis
- [Performance Analysis](PERFORMANCE_ANALYSIS.md) - Framework performance characteristics
- [Performance Results](PERFORMANCE_RESULTS.md) - Benchmark data and metrics
- [Performance Final Results](PERFORMANCE_FINAL_RESULTS.md) - Latest performance testing

### Code Examples
- [API Client Tool Example](ApiClientTool.cs) - HTTP client tool implementation
- [File System Tool Example](FileSystemTool.cs) - File operations tool implementation

### Examples
- [SimpleMcpServer](../examples/SimpleMcpServer/README.md) - Basic server with example tools

## Quick Links by Role

### 👩‍💻 For Developers
1. [Quick Start](../README.md#-quick-start)
2. [Core Framework Docs](../src/COA.Mcp.Framework/README.md)
3. [Lifecycle Hooks & Middleware](lifecycle-hooks.md)
4. [Testing Guide](../src/COA.Mcp.Framework.Testing/README.md)
5. [Examples](../examples/SimpleMcpServer/)

### 🏗️ For Architects
1. [Token Optimization](../src/COA.Mcp.Framework.TokenOptimization/README.md)
2. [Transport Options](../src/COA.Mcp.Framework/README.md#transport-options)
3. [Service Management](../src/COA.Mcp.Framework/README.md#service-management)

### 🧪 For QA/Testing
1. [Testing Framework](../src/COA.Mcp.Framework.Testing/README.md)
2. [Performance Analysis](PERFORMANCE_ANALYSIS.md)
3. [Test Examples](../src/COA.Mcp.Framework.Testing/README.md#testing-a-tool)

### 🚀 For DevOps
1. [Docker Support](../src/COA.Mcp.Framework.Templates/README.md#dockerfile)
2. [Deployment Templates](../src/COA.Mcp.Framework.Templates/README.md)

## Version Information

- Refer to the project CHANGELOG for the latest released version and dates.
- Compatibility: .NET 8.0+ / .NET 9.0

## Getting Help

### Quick Answers

**Q: How do I create my first tool?**  
A: See [Core Framework README](../src/COA.Mcp.Framework/README.md#quick-start)

**Q: How do I test my tools?**  
A: See [Testing Framework README](../src/COA.Mcp.Framework.Testing/README.md)

**Q: How do I manage token limits?**  
A: See [Token Optimization README](../src/COA.Mcp.Framework.TokenOptimization/README.md)

**Q: How do I deploy to production?**  
A: See [Templates README](../src/COA.Mcp.Framework.Templates/README.md#dockerfile)

### Additional Resources
- [Main README](../README.md) - Project overview and getting started
- [CLAUDE.md](../CLAUDE.md) - Claude AI assistant guide and development practices

---

📝 **Note**: This documentation is actively maintained. For the latest updates, check the [GitHub repository](https://github.com/anortham/COA-Mcp-Framework).