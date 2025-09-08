# AKS Cluster Terraform Template
# This template creates a production-ready AKS cluster with networking and security

terraform {
  required_version = ">= 1.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.1"
    }
  }
}

provider "azurerm" {
  features {}
}

# Variables
variable "workload_name" {
  description = "Name of the workload"
  type        = string
}

variable "project_name" {
  description = "Name of the project"
  type        = string
}

variable "cluster_name" {
  description = "Optional cluster name override"
  type        = string
  default     = ""
}

variable "owner" {
  description = "Owner of the resources"
  type        = string
}

variable "environment" {
  description = "Environment (dev, test, staging, prod)"
  type        = string
  default     = "dev"
}

variable "location" {
  description = "Azure region"
  type        = string
  default     = "East US"
}

variable "node_count" {
  description = "Number of nodes in the default node pool"
  type        = number
  default     = 3
}

variable "vm_size" {
  description = "Size of the Virtual Machine"
  type        = string
  default     = "Standard_DS2_v2"
}

variable "enable_autoscaling" {
  description = "Enable autoscaling"
  type        = bool
  default     = true
}

variable "enable_rbac" {
  description = "Enable RBAC"
  type        = bool
  default     = true
}

variable "network_policy" {
  description = "Network policy to use (azure, calico, or none)"
  type        = string
  default     = "azure"
}

variable "kubernetes_version" {
  description = "Kubernetes version"
  type        = string
  default     = "1.31.10"
}

# Random suffix for unique naming
resource "random_id" "suffix" {
  byte_length = 4
}

# Intelligent naming logic with conditional truncation only when needed
locals {
  # Clean input names (remove special characters, keep alphanumeric)
  clean_workload = replace(lower(var.workload_name), "/[^a-z0-9]/", "")
  clean_project  = replace(lower(var.project_name), "/[^a-z0-9]/", "")
  clean_owner    = replace(lower(var.owner), "/[^a-z0-9]/", "")
  clean_env      = lower(var.environment)
  clean_location = lower(substr(var.location, 0, 12)) # Keep more location context

  # 8-character suffix for uniqueness
  suffix = random_id.suffix.hex

  # Calculate potential resource group name first
  potential_rg_name = "rg-${local.clean_workload}-${local.clean_env}-${local.clean_location}-${local.suffix}"

  # Calculate potential cluster name
  potential_cluster_name = var.cluster_name != "" ? var.cluster_name : "aks-${local.clean_workload}-${local.clean_env}-${local.suffix}"

  # Calculate what the node resource group would be
  potential_node_rg        = "MC_${local.potential_rg_name}_${local.potential_cluster_name}_${var.location}"
  potential_node_rg_length = length(local.potential_node_rg)

  # Only truncate if node RG would exceed 80 characters
  needs_truncation = local.potential_node_rg_length > 80

  # Conditional truncation - only when needed
  workload_part = local.needs_truncation ? substr(local.clean_workload, 0, 8) : local.clean_workload
  env_part      = local.needs_truncation ? substr(local.clean_env, 0, 3) : local.clean_env
  location_part = local.needs_truncation ? substr(local.clean_location, 0, 6) : local.clean_location

  # Final resource names - preserve meaning when possible
  rg_name      = local.needs_truncation ? "rg-${local.workload_part}-${local.location_part}-${local.suffix}" : local.potential_rg_name
  cluster_name = local.needs_truncation ? (var.cluster_name != "" ? substr(var.cluster_name, 0, 18) : "aks-${local.workload_part}-${local.suffix}") : local.potential_cluster_name

  # Other resource names - only shorten if main names were shortened
  vnet_name   = local.needs_truncation ? "vnet-${local.workload_part}-${local.suffix}" : "vnet-${local.clean_workload}-${local.clean_env}-${local.suffix}"
  subnet_name = local.needs_truncation ? "snet-aks-${local.env_part}-${local.suffix}" : "snet-aks-${local.clean_env}-${local.suffix}"
  log_name    = local.needs_truncation ? "log-${local.workload_part}-${local.suffix}" : "log-${local.clean_workload}-${local.clean_env}-${local.suffix}"

  # Final validation: Calculate actual node RG length
  expected_node_rg = "MC_${local.rg_name}_${local.cluster_name}_${var.location}"
  node_rg_length   = length(local.expected_node_rg)

  # Common tags
  common_tags = {
    Environment  = var.environment
    Project      = var.project_name
    Owner        = var.owner
    WorkloadName = var.workload_name
    CreatedBy    = "AzureAIAgent"
    NodeRGLength = local.node_rg_length # For debugging
  }
}

# Data sources
data "azurerm_client_config" "current" {}

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = local.rg_name
  location = var.location
  tags     = local.common_tags
}

# Virtual Network
resource "azurerm_virtual_network" "main" {
  name                = local.vnet_name
  address_space       = ["10.0.0.0/16"]
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  tags                = local.common_tags
}

# Subnet for AKS
resource "azurerm_subnet" "aks" {
  name                 = local.subnet_name
  resource_group_name  = azurerm_resource_group.main.name
  virtual_network_name = azurerm_virtual_network.main.name
  address_prefixes     = ["10.0.1.0/24"]
}

# Log Analytics Workspace
resource "azurerm_log_analytics_workspace" "main" {
  name                = local.log_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
  tags                = local.common_tags
}

# AKS Cluster
resource "azurerm_kubernetes_cluster" "main" {
  name                = local.cluster_name
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  dns_prefix          = local.cluster_name
  kubernetes_version  = var.kubernetes_version

  default_node_pool {
    name            = "default"
    node_count      = var.enable_autoscaling ? null : var.node_count
    min_count       = var.enable_autoscaling ? 1 : null
    max_count       = var.enable_autoscaling ? 10 : null
    vm_size         = var.vm_size
    os_disk_size_gb = 30
    vnet_subnet_id  = azurerm_subnet.aks.id

    upgrade_settings {
      max_surge = "10%"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  role_based_access_control_enabled = var.enable_rbac

  network_profile {
    network_plugin    = "azure"
    network_policy    = var.network_policy
    dns_service_ip    = "10.2.0.10"
    service_cidr      = "10.2.0.0/24"
    load_balancer_sku = "standard"
  }

  oms_agent {
    log_analytics_workspace_id = azurerm_log_analytics_workspace.main.id
  }

  tags = local.common_tags
}

# Role assignment for AKS to manage network
resource "azurerm_role_assignment" "aks_network_contributor" {
  scope                = azurerm_virtual_network.main.id
  role_definition_name = "Network Contributor"
  principal_id         = azurerm_kubernetes_cluster.main.identity[0].principal_id
}

# Outputs
output "resource_group_name" {
  description = "Name of the created resource group"
  value       = azurerm_resource_group.main.name
}

output "aks_cluster_name" {
  description = "Name of the AKS cluster"
  value       = azurerm_kubernetes_cluster.main.name
}

output "aks_cluster_id" {
  description = "ID of the AKS cluster"
  value       = azurerm_kubernetes_cluster.main.id
}

output "aks_fqdn" {
  description = "FQDN of the AKS cluster"
  value       = azurerm_kubernetes_cluster.main.fqdn
}

output "aks_node_resource_group" {
  description = "Name of the AKS node resource group"
  value       = azurerm_kubernetes_cluster.main.node_resource_group
}

output "kube_config" {
  description = "Raw Kubernetes config to be used by kubectl and other compatible tools"
  value       = azurerm_kubernetes_cluster.main.kube_config_raw
  sensitive   = true
}

output "client_certificate" {
  description = "Base64 encoded public certificate used by clients to authenticate to the Kubernetes cluster"
  value       = azurerm_kubernetes_cluster.main.kube_config.0.client_certificate
  sensitive   = true
}

output "client_key" {
  description = "Base64 encoded private key used by clients to authenticate to the Kubernetes cluster"
  value       = azurerm_kubernetes_cluster.main.kube_config.0.client_key
  sensitive   = true
}

output "cluster_ca_certificate" {
  description = "Base64 encoded public CA certificate used as the root of trust for the Kubernetes cluster"
  value       = azurerm_kubernetes_cluster.main.kube_config.0.cluster_ca_certificate
  sensitive   = true
}

output "host" {
  description = "The Kubernetes cluster server host"
  value       = azurerm_kubernetes_cluster.main.kube_config.0.host
  sensitive   = true
}

output "log_analytics_workspace_id" {
  description = "ID of the Log Analytics workspace"
  value       = azurerm_log_analytics_workspace.main.id
}

output "vnet_id" {
  description = "ID of the virtual network"
  value       = azurerm_virtual_network.main.id
}

output "subnet_id" {
  description = "ID of the AKS subnet"
  value       = azurerm_subnet.aks.id
}

# Validation outputs for debugging
output "expected_node_rg_name" {
  description = "Expected node resource group name"
  value       = local.expected_node_rg
}

output "node_rg_name_length" {
  description = "Length of the expected node resource group name"
  value       = local.node_rg_length
}
