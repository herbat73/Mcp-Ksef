#!/bin/bash

# Azure Secrets Management Script for Remote MCP Server
# Handles client secrets through Azure Key Vault or environment variables
# Idempotent operations - safe to run multiple times

set -e

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Check dependencies
command -v az >/dev/null 2>&1 || { echo "❌ Azure CLI (az) is required but not installed."; exit 1; }
command -v jq >/dev/null 2>&1 || { echo "❌ jq is required but not installed."; exit 1; }

# Function to load configuration
load_config() {
    CLIENT_ID=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.ClientId // empty')
    TENANT_ID=$(cat appsettings.json | jq -r '.Authentication.ExternalIdP.AzureAD.TenantId // empty')
    
    if [ -z "$CLIENT_ID" ] || [ "$CLIENT_ID" = "null" ]; then
        echo "❌ No Azure AD Client ID found. Run './setup-azure-ad.sh setup' first."
        exit 1
    fi
}

# Function to verify Azure login
verify_azure_login() {
    if ! az account show >/dev/null 2>&1; then
        echo "❌ Not logged into Azure. Please run: az login"
        exit 1
    fi
    
    local user_name=$(az account show --query 'user.name' -o tsv)
    echo "✅ Authenticated as: $user_name"
}

# Function to manage secrets via environment variables (FREE alternative)
manage_env_var_secret() {
    echo "📝 Setting up environment variable configuration..."
    echo ""
    echo "⚠️  You need to generate a client secret manually:"
    echo ""
    echo "1. Generate new secret:"
    echo "   az ad app credential reset --id $CLIENT_ID --display-name 'MCP-Secret-$(date +%Y%m%d)'"
    echo ""
    echo "2. Copy the 'password' value from the output"
    echo ""
    echo "3. Set environment variable:"
    echo "   export Authentication__ExternalIdP__ClientSecret='<paste-secret-here>'"
    echo ""
    echo "4. Or add to your shell profile (~/.zshrc or ~/.bashrc):"
    echo "   echo 'export Authentication__ExternalIdP__ClientSecret=\"<secret>\"' >> ~/.zshrc"
    echo ""
    
    # Create .env.example file
    cat > .env.example <<'EOF'
# Azure AD Client Secret Configuration
# Copy this to .env and fill in your secret

# Option 1: Direct secret (development only)
Authentication__ExternalIdP__ClientSecret=your-secret-here

# Option 2: Azure Key Vault reference (production)
# AZURE_KEY_VAULT_NAME=your-vault-name
# AZURE_CLIENT_SECRET_NAME=mcp-client-secret
EOF
    
    echo "✅ Created .env.example with configuration template"
    echo ""
    echo "📋 Remember to:"
    echo "   - Never commit the actual secret to git"
    echo "   - Add .env to .gitignore"
    echo "   - Rotate secrets regularly (every 90 days)"
}

# Function to check for existing Key Vault
find_or_create_key_vault() {
    echo "🔍 Checking for Azure Key Vault..."
    
    # Look for existing Key Vaults in the subscription
    local vaults=$(az keyvault list --query '[].name' -o tsv 2>/dev/null)
    
    if [ -n "$vaults" ]; then
        echo "📦 Found existing Key Vaults:"
        echo "$vaults" | nl
        echo ""
        
        read -p "Enter Key Vault name to use (or 'skip' to use environment variables): " VAULT_NAME
        
        if [ "$VAULT_NAME" = "skip" ] || [ "$VAULT_NAME" = "SKIP" ]; then
            echo "⚠️  Skipping Key Vault setup - use environment variables instead"
            USE_ENV_VARS=true
            return 0
        fi
        
        if [ -n "$VAULT_NAME" ]; then
            # Verify the vault exists and is accessible
            if az keyvault show --name "$VAULT_NAME" >/dev/null 2>&1; then
                echo "✅ Using existing Key Vault: $VAULT_NAME"
                return 0
            else
                echo "❌ Key Vault '$VAULT_NAME' not accessible"
                exit 1
            fi
        fi
    fi
    
    # Warn about costs before creating new Key Vault
    echo ""
    echo "⚠️  WARNING: Azure Key Vault incurs costs!"
    echo "   - Storage: ~$0.03/10,000 operations"
    echo "   - Secrets: ~$0.03/10,000 operations"
    echo "   - Monthly minimum: ~$0.50-$5.00 depending on usage"
    echo ""
    echo "Alternative: Use environment variables (FREE)"
    echo "   export Authentication__ExternalIdP__ClientSecret='your-secret'"
    echo ""
    read -p "Do you want to create a new Key Vault? (yes/NO): " create_vault
    
    if [ "$create_vault" != "yes" ]; then
        echo "✅ Skipping Key Vault creation - use environment variables instead"
        USE_ENV_VARS=true
        return 0
    fi
    
    # Create new Key Vault with explicit confirmation
    echo "📝 Creating new Key Vault..."
    read -p "Enter name for new Key Vault (must be globally unique): " VAULT_NAME
    
    if [ -z "$VAULT_NAME" ]; then
        echo "❌ Key Vault name is required"
        exit 1
    fi
    
    # Get resource group or create default
    local resource_group="mcp-resources"
    read -p "Enter resource group name [$resource_group]: " input_rg
    resource_group="${input_rg:-$resource_group}"
    
    # Final confirmation before creating resources
    echo ""
    echo "📋 Summary of resources to create:"
    echo "   Resource Group: $resource_group"
    echo "   Key Vault: $VAULT_NAME"
    echo "   Location: eastus"
    echo "   SKU: Standard"
    echo ""
    echo "⚠️  This WILL incur Azure charges!"
    echo ""
    read -p "Type 'CREATE' to confirm: " final_confirm
    
    if [ "$final_confirm" != "CREATE" ]; then
        echo "❌ Creation cancelled"
        exit 1
    fi
    
    # Create resource group if it doesn't exist (idempotent)
    if ! az group show --name "$resource_group" >/dev/null 2>&1; then
        echo "📝 Creating resource group: $resource_group"
        az group create --name "$resource_group" --location "eastus" >/dev/null
    fi
    
    # Create Key Vault (idempotent - check if exists first)
    if az keyvault show --name "$VAULT_NAME" >/dev/null 2>&1; then
        echo "✅ Key Vault already exists: $VAULT_NAME"
    else
        echo "📝 Creating Key Vault: $VAULT_NAME"
        az keyvault create \
            --name "$VAULT_NAME" \
            --resource-group "$resource_group" \
            --location "eastus" \
            --sku standard >/dev/null
        echo "✅ Key Vault created: $VAULT_NAME"
    fi
}

# Function to manage client secret
manage_client_secret() {
    echo "🔐 Managing client secret for Azure AD app..."
    
    # Check if using environment variables instead of Key Vault
    if [ "$USE_ENV_VARS" = "true" ]; then
        manage_env_var_secret
        return 0
    fi
    
    local secret_name="mcp-client-secret"
    
    # Check if secret already exists in Key Vault
    if az keyvault secret show --vault-name "$VAULT_NAME" --name "$secret_name" >/dev/null 2>&1; then
        echo "✅ Client secret already exists in Key Vault"
        
        read -p "Do you want to rotate the secret? (y/N): " rotate
        if [ "$rotate" != "y" ] && [ "$rotate" != "Y" ]; then
            return 0
        fi
    fi
    
    # Generate or retrieve client secret
    echo "🔑 Generating new client secret..."
    
    # Reset the client credentials (generates new secret)
    local secret_json=$(az ad app credential reset \
        --id "$CLIENT_ID" \
        --display-name "MCP-Secret-$(date +%Y%m%d)" \
        --years 2 \
        --query '{secret: password, keyId: keyId}' \
        -o json)
    
    local client_secret=$(echo "$secret_json" | jq -r '.secret')
    local key_id=$(echo "$secret_json" | jq -r '.keyId')
    
    if [ -z "$client_secret" ] || [ "$client_secret" = "null" ]; then
        echo "❌ Failed to generate client secret"
        exit 1
    fi
    
    echo "✅ Generated new client secret (Key ID: ${key_id:0:8}...)"
    
    # Store in Key Vault (idempotent - set will update if exists)
    echo "📝 Storing secret in Key Vault..."
    az keyvault secret set \
        --vault-name "$VAULT_NAME" \
        --name "$secret_name" \
        --value "$client_secret" \
        --description "Client secret for MCP Azure AD app" \
        --tags "app=$CLIENT_ID" "generated=$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
        >/dev/null
    
    echo "✅ Secret stored in Key Vault: $VAULT_NAME/$secret_name"
    
    # Store Key Vault reference
    store_key_vault_reference
}

# Function to store Key Vault reference in configuration
store_key_vault_reference() {
    echo "📝 Updating configuration with Key Vault reference..."
    
    # Create or update .env file with Key Vault reference
    cat > .env.keyvault <<EOF
# Azure Key Vault Configuration
# Generated by setup-azure-secrets.sh on $(date)

# Key Vault containing the client secret
AZURE_KEY_VAULT_NAME=$VAULT_NAME

# Secret name in Key Vault
AZURE_CLIENT_SECRET_NAME=mcp-client-secret

# Alternative: Use environment variable directly
# Authentication__ExternalIdP__ClientSecret=<your-secret-here>
EOF
    
    echo "✅ Key Vault configuration saved to .env.keyvault"
}

# Function to configure application for Key Vault
configure_app_for_key_vault() {
    echo "🔧 Configuring application for Key Vault access..."
    
    # Check if Azure.Extensions.AspNetCore.Configuration.Secrets is installed
    if ! grep -q "Azure.Extensions.AspNetCore.Configuration.Secrets" *.csproj 2>/dev/null; then
        echo "📦 Installing Azure Key Vault configuration package..."
        dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
        dotnet add package Azure.Identity
    fi
    
    echo "✅ Application configured for Key Vault access"
    
    # Show configuration snippet
    cat <<'EOF'

📝 Add this to your Program.cs to use Key Vault:

var keyVaultName = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_NAME");
if (!string.IsNullOrEmpty(keyVaultName))
{
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
    builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());
}

Or use environment variable directly:
export Authentication__ExternalIdP__ClientSecret=$(az keyvault secret show \
    --vault-name $VAULT_NAME \
    --name mcp-client-secret \
    --query value -o tsv)
EOF
}

# Function to test secret retrieval
test_secret_access() {
    echo "🧪 Testing secret access..."
    
    # Test retrieving secret from Key Vault
    local test_secret=$(az keyvault secret show \
        --vault-name "$VAULT_NAME" \
        --name "mcp-client-secret" \
        --query value -o tsv 2>/dev/null)
    
    if [ -n "$test_secret" ]; then
        echo "✅ Successfully retrieved secret from Key Vault"
        echo "   Secret starts with: ${test_secret:0:5}..."
    else
        echo "❌ Failed to retrieve secret from Key Vault"
        exit 1
    fi
}

# Function to show usage instructions
show_usage() {
    echo ""
    echo "🎉 Azure secrets configuration complete!"
    echo ""
    echo "To use the client secret, choose one of these methods:"
    echo ""
    echo "1. Environment Variable (Recommended for development):"
    echo "   export Authentication__ExternalIdP__ClientSecret=\$(az keyvault secret show \\"
    echo "       --vault-name $VAULT_NAME \\"
    echo "       --name mcp-client-secret \\"
    echo "       --query value -o tsv)"
    echo ""
    echo "2. Key Vault Integration (Recommended for production):"
    echo "   - Set AZURE_KEY_VAULT_NAME=$VAULT_NAME"
    echo "   - Use DefaultAzureCredential in your app"
    echo ""
    echo "3. Managed Identity (Best for Azure deployment):"
    echo "   - Enable managed identity on your Azure resource"
    echo "   - Grant Key Vault access to the managed identity"
    echo ""
    echo "Key Vault: $VAULT_NAME"
    echo "Secret Name: mcp-client-secret"
}

# Function to remove secrets (idempotent)
remove_secrets() {
    echo "⚠️  Removing client secret from Key Vault..."
    
    if [ -z "$VAULT_NAME" ]; then
        read -p "Enter Key Vault name: " VAULT_NAME
    fi
    
    # Delete secret from Key Vault (idempotent - won't fail if doesn't exist)
    if az keyvault secret show --vault-name "$VAULT_NAME" --name "mcp-client-secret" >/dev/null 2>&1; then
        echo "🗑️  Deleting secret from Key Vault..."
        az keyvault secret delete --vault-name "$VAULT_NAME" --name "mcp-client-secret" >/dev/null
        echo "✅ Secret deleted from Key Vault"
    else
        echo "ℹ️  Secret not found in Key Vault - already removed"
    fi
    
    # Remove local configuration
    if [ -f .env.keyvault ]; then
        rm .env.keyvault
        echo "✅ Removed .env.keyvault"
    fi
}

# Main execution
main() {
    echo "🔐 Azure Secrets Management for Remote MCP Server"
    echo ""
    
    verify_azure_login
    load_config
    find_or_create_key_vault
    manage_client_secret
    
    # Only run Key Vault specific steps if not using env vars
    if [ "$USE_ENV_VARS" != "true" ]; then
        configure_app_for_key_vault
        test_secret_access
        show_usage
    fi
}

# Handle command line arguments
case "${1:-setup}" in
    "setup")
        main
        ;;
    "test")
        load_config
        read -p "Enter Key Vault name: " VAULT_NAME
        test_secret_access
        ;;
    "remove")
        load_config
        remove_secrets
        ;;
    "help"|"-h"|"--help")
        echo "Azure Secrets Management Script"
        echo ""
        echo "Usage: $0 [command]"
        echo ""
        echo "Commands:"
        echo "  setup  - Configure client secret in Azure Key Vault (default)"
        echo "  test   - Test secret retrieval from Key Vault"
        echo "  remove - Remove secrets from Key Vault"
        echo "  help   - Show this help"
        echo ""
        echo "Prerequisites:"
        echo "  - Azure CLI installed and authenticated"
        echo "  - Azure AD app already configured (run setup-azure-ad.sh first)"
        echo "  - jq installed for JSON processing"
        echo ""
        echo "Features:"
        echo "  - Idempotent operations (safe to run multiple times)"
        echo "  - Automatic Key Vault creation if needed"
        echo "  - Client secret rotation support"
        echo "  - Multiple authentication methods supported"
        ;;
    *)
        echo "❌ Unknown command: $1"
        echo "Run '$0 help' for usage information"
        exit 1
        ;;
esac