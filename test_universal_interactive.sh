#!/bin/bash

# Test the Universal Interactive System
API_URL="http://localhost:5050"

echo "🧪 Testing Universal Interactive System"
echo "======================================"

# Test 1: List Azure Resource Groups (should be clickable)
echo "📋 Test 1: List Azure Resource Groups"
curl -X POST "${API_URL}/api/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "list all my azure resource groups",
    "sessionId": "test-universal-001"
  }' | jq '.'

echo -e "\n\n"

# Test 2: List Azure Virtual Machines (should be clickable)
echo "🖥️ Test 2: List Azure Virtual Machines"
curl -X POST "${API_URL}/api/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "show me all virtual machines in azure",
    "sessionId": "test-universal-002"
  }' | jq '.'

echo -e "\n\n"

# Test 3: List Storage Accounts (should be clickable)
echo "💾 Test 3: List Azure Storage Accounts"
curl -X POST "${API_URL}/api/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "list azure storage accounts",
    "sessionId": "test-universal-003"
  }' | jq '.'

echo -e "\n\n"

# Test 4: AKS Cluster listing (should still work)
echo "☸️ Test 4: List AKS Clusters"
curl -X POST "${API_URL}/api/chat" \
  -H "Content-Type: application/json" \
  -d '{
    "message": "show me all aks clusters",
    "sessionId": "test-universal-004"
  }' | jq '.'

echo -e "\n\n✅ Universal Interactive System Tests Complete!"
