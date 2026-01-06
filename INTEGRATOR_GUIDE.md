# Enterprise MCP Server - Integrator Guide

This guide is for engineers and system integrators who need to configure, deploy, and customize the enterprise MCP server.

## ⚠️ **CRITICAL SECURITY NOTICE**

**External Identity Provider Integration Status:**

- ✅ **Azure AD**: Complete production-ready implementation with real JWKS validation
- ⚠️ **AWS Cognito**: Configuration ready, token validation PLACEHOLDER  
- ⚠️ **Auth0**: Configuration ready, token validation PLACEHOLDER
- ⚠️ **LDAP**: Configuration ready, directory authentication PLACEHOLDER

**Production Ready for Azure AD ResourceServer mode** - full JWT validation implemented.

**Production-Ready Features:**
- ✅ **AuthorizationServer mode**: Full OAuth 2.1 with local user management
- ✅ **ResourceServer mode with Azure AD**: Real JWT validation with JWKS
- ✅ **WebAuthn integration**: Complete biometric/security key support  
- ✅ **Rate limiting**: Enterprise IP-based request throttling
- ✅ **Hybrid mode**: Fallback between local and Azure AD auth
- ✅ **Disabled mode**: For development/testing only

## 🚀 **Automated Setup Scripts**

### **Azure AD Integration (Production Ready)**

We provide **automated setup scripts** that handle the complete Azure AD integration:

#### **Prerequisites**
1. **Azure CLI**: Install from [Microsoft Docs](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
   ```bash
   # macOS
   brew install azure-cli
   
   # Windows
   winget install Microsoft.AzureCLI
   
   # Linux
   curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
   ```

2. **Login to Azure**:
   ```bash
   az login
   ```

#### **Automated Setup Options**

**Option 1: Full Automated Setup (Linux/macOS)**
```bash
cd scripts
./setup-azure-ad.sh
```

**Option 2: PowerShell Setup (Windows)**
```powershell
cd scripts
.\Setup-AzureAD.ps1  
```

**Option 3: Interactive Configuration (Cross-Platform)**
```bash
cd scripts
node configure-azure-ad.js
```

#### **What the Scripts Do**
- ✅ Create Azure AD app registration with proper scopes
- ✅ Generate client secret securely
- ✅ Configure MCP-specific OAuth scopes (mcp:tools, mcp:math, mcp:admin)
- ✅ Generate complete `appsettings.azuread.json` configuration
- ✅ Create test scripts for verification
- ✅ Provide Azure Portal links for admin consent

#### **After Running Setup Script**
1. **Grant admin consent** in Azure Portal (link provided by script)
2. **Start server**: `dotnet run --configuration appsettings.azuread.json`
3. **Test integration**: Run the generated test script

**Result**: Your MCP server is now integrated with Azure AD and validates real tokens with scope-based authorization!

## 🔧 Configuration Options

### Authentication Modes

The server supports multiple authentication modes via `appsettings.json`:

```json
{
  "Authentication": {
    "Mode": "AuthorizationServer" // or "Disabled", "ResourceServer", "Hybrid", "ZeroTrust"
  }
}
```

#### Authentication Mode Details

| Mode | Use Case | Description |
|------|----------|-------------|
| `Disabled` | **Development/Testing** | No authentication required |
| `ResourceServer` | **External IdP** | Validates tokens from external identity providers |
| `AuthorizationServer` | **Self-Contained** | Full OAuth 2.1 server with local user management |
| `Hybrid` | **Mixed Environment** | Supports both local and external authentication |
| `ZeroTrust` | **High Security** | Continuous validation with enhanced security checks |

### OAuth 2.1 Configuration

#### Basic Development Configuration

```json
{
  "Authentication": {
    "OAuth": {
      "Issuer": "http://localhost:3001",
      "AccessTokenLifetime": "00:15:00",
      "RefreshTokenLifetime": "30.00:00:00",
      "EnableDynamicClientRegistration": true,
      "RequireClientApproval": false,
      "RequireClientCertificates": false,
      "Signing": {
        "Algorithm": "RS256",
        "KeyRotationInterval": "90.00:00:00",
        "UseHSM": false
      }
    }
  }
}
```

#### Production Issuer Configuration

The `Issuer` URL is the OAuth 2.1 authorization server identifier used for token validation. Configure based on your deployment architecture:

**Enterprise Domain:**
```json
"Issuer": "https://auth.company.com"
```

**API Gateway/Proxy:**
```json
"Issuer": "https://api-gateway.company.com/auth"
```

**Internal DNS:**
```json
"Issuer": "https://mcp-auth.internal.corp"
```

**Key Requirements:**
- Must be accessible by all clients validating tokens
- Should use HTTPS in production environments
- Must match exactly between token issuance and validation
- Supports OAuth 2.0 Authorization Server Metadata discovery at `{Issuer}/.well-known/oauth-authorization-server`

**Environment-Specific Examples:**
- **Development**: `http://localhost:3001`
- **Staging**: `https://auth-staging.company.com`  
- **Production**: `https://auth.company.com`

### WebAuthn Configuration

```json
{
  "Authentication": {
    "WebAuthn": {
      "Enabled": true,
      "ServerDomain": "localhost",
      "ServerName": "Enterprise MCP Server",
      "AllowedOrigins": ["http://localhost:3001"],
      "RequireAttestationValidation": false,
      "AllowedAuthenticatorTypes": ["platform", "cross-platform"],
      "RequireUserVerification": true,
      "ChallengeTimeout": "00:02:00"
    }
  }
}
```

### Security Configuration

```json
{
  "Authentication": {
    "Security": {
      "EnableAuditLogging": true,
      "EnableThreatDetection": false,
      "ComplianceFrameworks": [],
      "RateLimit": {
        "Enabled": true,
        "RequestsPerMinute": 60,
        "RequestsPerHourPerUser": 1000,
        "BurstAllowance": 10
      }
    }
  }
}
```

## 🔌 Client Integration

### Claude Code Integration

**For Authentication Enabled:**
```bash
# Connect with OAuth authentication
claude mcp add remote-math-server --scope project npx mcp-remote http://localhost:3001/

# Claude Code will automatically detect OAuth and initiate browser-based authentication
```

**For Authentication Disabled:**
```bash
# Direct connection (testing only)
claude mcp add remote-math-server --scope project npx mcp-remote http://localhost:3001/
```

### Other MCP Clients

**MCP Inspector:**
```bash
npx @modelcontextprotocol/inspector@latest
# Connect to: http://localhost:3001/
```

**Cursor IDE:**
Add to your MCP configuration:
```json
{
  "mcpServers": {
    "remote-math-server": {
      "command": "npx",
      "args": ["mcp-remote", "http://localhost:3001/"]
    }
  }
}
```

## 🔐 Authentication Flows

### OAuth 2.1 Flow (Default)

1. **Client Registration**: Automatic via `/register` endpoint
2. **Authorization**: User visits `/authorize` with choice of:
   - Password authentication
   - WebAuthn (biometric/security key)
3. **Token Exchange**: Authorization code exchanged for JWT access token
4. **MCP Access**: Bearer token used for MCP endpoint access

### WebAuthn Flow

1. **Registration**: Visit `http://localhost:3001/webauthn/register`
2. **Credential Creation**: Browser prompts for biometric/security key
3. **OAuth Integration**: Use WebAuthn option in OAuth authorization
4. **Authentication**: Biometric validation replaces password

## 🛠️ Development & Testing

### Quick Testing Commands

```bash
# Test server health
curl http://localhost:3001/health

# Test OAuth discovery
curl http://localhost:3001/.well-known/oauth-authorization-server

# Test WebAuthn registration page
open http://localhost:3001/webauthn/register

# Test MCP without authentication (when disabled)
curl -X POST http://localhost:3001/ -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}'
```

### Test Credentials

**Password Authentication:**
- Username: `test.user@company.com`
- Password: `testpass`

**Additional Test User:**
- Username: `admin.user@company.com`  
- Password: `adminpass`

### Switching Authentication Modes

1. **Edit** `appsettings.json`
2. **Change** the `Authentication.Mode` value
3. **Restart** the server with `dotnet run`

No code changes required - the server adapts at runtime.

## 🌐 Network Deployment

### Local Network Access

The server binds to `0.0.0.0:3001` for network access:

```bash
# Find your IP address
hostname -I  # Linux/macOS
ipconfig     # Windows

# Connect from other machines
http://YOUR_IP_ADDRESS:3001/
```

### Production Deployment

**Environment Variables:**
```bash
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS=https://0.0.0.0:3001
```

**HTTPS Configuration:**
```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://0.0.0.0:3001",
        "Certificate": {
          "Path": "certificate.pfx",
          "Password": "cert_password"
        }
      }
    }
  }
}
```

### Docker Deployment

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY bin/Release/net9.0/publish/ .
EXPOSE 3001
ENTRYPOINT ["dotnet", "remote-mcp.dll"]
```

## 🔒 Security Considerations

### Production Security Checklist

- [ ] **HTTPS Only**: Configure SSL certificates
- [ ] **Authentication**: Never use `Disabled` mode in production
- [ ] **CORS**: Restrict `AllowAnyOrigin()` to specific domains
- [ ] **Rate Limiting**: Enable and tune rate limits
- [ ] **Audit Logging**: Enable comprehensive audit logging
- [ ] **Firewall**: Restrict port 3001 access to authorized networks
- [ ] **Key Management**: Use HSM for production signing keys
- [ ] **Certificate Validation**: Enable WebAuthn attestation validation

### Enterprise Integration

**Active Directory Integration:**
- Replace test users with AD authentication
- Implement `IEnterpriseWebAuthnService` with AD user lookup
- Configure multi-tenant support for organization isolation

**External Identity Providers:**
- Set mode to `ResourceServer`
- Configure token validation for external JWT tokens
- Implement custom token validation logic

## 🛠️ Customization

### Adding New Tools

Create new tool files in `Tools/` directory:

```csharp
[McpServerToolType]
public static class CustomTools
{
    [McpServerTool, Description("Your custom tool description")]
    public static string CustomTool([Description("Parameter description")] string input)
    {
        // Your implementation
        return $"Result: {input}";
    }
}
```

Tools are automatically discovered via assembly scanning.

### Custom Authentication

Implement custom authentication by:

1. **Creating** custom `IAuthenticationModeProvider`
2. **Registering** in dependency injection
3. **Configuring** new authentication mode

### Custom UI Themes

Modify the HTML templates in:
- `OAuthImplementation.CreateLoginForm()` - OAuth login page
- `WebAuthnEndpoints.CreateRegistrationPage()` - WebAuthn registration

## 📊 Monitoring & Logging

### Log Levels

Configure in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Authentication": "Debug"  // For detailed auth logging
    }
  }
}
```

### Health Monitoring

- **Health Check**: `GET /health`
- **Server Info**: `GET /info`
- **OAuth Discovery**: `GET /.well-known/oauth-authorization-server`

### Audit Events

When audit logging is enabled, the server logs:
- Authentication attempts and failures
- Token issuance and validation
- MCP tool access attempts
- WebAuthn registration and authentication events

## 🤝 Support

### Common Issues

**Authentication Fails:**
- Check `Authentication.Mode` in `appsettings.json`
- Verify test credentials are correct
- Check server logs for detailed error messages

**WebAuthn Not Working:**
- Ensure HTTPS for production (required for WebAuthn)
- Check browser WebAuthn support
- Verify device has biometric capabilities enabled

**Claude Code Connection Issues:**
- Verify server is running on port 3001
- Check authentication mode configuration
- Try clearing Claude Code's MCP authentication cache

### Getting Help

- **Issues**: Report at GitHub repository
- **Documentation**: See `CLAUDE.md` for development details
- **Logs**: Check server output for detailed error information