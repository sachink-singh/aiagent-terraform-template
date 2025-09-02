# AKS Cluster Terraform Template

This Terraform template creates a production-ready Azure Kubernetes Service (AKS) cluster with the following components:

## Resources Created

- **Resource Group**: Contains all the resources
- **Virtual Network**: Private network for the cluster
- **Subnet**: Dedicated subnet for AKS nodes
- **AKS Cluster**: Managed Kubernetes cluster
- **Log Analytics Workspace**: For monitoring and logging
- **Role Assignments**: Proper permissions for AKS

## Features

- ✅ **Production-ready**: Includes monitoring, networking, and security best practices
- ✅ **Auto-scaling**: Optional node auto-scaling capability
- ✅ **RBAC**: Role-based access control support
- ✅ **Network Policy**: Azure CNI with network policies
- ✅ **Monitoring**: Integrated with Azure Monitor and Log Analytics
- ✅ **Secure**: System-assigned managed identity

## Usage

### Prerequisites

1. Azure CLI installed and logged in (`az login`)
2. Terraform installed (>= 1.0)
3. Proper Azure permissions to create resources

### Quick Start

1. **Initialize Terraform:**
   ```bash
   terraform init
   ```

2. **Create variables file:**
   ```bash
   cp terraform.tfvars.example terraform.tfvars
   # Edit terraform.tfvars with your values
   ```

3. **Plan the deployment:**
   ```bash
   terraform plan
   ```

4. **Apply the configuration:**
   ```bash
   terraform apply
   ```

### Variables

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `workload_name` | Name of the workload | - | Yes |
| `project_name` | Name of the project | - | Yes |
| `owner` | Owner of the resources | - | Yes |
| `environment` | Environment (dev, test, staging, prod) | `dev` | No |
| `location` | Azure region | `East US` | No |
| `node_count` | Number of nodes (if autoscaling disabled) | `3` | No |
| `vm_size` | Size of the VMs | `Standard_DS2_v2` | No |
| `enable_autoscaling` | Enable node autoscaling | `true` | No |
| `enable_rbac` | Enable RBAC | `true` | No |
| `network_policy` | Network policy (azure, calico, none) | `azure` | No |
| `kubernetes_version` | Kubernetes version | `1.28` | No |

### Outputs

After deployment, the following outputs are available:

- `resource_group_name`: Name of the created resource group
- `aks_cluster_name`: Name of the AKS cluster
- `aks_cluster_id`: ID of the AKS cluster
- `aks_fqdn`: FQDN of the AKS cluster
- `kube_config`: Kubernetes configuration (sensitive)
- And more...

### Connect to the Cluster

After deployment, connect to your AKS cluster:

```bash
# Get cluster credentials
az aks get-credentials --resource-group $(terraform output -raw resource_group_name) --name $(terraform output -raw aks_cluster_name)

# Verify connection
kubectl get nodes
```

## Security Considerations

- The cluster uses system-assigned managed identity
- Network policies are enabled by default
- RBAC is enabled by default
- Private networking with dedicated subnet
- Monitoring and logging are enabled

## Cost Optimization

- Uses Standard load balancer (required for production)
- Auto-scaling helps optimize costs based on demand
- Smaller VM sizes can be used for development environments
- Consider reserved instances for production workloads

## Cleanup

To destroy the infrastructure:

```bash
terraform destroy
```

## Support

This template is designed to work with the Azure AI Agent system and follows Azure best practices for AKS deployments.
