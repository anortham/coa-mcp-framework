# HTTP Authentication Configuration

This framework supports multiple server-side authentication modes for the HTTP transport.

The `/mcp/rpc` endpoint enforces the configured mode. Health (`/mcp/health`) and other diagnostics remain open.

## CORS

When `EnableCors = true`, the server enforces `AllowedOrigins`. Disallowed origins receive `403` on preflight.

```csharp
.UseHttpTransport(options =>
{
    options.Host = "localhost";
    options.Port = 5000;
    options.EnableCors = true;
    options.AllowedOrigins = new[] { "http://localhost:3000", "https://myapp.example.com" };
});
```

## API Key

Use a static API key in a custom header (default: `X-API-Key`).

```csharp
.UseHttpTransport(options =>
{
    options.Authentication = AuthenticationType.ApiKey;
    options.ApiKey = "super-secret-api-key";
    options.ApiKeyHeader = "X-API-Key"; // optional, default shown
});
```

Client request:
```
POST /mcp/rpc
X-API-Key: super-secret-api-key
Content-Type: application/json

{"jsonrpc":"2.0","method":"tools/list","id":1}
```

## Basic Auth

Enable simple username/password for environments that need it.

```csharp
.UseHttpTransport(options =>
{
    options.Authentication = AuthenticationType.Basic;
    options.BasicUsername = "mcp";
    options.BasicPassword = "change-me";
});
```

Client request:
```
Authorization: Basic base64(username:password)
```

Example (PowerShell):
```powershell
$pair = "mcp:change-me"
$bytes = [System.Text.Encoding]::UTF8.GetBytes($pair)
$token = [Convert]::ToBase64String($bytes)
curl http://localhost:5000/mcp/rpc -H "Authorization: Basic $token" -H "Content-Type: application/json" -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
```

## JWT (HS256)

Validate `Authorization: Bearer` tokens signed with HMAC-SHA256 (HS256).

```csharp
.UseHttpTransport(options =>
{
    options.Authentication = AuthenticationType.Jwt;
    options.JwtSettings = new JwtSettings
    {
        SecretKey = "your-hs256-secret", // required
        Issuer = "my-issuer",             // optional
        Audience = "my-audience"          // optional
    };
});
```

Client request:
```
Authorization: Bearer <jwt>
```

Notes:
- The server verifies HS256 signature, and optionally `exp`, `iss`, and `aud` if configured.
- This is a minimal implementation for common service-to-service scenarios. For advanced use, consider a full JWT library, key rotation, and stronger validation strategies.

## Request Size Limits

Large request bodies are rejected with `413`:
```csharp
options.MaxRequestSize = 10 * 1024 * 1024; // default 10MB
```

## Mixed Modes / Custom

`AuthenticationType.Custom` is reserved and not enforced in v3. If configured, the server logs a one-time warning and treats it as disabled.

## Health Check

`/mcp/health` is intentionally unauthenticated for operational readiness checks.

