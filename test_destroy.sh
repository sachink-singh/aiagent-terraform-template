#!/bin/bash

echo "Testing Terraform Destroy Functionality"
echo "======================================="

# Test if API is running
echo "1. Testing API health..."
curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/ || echo "API not responding"

echo ""
echo "2. Testing destroy endpoint..."

# Test the destroy functionality
curl -X POST http://localhost:5000/api/azure/destroy \
  -H "Content-Type: application/json" \
  -d '{"deploymentId": "rg-dev-aksworkload-westus2-002"}' \
  --max-time 30 || echo "Destroy request failed"

echo ""
echo "Test completed."
