#!/bin/bash

# Quick MCP Server Connection Assurance Script
# This script ensures the MCP server is available for Claude Code connection

# Read port from appsettings.json using jq
PORT=$(cat appsettings.json | jq -r '.Server.Port // 3001')
MAX_RETRIES=3

# Function to check if server is responding
check_server() {
    curl -s --connect-timeout 2 "http://localhost:$PORT/health" > /dev/null 2>&1
}

# Function to start server if needed
ensure_server() {
    if check_server; then
        echo "✅ MCP server already running on port $PORT"
        return 0
    fi
    
    echo "🚀 Starting MCP server..."
    
    # Try to load the launchd service
    launchctl load ~/Library/LaunchAgents/com.remotemcp.server.plist 2>/dev/null || true
    
    # If that fails, use the direct script
    if ! check_server; then
        ./start-mcp-server.sh start > /dev/null 2>&1 &
    fi
    
    # Wait for server to be ready
    local retries=$MAX_RETRIES
    while [ $retries -gt 0 ]; do
        if check_server; then
            echo "✅ MCP server is ready on port $PORT"
            return 0
        fi
        echo "⏳ Waiting for server... ($retries retries left)"
        sleep 1
        retries=$((retries - 1))
    done
    
    echo "❌ Failed to start MCP server within $MAX_RETRIES seconds"
    return 1
}

# Main execution
ensure_server