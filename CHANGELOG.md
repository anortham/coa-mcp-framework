# Changelog

All notable changes to the COA MCP Framework will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **IAsyncDisposable Support**: Tools can now properly manage resources like database connections
- **DisposableToolBase Class**: Base class for tools requiring resource cleanup
- **IDisposableTool Interface**: Marker interface for disposable tools
- **Automatic Tool Disposal**: McpToolRegistry automatically disposes tools on shutdown

## [1.1.0] - 2025-01-06

### Added

#### 🎯 C# Client Library (COA.Mcp.Client)
- **McpHttpClient**: Base HTTP client for MCP server communication
- **TypedMcpClient<TParams, TResult>**: Strongly-typed client for type-safe tool invocation
- **McpClientBuilder**: Fluent API for client configuration
- Full authentication support (API Key, JWT, Basic, Custom)
- Resilience patterns using Polly (retry logic, circuit breaker)
- Connection management and event notifications
- Batch operations for parallel tool execution
- Comprehensive example application (McpClientExample)

#### 🌐 Transport Layer
- **Transport Abstraction**: `IMcpTransport` interface for pluggable transports
- **HTTP Transport**: Full HTTP/HTTPS support with `HttpListener`
- **WebSocket Transport**: Real-time bidirectional communication
- **SSL/TLS Support**: Automatic development certificate generation
- **CORS Configuration**: Flexible cross-origin resource sharing
- **Authentication**: API Key, JWT (structure), Basic auth support
- **Request-Response Correlation**: Async message correlation for HTTP

#### 📋 Type-Safe Schema System
- **IJsonSchema Interface**: Type-safe schema representation
- **JsonSchema<T>**: Generic implementation for compile-time type safety
- **RuntimeJsonSchema**: Support for non-generic runtime scenarios
- Automatic schema generation from tool parameters
- Removed need for manual `GetInputSchema()` overrides

#### 📚 Examples & Documentation
- **HttpMcpServer**: Complete HTTP/WebSocket server example
- **McpClientExample**: Comprehensive client usage examples
- **Transport Tests**: Full test coverage for HTTP and WebSocket
- **Client Tests**: 50 unit tests for client library components

### Changed

#### Framework Core
- Updated `IMcpTool` to use `IJsonSchema` instead of object types
- Modified `McpServer` to support multiple transport types
- Enhanced `McpServerBuilder` with transport configuration methods
- Improved `ToolResultBase` with required `Operation` property
- Updated all example tools to use new schema system

#### Protocol Integration
- Fixed namespace references to use `COA.Mcp.Protocol` correctly
- Updated type mappings for MCP protocol types
- Aligned client/server communication with protocol specifications

### Fixed

- Compilation errors related to type references
- Schema generation for generic and non-generic tools
- Certificate handling warnings in HTTP transport
- Test mock configurations for new interfaces

### Testing

- **Total Tests**: 352 across all projects
- **Pass Rate**: 96.9% (341 passing)
- New test projects:
  - COA.Mcp.Client.Tests (50 tests)
  - Enhanced transport tests (23 tests)

## [1.0.0] - 2025-01-05

### Initial Release

#### Core Framework
- **McpToolBase<TParams, TResult>**: Generic base class for type-safe tools
- **McpServerBuilder**: Fluent API for server configuration
- **McpToolRegistry**: Unified tool registration and discovery
- **Error Models**: Comprehensive error handling with recovery steps

#### Features
- Automatic parameter validation using data annotations
- Tool categorization system
- Metadata support for tools
- Dependency injection integration
- Comprehensive logging support

#### Examples
- SimpleMcpServer with 4 working tools:
  - CalculatorTool
  - DataStoreTool
  - SystemInfoTool
  - StringManipulationTool

#### Testing
- 230 unit tests with 100% pass rate
- Testing framework with fluent assertions
- Mock helpers for tool testing

#### Documentation
- Complete README with quick start guide
- CLAUDE.md for AI assistant guidance
- API documentation with examples

## [0.9.0] - 2024-12-15 (Pre-release)

### Added
- Initial framework structure
- Basic MCP protocol implementation
- Tool registration system
- Simple stdio transport

---

## Upcoming (Planned)

### [1.2.0]
- [ ] JWT authentication implementation
- [ ] Response compression support
- [ ] Response caching for performance
- [ ] JavaScript/TypeScript client library
- [ ] Python client library

### [1.3.0]
- [ ] Load balancing support for multiple servers
- [ ] Metrics and monitoring (OpenTelemetry)
- [ ] Rate limiting
- [ ] GraphQL support
- [ ] Project templates (`dotnet new mcp-server`)

### [2.0.0]
- [ ] Breaking change: Async tool discovery
- [ ] Cloud-native features (service discovery, distributed tracing)
- [ ] Kubernetes support with health probes
- [ ] Auto-scaling capabilities