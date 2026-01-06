#!/bin/bash

# Azure AD Enterprise Setup Script
# Configures Azure AD app registration with proper scoped authentication for MCP service
# Follows Microsoft enterprise standards and OAuth 2.1 compliance

set -e

# Configuration validation
if [ ! -f "appsettings.json" ]; then
    echo "❌ appsettings.json not found. Run from project root directory."
    exit 1
fi

# Check dependencies
command -v az >/dev/null 2>&1 || { echo "❌ Azure CLI (az) is required but not installed."; exit 1; }
command -v jq >/dev/null 2>&1 || { echo "❌ jq is required but not installed."; exit 1; }

# Function to load and display configuration
load_and_show_config() {
    # Extract configuration from appsettings.json
    SERVER_PORT=$(cat appsettings.json | jq -r '.Server.Port // 3001')
    SERVER_HOST=$(cat appsettings.json | jq -r '.Server.Host // "localhost"')
    OAUTH_ISSUER=$(cat appsettings.json | jq -r '.Authentication.OAuth.Issuer // "http://localhost:3001"')
    TENANT_ID=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.TenantId // empty')
    CLIENT_ID=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.ClientId // empty')

    echo "🚀 Azure AD Enterprise Setup for Remote MCP Server"
    echo "Configuration:"
    echo "  Server: ${SERVER_HOST}:${SERVER_PORT}"
    echo "  OAuth Issuer: ${OAUTH_ISSUER}"
    echo "  Tenant ID: ${TENANT_ID:-"<will be created>"}"
    echo "  Client ID: ${CLIENT_ID:-"<will be created>"}"
}

# Function to load configuration silently
load_config() {
    SERVER_PORT=$(cat appsettings.json | jq -r '.Server.Port // 3001')
    SERVER_HOST=$(cat appsettings.json | jq -r '.Server.Host // "localhost"')
    OAUTH_ISSUER=$(cat appsettings.json | jq -r '.Authentication.OAuth.Issuer // "http://localhost:3001"')
    TENANT_ID=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.TenantId // empty')
    CLIENT_ID=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.ClientId // empty')
}

# Function to create or update Azure AD app registration
setup_azure_app() {
    local app_name="Remote MCP Server"
    local redirect_uri_http="http://${SERVER_HOST}:${SERVER_PORT}/oauth/callback"
    local redirect_uri_https="https://${SERVER_HOST}:${SERVER_PORT}/oauth/callback"
    
    echo "📝 Setting up Azure AD app registration..."
    
    if [ -n "$CLIENT_ID" ] && [ "$CLIENT_ID" != "null" ]; then
        echo "✅ Using existing app registration: $CLIENT_ID"
        
        # Update existing app with correct redirect URIs
        az ad app update \
            --id "$CLIENT_ID" \
            --web-redirect-uris "$redirect_uri_http" "$redirect_uri_https" \
            --required-resource-accesses '[
                {
                    "resourceAppId": "00000003-0000-0000-c000-000000000000",
                    "resourceAccess": [
                        {
                            "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
                            "type": "Scope"
                        }
                    ]
                }
            ]'
        
        echo "✅ Updated app registration with redirect URIs: $redirect_uri_http, $redirect_uri_https"
    else
        echo "🔨 Creating new Azure AD app registration..."
        
        # Get current tenant ID
        CURRENT_TENANT_ID=$(az account show --query tenantId -o tsv)
        
        # Create new app registration
        APP_JSON=$(az ad app create \
            --display-name "$app_name" \
            --web-redirect-uris "$redirect_uri_http" "$redirect_uri_https" \
            --required-resource-accesses '[
                {
                    "resourceAppId": "00000003-0000-0000-c000-000000000000",
                    "resourceAccess": [
                        {
                            "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d",
                            "type": "Scope"
                        }
                    ]
                }
            ]' \
            --query '{appId: appId, id: id}' \
            --output json)
        
        NEW_CLIENT_ID=$(echo "$APP_JSON" | jq -r '.appId')
        
        echo "✅ Created app registration with Client ID: $NEW_CLIENT_ID"
        echo "✅ Tenant ID: $CURRENT_TENANT_ID"
        
        # Update appsettings.json with new values
        update_appsettings "$CURRENT_TENANT_ID" "$NEW_CLIENT_ID"
    fi
}

# Function to update appsettings.json with Azure AD configuration
update_appsettings() {
    local tenant_id="$1"
    local client_id="$2"
    local authority="https://login.microsoftonline.com/${tenant_id}"
    local valid_issuer="https://login.microsoftonline.com/${tenant_id}/v2.0"
    
    echo "📝 Updating appsettings.json with Azure AD configuration..."
    
    # Create temporary file with updated configuration
    jq --arg tenantId "$tenant_id" \
       --arg clientId "$client_id" \
       --arg authority "$authority" \
       --arg validIssuer "$valid_issuer" \
       --arg redirectHttp "http://${SERVER_HOST}:${SERVER_PORT}/oauth/callback" \
       --arg redirectHttps "https://${SERVER_HOST}:${SERVER_PORT}/oauth/callback" \
       '.Authentication.ExternalIdP.AzureAD.TenantId = $tenantId |
        .Authentication.ExternalIdP.AzureAD.ClientId = $clientId |
        .Authentication.ExternalIdP.AzureAD.Authority = $authority |
        .Authentication.ExternalIdP.AzureAD.RedirectUris = [$redirectHttp, $redirectHttps] |
        .Authentication.ExternalIdP.TokenValidation.ValidIssuer = $validIssuer |
        .Authentication.ExternalIdP.TokenValidation.ValidAudience = $clientId' \
       appsettings.json > appsettings.json.tmp
    
    # Validate JSON before replacing
    if jq . appsettings.json.tmp >/dev/null 2>&1; then
        mv appsettings.json.tmp appsettings.json
        echo "✅ Updated appsettings.json with Azure AD configuration"
    else
        rm -f appsettings.json.tmp
        echo "❌ Failed to update appsettings.json - invalid JSON generated"
        exit 1
    fi
}

# Function to verify Azure login
verify_azure_login() {
    echo "🔐 Verifying Azure CLI authentication..."
    
    if ! az account show >/dev/null 2>&1; then
        echo "❌ Not logged into Azure. Please run: az login"
        exit 1
    fi
    
    local account_info=$(az account show --query '{name: name, tenantId: tenantId, user: user.name}' --output json)
    local tenant_name=$(echo "$account_info" | jq -r '.name')
    local tenant_id=$(echo "$account_info" | jq -r '.tenantId')
    local user_name=$(echo "$account_info" | jq -r '.user')
    
    echo "✅ Authenticated as: $user_name"
    echo "✅ Tenant: $tenant_name ($tenant_id)"
}

# Function to test configuration
test_configuration() {
    echo "🧪 Testing Azure AD configuration..."
    
    local test_tenant_id=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.TenantId')
    local test_client_id=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.ClientId')
    
    if [ -z "$test_tenant_id" ] || [ "$test_tenant_id" = "null" ] || [ -z "$test_client_id" ] || [ "$test_client_id" = "null" ]; then
        echo "❌ Configuration validation failed - missing tenant or client ID"
        exit 1
    fi
    
    # Verify app exists
    if az ad app show --id "$test_client_id" >/dev/null 2>&1; then
        echo "✅ Azure AD app registration verified: $test_client_id"
    else
        echo "❌ Azure AD app registration not found: $test_client_id"
        exit 1
    fi
    
    echo "✅ Configuration test passed"
}

# Function to manage client secret
manage_client_secret() {
    local client_id="$1"
    
    echo ""
    echo "🔐 Client Secret Management"
    echo ""
    echo "Your Azure AD app supports both authentication modes:"
    echo "  1. Public client with PKCE (no secret needed) - CURRENTLY ACTIVE"
    echo "  2. Confidential client with secret (more secure for servers)"
    echo ""
    
    # Check if a secret is already configured in environment
    if [ -n "${Authentication__ExternalIdP__ClientSecret}" ]; then
        echo "✅ Client secret already configured via environment variable"
        return
    fi
    
    # Ask user if they want to create a client secret
    echo "Would you like to create a client secret for confidential client mode?"
    echo "Note: This is recommended for production server deployments."
    read -p "Create client secret? (y/n) [n]: " create_secret
    
    if [ "${create_secret}" = "y" ] || [ "${create_secret}" = "Y" ]; then
        echo ""
        echo "🔑 Creating new client secret..."
        
        # Create secret with 2-year expiry
        SECRET_JSON=$(az ad app credential reset --id "$client_id" --years 2 --query '{password: password, keyId: keyId}' --output json 2>/dev/null)
        
        if [ $? -eq 0 ]; then
            CLIENT_SECRET=$(echo "$SECRET_JSON" | jq -r '.password')
            KEY_ID=$(echo "$SECRET_JSON" | jq -r '.keyId')
            
            echo ""
            echo "✅ Client secret created successfully!"
            echo ""
            echo "⚠️  IMPORTANT: Save this secret securely - it won't be shown again!"
            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
            echo "$CLIENT_SECRET"
            echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
            echo ""
            echo "To use this secret, set it as an environment variable:"
            echo ""
            echo "  export Authentication__ExternalIdP__ClientSecret=\"$CLIENT_SECRET\""
            echo ""
            echo "Or use the setup-azure-secrets.sh script for secure storage."
            echo ""
            echo "Secret ID: $KEY_ID (expires in 2 years)"
        else
            echo "❌ Failed to create client secret"
            echo "You can create one manually later using:"
            echo "  az ad app credential reset --id $client_id --years 2"
        fi
    else
        echo ""
        echo "✅ Continuing with PKCE-only authentication (public client mode)"
        echo "   You can add a client secret later if needed."
    fi
}

# Function to show next steps
show_next_steps() {
    local client_id=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.ClientId')
    local auth_url="${OAUTH_ISSUER}/authorize"
    
    # Manage client secret
    manage_client_secret "$client_id"
    
    echo ""
    echo "🎉 Azure AD setup completed successfully!"
    echo ""
    echo "Next steps:"
    echo "1. Restart your MCP server: dotnet run"
    echo "2. Test authentication: ${auth_url}"
    echo "3. Configure Claude Code with: /mcp"
    echo ""
    echo "Configuration summary:"
    echo "  Client ID: $client_id"
    echo "  Authentication URL: $auth_url"
    echo "  Callback URLs: configured for port $SERVER_PORT"
}

# Function to destroy Azure AD app registration (idempotent)
implode_azure_app() {
    local client_id=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.ClientId')
    
    if [ -z "$client_id" ] || [ "$client_id" = "null" ] || [ "$client_id" = "" ]; then
        echo "ℹ️  No Azure AD app found in configuration - already clean"
        clear_azure_configuration
        echo "✅ Configuration cleared from appsettings.json"
        return 0
    fi
    
    # Check if app actually exists in Azure AD
    if ! az ad app show --id "$client_id" >/dev/null 2>&1; then
        echo "ℹ️  Azure AD app $client_id no longer exists in Azure - cleaning local configuration"
        clear_azure_configuration
        echo "✅ Configuration cleared from appsettings.json"
        return 0
    fi
    
    echo "💥 DESTRUCTIVE OPERATION: This will permanently delete the Azure AD app registration"
    echo "App to be deleted: $client_id"
    echo ""
    read -p "Are you absolutely sure? Type 'DELETE' to confirm: " confirmation
    
    if [ "$confirmation" != "DELETE" ]; then
        echo "❌ Operation cancelled - confirmation not received"
        return 1
    fi
    
    echo "🔥 Destroying Azure AD app registration..."
    
    # Delete the Azure AD app registration (idempotent)
    if az ad app delete --id "$client_id" 2>/dev/null; then
        echo "✅ Azure AD app registration deleted: $client_id"
    else
        # Check if it was already deleted
        if ! az ad app show --id "$client_id" >/dev/null 2>&1; then
            echo "✅ Azure AD app registration was already deleted: $client_id"
        else
            echo "❌ Failed to delete Azure AD app registration"
            echo "App exists but deletion failed - check permissions"
            return 1
        fi
    fi
    
    # Clear configuration in appsettings.json (always safe to run)
    clear_azure_configuration
    
    echo "✅ Configuration cleared from appsettings.json"
    echo ""
    echo "🎯 App registration completely destroyed"
    echo "To recreate: ./setup-azure-ad.sh setup"
}

# Function to clear Azure AD configuration from appsettings.json
clear_azure_configuration() {
    echo "🧹 Clearing Azure AD configuration from appsettings.json..."
    
    # Create temporary file with cleared configuration
    jq '.Authentication.ExternalIdP.AzureAD.TenantId = "" |
        .Authentication.ExternalIdP.AzureAD.ClientId = "" |
        .Authentication.ExternalIdP.AzureAD.Authority = "" |
        .Authentication.ExternalIdP.AzureAD.RedirectUris = [] |
        .Authentication.ExternalIdP.TokenValidation.ValidIssuer = "" |
        .Authentication.ExternalIdP.TokenValidation.ValidAudience = ""' \
       appsettings.json > appsettings.json.tmp
    
    # Validate JSON before replacing
    if jq . appsettings.json.tmp >/dev/null 2>&1; then
        mv appsettings.json.tmp appsettings.json
    else
        rm -f appsettings.json.tmp
        echo "❌ Failed to clear appsettings.json - invalid JSON generated"
        return 1
    fi
}

# Main execution
main() {
    load_and_show_config
    verify_azure_login
    setup_azure_app
    test_configuration
    show_next_steps
}

# Handle command line arguments
case "${1:-setup}" in
    "setup")
        main
        ;;
    "test")
        load_config
        test_configuration
        ;;
    "implode")
        load_config
        verify_azure_login
        implode_azure_app
        ;;
    "help"|"-h"|"--help")
        echo "Azure AD Enterprise Setup Script"
        echo ""
        echo "Usage: $0 [command]"
        echo ""
        echo "Commands:"
        echo "  setup   - Configure Azure AD app registration (default)"
        echo "  test    - Test current configuration"
        echo "  implode - Destroy Azure AD app registration (requires DELETE confirmation)"
        echo "  help    - Show this help"
        echo ""
        echo "Prerequisites:"
        echo "  - Azure CLI installed and authenticated (az login)"
        echo "  - jq installed for JSON processing"
        echo "  - Valid appsettings.json in current directory"
        echo ""
        echo "Notes:"
        echo "  - All operations are idempotent (safe to run multiple times)"
        echo "  - 'implode' requires typing 'DELETE' to confirm destructive operation"
        echo "  - Configuration is automatically updated in appsettings.json"
        ;;
    *)
        echo "❌ Unknown command: $1"
        echo "Run '$0 help' for usage information"
        exit 1
        ;;
esac