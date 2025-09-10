#!/bin/bash

# Test the MCP server standalone
echo "🧪 Testing Kubernetes MCP Server Standalone"
echo

# Start the MCP server in background
cd KubernetesMcpServer
echo "Starting MCP server..."
dotnet run &
MCP_PID=$!

# Wait a moment for server to start
sleep 3

# Test initialization
echo "Testing initialization..."
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{"tools":{}}}}' | nc localhost -

# Test tools list
echo "Testing tools list..."
echo '{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}' | nc localhost -

# Test cluster connection
echo "Testing cluster connection..."
echo '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"connect_aks_cluster","arguments":{"clusterName":"aks-dev-aksworkload-si-002","resourceGroup":"rg-dev-aksworkload-si-002"}}}' | nc localhost -

# Cleanup
echo "Stopping MCP server..."
kill $MCP_PID

echo "✅ Standalone testing complete"
