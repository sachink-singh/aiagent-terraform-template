# Variables for Enterprise Azure Key Vault Template

variable "keyvault_name" {
  description = "Name of the Key Vault (must be globally unique)"
  type        = string
  validation {
    condition     = length(var.keyvault_name) >= 3 && length(var.keyvault_name) <= 24
    error_message = "Key Vault name must be between 3 and 24 characters."
  }
}

variable "resource_group_name" {
  description = "Name of the resource group"
  type        = string
}

variable "location" {
  description = "Azure region where resources will be created"
  type        = string
  default     = "East US"
}

variable "sku_name" {
  description = "The Name of the SKU used for this Key Vault"
  type        = string
  default     = "premium"
  validation {
    condition     = contains(["standard", "premium"], var.sku_name)
    error_message = "SKU name must be either 'standard' or 'premium'."
  }
}

variable "enable_private_endpoint" {
  description = "Enable private endpoint for Key Vault"
  type        = bool
  default     = true
}

variable "subnet_id" {
  description = "Subnet ID for private endpoint (required if enable_private_endpoint is true)"
  type        = string
  default     = null
}

variable "enable_diagnostic_settings" {
  description = "Enable diagnostic settings and monitoring"
  type        = bool
  default     = true
}

variable "log_analytics_workspace_id" {
  description = "Log Analytics workspace ID (if null, a new workspace will be created)"
  type        = string
  default     = null
}

# Enterprise tagging variables
variable "environment" {
  description = "Environment name (dev, test, staging, prod)"
  type        = string
  validation {
    condition     = contains(["dev", "test", "staging", "prod"], var.environment)
    error_message = "Environment must be one of: dev, test, staging, prod."
  }
}

variable "business_unit" {
  description = "Business unit for cost allocation and governance"
  type        = string
}

variable "compliance_framework" {
  description = "Compliance framework (SOC2, HIPAA, PCI, etc.)"
  type        = string
  default     = ""
}
