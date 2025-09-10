#!/bin/bash

# Test script to verify the duplicate parameter request issue is fixed

BASE_URL="http://localhost:5050"
SESSION_ID="test-session-$(date +%s)"

echo "🧪 Testing AKS Cluster Creation Flow"
echo "Session ID: $SESSION_ID"
echo ""

echo "1️⃣ Testing: Create an AKS cluster"
curl -X POST "$BASE_URL/api/agent/chat" \
  -H "Content-Type: application/json" \
  -d "{
    \"sessionId\": \"$SESSION_ID\",
    \"message\": \"Create an AKS cluster\"
  }" | jq -r '.message' | head -20

echo ""
echo "✅ Test completed. Check if:"
echo "   - Only ONE request for parameters (no duplicate)"
echo "   - Adaptive card is shown properly"
echo "   - No plain text parameter request before the card"
