#!/bin/bash

# MCP Server Auto-Start Script
# This script ensures the MCP server is running and starts it if needed

set -e

MCP_SERVER_PATH="$(dirname "$0")/publish/remote-mcp"
PID_FILE="/tmp/remote-mcp.pid"
LOG_FILE="/tmp/remote-mcp.log"
# Read port from appsettings.json using jq
PORT=$(cat appsettings.json | jq -r '.Server.Port // 3001')

# Function to check if server is running
is_server_running() {
    if [ -f "$PID_FILE" ]; then
        local pid=$(cat "$PID_FILE")
        if kill -0 "$pid" 2>/dev/null; then
            # Check if it's actually listening on the port
            if curl -s "http://localhost:$PORT/health" > /dev/null 2>&1; then
                return 0
            else
                # Process exists but not responding, clean up
                rm -f "$PID_FILE"
                return 1
            fi
        else
            # PID file exists but process doesn't, clean up
            rm -f "$PID_FILE"
            return 1
        fi
    else
        return 1
    fi
}

# Function to start the server
start_server() {
    echo "Starting MCP server..."
    
    # Start server in background and capture PID
    nohup "$MCP_SERVER_PATH" > "$LOG_FILE" 2>&1 &
    local server_pid=$!
    
    # Save PID to file
    echo "$server_pid" > "$PID_FILE"
    
    # Wait a moment for server to start
    sleep 2
    
    # Verify server is responding
    local retries=10
    while [ $retries -gt 0 ]; do
        if curl -s "http://localhost:$PORT/health" > /dev/null 2>&1; then
            echo "✅ MCP server started successfully on port $PORT (PID: $server_pid)"
            return 0
        fi
        echo "Waiting for server to start... ($retries retries left)"
        sleep 1
        retries=$((retries - 1))
    done
    
    echo "❌ Failed to start MCP server"
    rm -f "$PID_FILE"
    return 1
}

# Function to stop the server
stop_server() {
    if [ -f "$PID_FILE" ]; then
        local pid=$(cat "$PID_FILE")
        if kill "$pid" 2>/dev/null; then
            echo "✅ MCP server stopped (PID: $pid)"
        fi
        rm -f "$PID_FILE"
    else
        echo "No MCP server PID file found"
    fi
}

# Function to show server status
show_status() {
    if is_server_running; then
        local pid=$(cat "$PID_FILE")
        echo "✅ MCP server is running on port $PORT (PID: $pid)"
        curl -s "http://localhost:$PORT/info" | head -3
    else
        echo "❌ MCP server is not running"
    fi
}

# Main script logic
case "${1:-start}" in
    "start")
        if is_server_running; then
            echo "✅ MCP server is already running"
            show_status
        else
            start_server
        fi
        ;;
    "stop")
        stop_server
        ;;
    "restart")
        stop_server
        sleep 1
        start_server
        ;;
    "status")
        show_status
        ;;
    "logs")
        if [ -f "$LOG_FILE" ]; then
            tail -f "$LOG_FILE"
        else
            echo "No log file found at $LOG_FILE"
        fi
        ;;
    *)
        echo "Usage: $0 {start|stop|restart|status|logs}"
        echo "  start   - Start MCP server if not running (default)"
        echo "  stop    - Stop MCP server"
        echo "  restart - Restart MCP server"
        echo "  status  - Show server status"
        echo "  logs    - Show server logs (tail -f)"
        exit 1
        ;;
esac