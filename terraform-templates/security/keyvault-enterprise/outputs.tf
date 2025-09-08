# Outputs for Enterprise Azure Key Vault Template

output "key_vault_id" {
  description = "The ID of the Key Vault"
  value       = azurerm_key_vault.main.id
}

output "key_vault_uri" {
  description = "The URI of the Key Vault"
  value       = azurerm_key_vault.main.vault_uri
}

output "key_vault_name" {
  description = "The name of the Key Vault"
  value       = azurerm_key_vault.main.name
}

output "resource_group_name" {
  description = "The name of the resource group"
  value       = azurerm_resource_group.main.name
}

output "key_vault_tenant_id" {
  description = "The tenant ID of the Key Vault"
  value       = azurerm_key_vault.main.tenant_id
}

output "private_endpoint_id" {
  description = "The ID of the private endpoint (if enabled)"
  value       = var.enable_private_endpoint ? azurerm_private_endpoint.keyvault[0].id : null
}

output "private_dns_zone_id" {
  description = "The ID of the private DNS zone (if enabled)"
  value       = var.enable_private_endpoint ? azurerm_private_dns_zone.keyvault[0].id : null
}

output "log_analytics_workspace_id" {
  description = "The ID of the Log Analytics workspace"
  value       = var.enable_diagnostic_settings ? (var.log_analytics_workspace_id != null ? var.log_analytics_workspace_id : azurerm_log_analytics_workspace.keyvault[0].id) : null
}

output "diagnostics_storage_account_name" {
  description = "The name of the storage account used for diagnostics"
  value       = var.enable_diagnostic_settings ? azurerm_storage_account.diagnostics[0].name : null
}
