# Terraform State Directory Fixes Summary

## Issue Identified
The Azure AI Agent was incorrectly using temporary directories (`Path.GetTempPath()`) for Terraform state files, which caused issues when trying to destroy or manage deployments. This resulted in the error "deployment directory cannot be found" even when the deployment was successful.

## Root Cause
- Deployment functions were creating state files in temporary directories that could be cleaned up by the system
- Destruction and management functions were looking for these state files but couldn't find them
- No consistent directory strategy across all Terraform-related functions

## Solution Implemented
Changed all Terraform-related functions to use a **persistent directory structure**:
```
~/.azure-ai-agent/terraform/{deployment-id}/
```

## Functions Fixed

### 1. `ApplyTerraformTemplateAsync`
- **Before**: Used `Path.GetTempPath()` for deployment directory
- **After**: Uses `~/.azure-ai-agent/terraform/` as base directory
- **Impact**: All new deployments will use persistent storage

### 2. `DestroyTerraformResources`
- **Before**: Only looked in `Path.GetTempPath()`
- **After**: Smart lookup with fallback:
  1. First checks persistent directory
  2. Falls back to legacy temp directory
  3. Uses `FindDeploymentDirectory` helper for comprehensive search
- **Impact**: Can now find and destroy existing deployments

### 3. `CheckAsyncDeploymentStatus`
- **Before**: Only looked in `Path.GetTempPath()`
- **After**: Smart lookup with fallback (same as above)
- **Impact**: Can track status of all deployments regardless of where they were created

### 4. `ShowTerraformState`
- **Before**: Only looked in `Path.GetTempPath()`
- **After**: Smart lookup with fallback (same as above)
- **Impact**: Can show state for all deployments

### 5. `RecoverFromPartialFailure`
- **Before**: Only looked in `Path.GetTempPath()`
- **After**: Smart lookup with fallback (same as above)
- **Impact**: Can recover from failures in any deployment location

## New Helper Function Added

### `FindDeploymentDirectory(string deploymentId)`
- Searches multiple possible locations for deployment directories
- Returns the first valid directory found containing Terraform state files
- Provides comprehensive fallback when standard paths fail

## Enhanced Error Handling
- Better error messages that show which directories were searched
- Helpful guidance for users when deployments cannot be found
- Fallback suggestions like using `CheckForOrphanedResources`

## Backward Compatibility
All functions maintain backward compatibility by:
1. First checking the new persistent directory
2. Falling back to legacy temp directory locations
3. Using comprehensive search as final fallback

## Migration Path
- Existing deployments in temp directories will continue to work
- New deployments automatically use persistent directories
- Users can manually move existing state files if needed

## Testing Recommendation
Test the fixed `DestroyTerraformResources` function with the user's deployment:
```
DestroyTerraformResources("rg-dev-aksworkload-westus2-002")
```

This should now successfully locate and destroy the deployment that was previously inaccessible.
