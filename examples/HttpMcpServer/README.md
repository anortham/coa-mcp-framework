# HTTP/WebSocket MCP Server Example

This example demonstrates how to create an MCP server using HTTP and WebSocket transports with optional HTTPS support.

## Features

- HTTP API endpoints for MCP protocol
- WebSocket support for real-time bidirectional communication
- HTTPS/WSS support with SSL/TLS certificates
- CORS configuration for browser clients
- Example tools (Weather, Calculator, Time)
- HTML test client for browser testing

## Running the Server

### Basic HTTP Mode (Default)
```bash
dotnet run
```
Server runs on http://localhost:5000

### HTTPS Mode with Development Certificate
```bash
dotnet run -- --https
```
Server runs on https://localhost:5000 with a self-signed certificate

### HTTPS Mode with Custom Certificate
```bash
dotnet run -- --https --cert=path/to/certificate.pfx --cert-password=yourpassword
```

### Custom Port
```bash
dotnet run -- 8080
```

### Disable WebSocket
```bash
dotnet run -- --no-websocket
```

### Combined Options
```bash
dotnet run -- 8443 --https --cert=./certs/server.pfx --cert-password=pass123
```

## Command Line Options

- `[port]` - Port number (default: 5000)
- `--https` - Enable HTTPS mode
- `--cert=<path>` - Path to PFX certificate file
- `--cert-password=<password>` - Certificate password
- `--no-websocket` - Disable WebSocket support

## Creating a Certificate for Development

### Using OpenSSL
```bash
# Generate private key
openssl genrsa -out server.key 2048

# Generate certificate request
openssl req -new -key server.key -out server.csr

# Generate self-signed certificate
openssl x509 -req -days 365 -in server.csr -signkey server.key -out server.crt

# Convert to PFX
openssl pkcs12 -export -out server.pfx -inkey server.key -in server.crt
```

### Using PowerShell (Windows)
```powershell
# Create self-signed certificate
$cert = New-SelfSignedCertificate -DnsName "localhost" -CertStoreLocation "cert:\LocalMachine\My"

# Export to PFX
$password = ConvertTo-SecureString -String "password123" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath ".\server.pfx" -Password $password
```

### Using dotnet dev-certs
```bash
# Generate development certificate
dotnet dev-certs https -ep ./server.pfx -p password123

# Trust the certificate (optional, for development)
dotnet dev-certs https --trust
```

## Testing the Server

### Using the HTML Client
1. Start the server
2. Open `wwwroot/index.html` in a browser
3. Test HTTP endpoints and WebSocket connections

### Using curl (HTTP)
```bash
# Health check
curl http://localhost:5000/mcp/health

# List tools
curl http://localhost:5000/mcp/tools

# Call a tool
curl -X POST http://localhost:5000/mcp/rpc \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "get_weather",
      "arguments": {"location": "Seattle"}
    },
    "id": 1
  }'
```

### Using curl (HTTPS with self-signed cert)
```bash
# Add -k flag to ignore certificate validation
curl -k https://localhost:5000/mcp/health
```

### Using WebSocket Client
```javascript
// JavaScript example
const ws = new WebSocket('ws://localhost:5000/mcp/ws');

ws.onopen = () => {
    console.log('Connected');
    ws.send(JSON.stringify({
        jsonrpc: "2.0",
        method: "initialize",
        params: {
            protocolVersion: "2024-11-05", // Keep in sync with server's MCP protocol version
            capabilities: {},
            clientInfo: {
                name: "Test Client",
                version: "1.0.0"
            }
        },
        id: 1
    }));
};

ws.onmessage = (event) => {
    console.log('Received:', event.data);
};
```

## Production Deployment

### Certificate Requirements
- Use a certificate from a trusted Certificate Authority (CA)
- Ensure the certificate matches your domain name
- Keep the private key secure

### Security Considerations
1. **Never use self-signed certificates in production**
2. **Enable authentication** (API Key, JWT, etc.)
3. **Configure CORS** to only allow trusted origins
4. **Use HTTPS exclusively** in production
5. **Implement rate limiting** to prevent abuse
6. **Log and monitor** all requests

### Running as a Windows Service
```powershell
# Install as service
sc create "MCPServer" binPath= "C:\path\to\HttpMcpServer.exe --https --cert=C:\certs\server.pfx"

# Start service
sc start MCPServer
```

### Running in Docker
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY bin/Release/net9.0/publish/ .

# Copy certificate
COPY certs/server.pfx /certs/server.pfx

EXPOSE 5000
EXPOSE 5001

ENTRYPOINT ["dotnet", "HttpMcpServer.dll", "--https", "--cert=/certs/server.pfx"]
```

## Troubleshooting

### Certificate Issues
- **"Access Denied"**: Run as Administrator when binding certificates on Windows
- **"Certificate not trusted"**: Add certificate to trusted root store for development
- **"Port already in use"**: Check if another service is using the port

### HTTPS on Windows
Windows requires certificates to be bound to ports using `netsh`:
```cmd
netsh http add sslcert ipport=0.0.0.0:5000 certhash=<thumbprint> appid={guid}
```
The HttpTransport handles this automatically when running with appropriate permissions.

### WebSocket Connection Failed
- Ensure WebSocket is enabled (`--no-websocket` not used)
- Check firewall rules
- For HTTPS, use `wss://` instead of `ws://`

## Example Tools

### Weather Tool
Gets mock weather data for a location:
```json
{
  "name": "get_weather",
  "arguments": {"location": "Seattle"}
}
```

### Calculator Tool
Performs basic math operations:
```json
{
  "name": "calculate",
  "arguments": {
    "operation": "add",
    "a": 10,
    "b": 5
  }
}
```

### Time Tool
Gets current time in various timezones:
```json
{
  "name": "get_time",
  "arguments": {"timezone": "PST"}
}
```

## License

This example is part of the COA MCP Framework and is licensed under the same terms.
