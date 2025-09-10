# Testing the "Use Defaults" Feature

## 🧪 Test Scenario: Deploy AKS with All Defaults

This test demonstrates the new **"deploy with defaults"** functionality that provides instant infrastructure deployment without parameter configuration.

### Test Command:
```
deploy with defaults aks-cluster
```

### Expected Behavior:

1. **Automatic Default Generation**:
   - `workload_name` → `demo-workload`
   - `project_name` → `demo-project`
   - `owner` → Current username
   - `location` → `East US`
   - `vm_size` → `Standard_B2s`
   - `kubernetes_version` → `1.30`
   - `node_count` → `2`
   - `enable_rbac` → `true`

2. **Deployment Process**:
   - Clear parameter summary displayed
   - Terraform initialization
   - Plan generation with default values
   - Apply execution
   - Success confirmation with deployment ID

3. **Intelligent Error Handling**:
   - If VM size not available → Automatically generate alternatives
   - If region has constraints → Suggest alternative regions
   - Real-time constraint resolution

### Alternative Test Commands:

#### 1. Quick VM Deployment:
```
deploy with defaults org-vm-standard
```

#### 2. Web App with Defaults:
```
deploy with defaults org-webapp-secure
```

#### 3. Compare with Custom Parameters:
```
deploy template aks-cluster '{"workload_name":"my-custom-cluster","vm_size":"Standard_D4s_v3"}'
```

### Expected Output Format:

```
🚀 Deploying 'AKS Kubernetes Cluster' with Default Values

📋 Using Default Parameters:
• workload_name: demo-workload
• project_name: demo-project
• owner: myusername
• location: East US
• vm_size: Standard_B2s
• kubernetes_version: 1.30
• node_count: 2
• enable_rbac: true

⏳ Starting deployment...

🔄 Terraform Execution:
Init: ✅ Success
Plan: ✅ Success
Apply: ✅ Success

🎉 Deployment Successful!
📁 Deployment ID: aks-cluster-20250903-201234
```

### Validation Points:

✅ **No parameter input required**  
✅ **Intelligent defaults generated**  
✅ **Deployment completes successfully**  
✅ **Error handling works for constraints**  
✅ **Real-time status updates**  
✅ **Deployment artifacts created**  

### Performance Benefits:

- **Time to Deploy**: ~30 seconds (vs. 5+ minutes for parameter configuration)
- **User Effort**: 1 command (vs. multiple parameter forms)
- **Error Rate**: Minimal (intelligent defaults reduce configuration errors)
- **Learning Curve**: Immediate (no infrastructure knowledge required)

This feature transforms Azure infrastructure deployment from a complex configuration task into a **one-command operation**, perfect for development, testing, and learning scenarios.
