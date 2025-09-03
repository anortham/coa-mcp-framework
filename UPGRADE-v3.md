# Upgrade Guide: v2 → v3

This guide helps you migrate from v2.x to v3.0.

## Breaking Changes

1) Removed experimental middleware
- Deleted `TypeVerificationMiddleware`, `TddEnforcementMiddleware`, and related services/options.
- Action: Remove calls to `.AddTypeVerificationMiddleware(...)` and `.AddTddEnforcementMiddleware(...)` and any DI registrations of their services.

2) Validation simplification
- Removed custom validation (`ParameterValidationAttribute`, `IParameterValidator`, `DefaultParameterValidator`).
- Standardize on DataAnnotations: `[Required]`, `[Range]`, `[StringLength]`, `[RegularExpression]`, `[EmailAddress]`, `[Url]`.
- For fixed choices, prefer enums over custom attributes.

3) STDIO framing
- STDIO now uses `Content-Length` framing (LSP style). Fallback input supports single-line JSON for backward compatibility.
- Prefer using `StdioTransportOptions.InputStream/OutputStream` instead of `Input/Output`.

4) HTTP transport hardening
- CORS `AllowedOrigins` enforced. Disallowed origins receive 403 on preflight.
- `MaxRequestSize` enforced for `/mcp/rpc` POST.
- Authentication: Only `ApiKey` is enforced. Other modes log a warning and are treated as disabled in v3.

5) Typed response builders
- `BuildResponseAsync<TBuilder,TData>` now requires a typed builder (`BaseResponseBuilder<TData, TResult>`). Reflective invocation is removed.

6) Disposal
- Removed async finalizer path from `DisposableToolBase`. Use `IAsyncDisposable` and registry lifecycle instead.

## Recommended Changes

- Enable/disable validation globally with `McpFrameworkOptions.EnableValidation`.
- Use `ITokenEstimator` (DI) or default estimator for token-aware sizing. `McpToolBase.EstimateTokenUsage(parameters)` is available for higher-fidelity estimation.
- For HTTP auth, set `Authentication = ApiKey`, `ApiKey`, and optionally `ApiKeyHeader`.

## Code Examples

- Replace AllowedValues with enums:
```csharp
public enum Operation { add, subtract, multiply, divide }
public class Params { [Required] public Operation Op { get; set; } }
```

- Use typed response builders:
```csharp
var builder = new MyResponseBuilder();
return await BuildResponseAsync(builder, data, responseMode: "full");
```

- STDIO configuration:
```csharp
.UseStdioTransport(opts => {
    opts.InputStream = Console.OpenStandardInput();
    opts.OutputStream = Console.OpenStandardOutput();
});
```

## Protocol Version
- Centralized at `COA.Mcp.Protocol.McpProtocol.Version`. Keep client and server in sync.

## Testing
- Verify CORS behavior and request size limits for HTTP clients.
- Ensure client supports Content-Length framing for STDIO.
- Remove tests referencing removed middleware/custom validation.

## Notes
- v3 focuses on production readiness and simplicity (less magic, safer defaults, clearer APIs).
