# Enterprise Azure Key Vault Terraform Template
# Key Vault with private endpoints, advanced security, and compliance features

terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~>3.0"
    }
  }
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = true
      recover_soft_deleted_key_vaults = true
    }
  }
}

# Data source for current client
data "azurerm_client_config" "current" {}

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = var.resource_group_name
  location = var.location

  tags = {
    Environment         = var.environment
    BusinessUnit        = var.business_unit
    ComplianceFramework = var.compliance_framework
    ManagedBy          = "Terraform"
    CreatedBy          = "AzureAIAgent"
    SecurityLevel      = "Enterprise"
  }
}

# Key Vault with Premium SKU and enhanced security
resource "azurerm_key_vault" "main" {
  name                          = var.keyvault_name
  location                      = azurerm_resource_group.main.location
  resource_group_name           = azurerm_resource_group.main.name
  enabled_for_disk_encryption   = true
  enabled_for_deployment        = true
  enabled_for_template_deployment = true
  tenant_id                     = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days    = 90  # Maximum retention for enterprise
  purge_protection_enabled      = true # Always enabled for enterprise
  enable_rbac_authorization     = true # RBAC required for enterprise

  sku_name = var.sku_name

  # Restrictive network access for enterprise
  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"
    
    # Add specific IP ranges if needed
    # ip_rules = var.allowed_ip_ranges
  }

  tags = {
    Environment         = var.environment
    BusinessUnit        = var.business_unit
    ComplianceFramework = var.compliance_framework
    ManagedBy          = "Terraform"
    CreatedBy          = "AzureAIAgent"
    SecurityLevel      = "Enterprise"
  }

  lifecycle {
    prevent_destroy = true  # Prevent accidental deletion
    ignore_changes = [
      access_policy,
    ]
  }
}

# Private DNS Zone for Key Vault
resource "azurerm_private_dns_zone" "keyvault" {
  count               = var.enable_private_endpoint ? 1 : 0
  name                = "privatelink.vaultcore.azure.net"
  resource_group_name = azurerm_resource_group.main.name

  tags = {
    Environment  = var.environment
    BusinessUnit = var.business_unit
    ManagedBy    = "Terraform"
    CreatedBy    = "AzureAIAgent"
  }
}

# Private Endpoint for Key Vault
resource "azurerm_private_endpoint" "keyvault" {
  count               = var.enable_private_endpoint ? 1 : 0
  name                = "${var.keyvault_name}-pe"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  subnet_id           = var.subnet_id

  private_service_connection {
    name                           = "${var.keyvault_name}-psc"
    private_connection_resource_id = azurerm_key_vault.main.id
    subresource_names              = ["vault"]
    is_manual_connection           = false
  }

  private_dns_zone_group {
    name                 = "keyvault-dns-zone-group"
    private_dns_zone_ids = [azurerm_private_dns_zone.keyvault[0].id]
  }

  tags = {
    Environment  = var.environment
    BusinessUnit = var.business_unit
    ManagedBy    = "Terraform"
    CreatedBy    = "AzureAIAgent"
  }
}

# Log Analytics Workspace for enhanced monitoring
resource "azurerm_log_analytics_workspace" "keyvault" {
  count               = var.enable_diagnostic_settings && var.log_analytics_workspace_id == null ? 1 : 0
  name                = "${var.keyvault_name}-law"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 90

  tags = {
    Environment  = var.environment
    BusinessUnit = var.business_unit
    Purpose      = "KeyVault-Monitoring"
    ManagedBy    = "Terraform"
    CreatedBy    = "AzureAIAgent"
  }
}

# Enhanced Diagnostic Settings
resource "azurerm_monitor_diagnostic_setting" "keyvault" {
  count                      = var.enable_diagnostic_settings ? 1 : 0
  name                       = "${var.keyvault_name}-diagnostics"
  target_resource_id         = azurerm_key_vault.main.id
  log_analytics_workspace_id = var.log_analytics_workspace_id != null ? var.log_analytics_workspace_id : azurerm_log_analytics_workspace.keyvault[0].id

  enabled_log {
    category = "AuditEvent"
  }

  enabled_log {
    category = "AzurePolicyEvaluationDetails"
  }

  metric {
    category = "AllMetrics"
    enabled  = true
  }
}

# Security Alert Rules
resource "azurerm_monitor_metric_alert" "keyvault_availability" {
  count               = var.enable_diagnostic_settings ? 1 : 0
  name                = "${var.keyvault_name}-availability-alert"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_key_vault.main.id]
  description         = "Alert when Key Vault availability drops"
  severity            = 1

  criteria {
    metric_namespace = "Microsoft.KeyVault/vaults"
    metric_name      = "Availability"
    aggregation      = "Average"
    operator         = "LessThan"
    threshold        = 99
  }

  tags = {
    Environment  = var.environment
    BusinessUnit = var.business_unit
    AlertType    = "Availability"
    ManagedBy    = "Terraform"
    CreatedBy    = "AzureAIAgent"
  }
}

# Storage Account for long-term log retention
resource "azurerm_storage_account" "diagnostics" {
  count                    = var.enable_diagnostic_settings ? 1 : 0
  name                     = "${replace(var.keyvault_name, "-", "")}diag"
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = "GRS"  # Geo-redundant for enterprise
  
  # Enhanced security for enterprise
  account_kind                     = "StorageV2"
  enable_https_traffic_only        = true
  min_tls_version                  = "TLS1_2"
  allow_nested_items_to_be_public  = false

  network_rules {
    default_action = "Deny"
    bypass         = ["AzureServices"]
  }

  tags = {
    Environment  = var.environment
    BusinessUnit = var.business_unit
    Purpose      = "KeyVault-Diagnostics"
    ManagedBy    = "Terraform"
    CreatedBy    = "AzureAIAgent"
  }
}
