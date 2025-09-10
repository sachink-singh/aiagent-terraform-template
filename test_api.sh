#!/bin/bash

# Test Azure AI Agent API with real Azure operations
BASE_URL="http://localhost:5050"
SESSION_ID="test-session-$(date +%Y%m%d-%H%M%S)"

echo "🚀 Testing Azure AI Agent API"
echo "Session ID: $SESSION_ID"
echo ""

# Test 1: Check agent status
echo "1️⃣ Checking agent status..."
curl -s -X GET "$BASE_URL/api/agent/status" | jq '.' || echo "❌ Failed to get status"
echo ""

# Test 2: Simple chat test
echo "2️⃣ Testing chat with simple Azure command..."
curl -s -X POST "$BASE_URL/api/agent/chat" \
  -H "Content-Type: application/json" \
  -d "{\"message\": \"List all my resource groups\", \"sessionId\": \"$SESSION_ID\"}" | jq '.' || echo "❌ Failed to chat"
echo ""

echo "🎉 Basic test completed!"
echo "💡 Open Swagger UI at http://localhost:5050/swagger for interactive testing"
