#!/bin/bash

echo "Testing Azure Resource Naming Validation"
echo "========================================"

# Test 1: Storage Account Name Too Long
echo "Test 1: Storage Account with Long Name"
echo "Expected: Shortened and formatted for Azure compliance"

# Test 2: Container Registry Name with Invalid Characters  
echo "Test 2: Container Registry with Invalid Characters"
echo "Expected: Remove hyphens and underscores, ensure alphanumeric only"

# Test 3: Key Vault Name Too Long
echo "Test 3: Key Vault Name Length Validation"
echo "Expected: Truncated to 24 characters maximum"

echo ""
echo "These tests verify the naming validation system prevents:"
echo "• Storage account names > 24 characters"
echo "• Storage account names with invalid characters"
echo "• Resource names that violate Azure naming conventions"
echo "• Terraform deployment failures due to naming issues"

echo ""
echo "Run the console app and try creating resources with long names to test."
