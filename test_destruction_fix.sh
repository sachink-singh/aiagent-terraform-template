#!/bin/bash

# Test script to verify the fixed DestroyTerraformResources function
echo "🧪 Testing Fixed Terraform Destruction Functionality"
echo "=================================================="

# Wait for API to be ready
echo "⏳ Waiting for API to start..."
sleep 5

# Test the destruction function directly with the deployment ID
echo "🎯 Testing destruction of deployment: rg-dev-aksworkload-westus2-002"

curl -X POST http://localhost:5000/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Please destroy the Terraform deployment with ID: rg-dev-aksworkload-westus2-002",
    "sessionId": "test-session-' $(date +%s) '"
  }' \
  | jq '.'

echo ""
echo "✅ Test completed. Check the response above for results."
