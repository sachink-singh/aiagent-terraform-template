# Azure AI Agent - Default Parameter Deployment

## 🚀 Quick Deployment with Defaults

The Azure AI Agent now supports **ultra-fast deployment** using intelligent default values! This feature is perfect for:

- **Quick prototyping**
- **Development environments** 
- **Testing infrastructure templates**
- **Learning and experimentation**

## 📋 How to Use Defaults

### Option 1: Deploy with Defaults Command
```
deploy with defaults <template-id>
```

**Example:**
```
deploy with defaults aks-cluster
```

### Option 2: Template Gallery
1. Run `show templates` to see available templates
2. Look for the "deploy with defaults" command for each template
3. Use the command to deploy instantly

## 🤖 Intelligent Default Generation

The system automatically generates sensible defaults for all parameters:

### **Naming Defaults**
- `vm_name`, `cluster_name`, `app_name` → `templateid-20250903-194508`
- `workload_name` → `demo-workload`
- `project_name` → `demo-project` 
- `owner` → Your username

### **Environment Defaults**
- `environment` → `dev`
- `location`/`region` → `East US`
- `business_unit` → `IT`

### **Size & Capacity Defaults**
- `vm_size`, `node_pool_size` → `Standard_B2s` (cost-effective)
- `node_count` → `2`
- `kubernetes_version` → `1.30` (latest stable)

### **Security Defaults**
- `enable_rbac` → `true`
- `enable_*` (any enable flag) → `true`

### **Type-Based Defaults**
- **String parameters** → Descriptive default values
- **Boolean parameters** → `false` (unless security-related)
- **Number parameters** → `1`

## 📊 Example Output

When you run `deploy with defaults aks-cluster`, you'll see:

```
🚀 Deploying 'AKS Kubernetes Cluster' with Default Values

📋 Using Default Parameters:
• workload_name: `demo-workload`
• project_name: `demo-project`
• owner: `myusername`
• location: `East US`
• vm_size: `Standard_B2s`
• kubernetes_version: `1.30`
• node_count: `2`
• enable_rbac: `true`

⏳ Starting deployment...

🔄 Terraform Execution:
Init: ✅ Success
Plan: ✅ Success  
Apply: ✅ Success

🎉 Deployment Successful!
📁 Deployment ID: aks-cluster-20250903-194508
```

## 🛡️ Intelligent Error Handling

Even with defaults, the system includes:

- **Azure constraint detection** (VM sizes, regions)
- **Automatic alternative generation** (different VM sizes/regions)
- **Smart retry mechanisms** with viable configurations

## 💡 Best Practices

### **When to Use Defaults:**
✅ Development and testing environments  
✅ Learning and experimentation  
✅ Quick prototyping  
✅ Demo scenarios  

### **When to Use Custom Parameters:**
✅ Production deployments  
✅ Specific compliance requirements  
✅ Complex networking setups  
✅ Enterprise naming conventions  

## 🔄 Workflow Integration

The defaults work seamlessly with the existing workflow:

1. **Browse Templates**: `show templates`
2. **Quick Deploy**: `deploy with defaults template-id`
3. **Monitor Progress**: Real-time status tracking
4. **Handle Constraints**: Automatic alternative generation
5. **Success**: Ready-to-use infrastructure

## 🎯 Summary

The **"use defaults"** feature transforms infrastructure deployment from a complex parameter configuration task into a **one-command operation**, perfect for rapid development and testing scenarios while maintaining enterprise-grade error handling and constraint resolution.

Use it whenever you need infrastructure **fast** and don't want to spend time on parameter configuration!
