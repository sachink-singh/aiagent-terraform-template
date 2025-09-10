using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

class Program
{
    static async Task Main(string[] args)
    {
        var deploymentDir = @"C:\Users\sacsing\.azure-ai-agent\terraform\aks-cluster-20250903-194508";
        var errorMessage = @"Error: creating Kubernetes Cluster (Subscription: ""04024b46-1c4b-487f-82e3-befc24d4c80e""
Resource Group Name: ""rg-dev-status-demo-eastus-f9d432b9""
Kubernetes Cluster Name: ""aks-dev-status-demo-f9d432b9""): performing CreateOrUpdate: unexpected status 400 (400 Bad Request) with response: {
  ""code"": ""BadRequest"",
  ""details"": null,
  ""message"": ""The VM size of Standard_DS2_v2 is not allowed in your subscription in location 'eastus'.  For more details, please visit https://aka.ms/aks/quotas-skus-regions"",
  ""subcode"": """"
 }";

        await GenerateAlternativeConfigurations(deploymentDir, "Standard_DS2_v2", "eastus");
        Console.WriteLine("Alternative configurations generated successfully!");
    }

    static async Task GenerateAlternativeConfigurations(string workingDirectory, string currentVmSize, string currentRegion)
    {
        try
        {
            var alternativeVmSizes = new[]
            {
                "Standard_B2s",
                "Standard_D2s_v3", 
                "Standard_D2as_v4",
                "Standard_DS1_v2",
                "Standard_B2ms",
                "Standard_D2_v3",
                "Standard_A2_v2"
            };

            var alternativeRegions = new[]
            {
                "westus2",
                "westus", 
                "centralus",
                "eastus2",
                "westeurope",
                "northeurope"
            };

            var originalTfvarsPath = Path.Combine(workingDirectory, "terraform.tfvars.json");
            if (!File.Exists(originalTfvarsPath))
                return;

            var originalContent = await File.ReadAllTextAsync(originalTfvarsPath);
            var originalConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(originalContent);
            
            if (originalConfig == null)
                return;

            // Generate alternative with different VM size
            var altVmSize = alternativeVmSizes.FirstOrDefault(vm => vm != currentVmSize) ?? "Standard_B2s";
            var vmAltConfig = new Dictionary<string, object>(originalConfig);
            
            // Update or add vm_size parameter
            vmAltConfig["vm_size"] = altVmSize;
            if (vmAltConfig.ContainsKey("node_vm_size"))
                vmAltConfig["node_vm_size"] = altVmSize;

            await File.WriteAllTextAsync(
                Path.Combine(workingDirectory, "terraform-alt-vmsize.tfvars.json"),
                JsonSerializer.Serialize(vmAltConfig, new JsonSerializerOptions { WriteIndented = true })
            );

            // Generate alternative with different region
            var altRegion = alternativeRegions.FirstOrDefault(r => r != currentRegion) ?? "westus2";
            var regionAltConfig = new Dictionary<string, object>(originalConfig);
            
            // Update location parameter
            regionAltConfig["location"] = altRegion;

            await File.WriteAllTextAsync(
                Path.Combine(workingDirectory, "terraform-alt-region.tfvars.json"),
                JsonSerializer.Serialize(regionAltConfig, new JsonSerializerOptions { WriteIndented = true })
            );

            // Generate combined alternative (different VM + different region)
            var combinedAltConfig = new Dictionary<string, object>(originalConfig);
            combinedAltConfig["vm_size"] = altVmSize;
            if (combinedAltConfig.ContainsKey("node_vm_size"))
                combinedAltConfig["node_vm_size"] = altVmSize;
            combinedAltConfig["location"] = altRegion;

            await File.WriteAllTextAsync(
                Path.Combine(workingDirectory, "terraform-alt-combined.tfvars.json"),
                JsonSerializer.Serialize(combinedAltConfig, new JsonSerializerOptions { WriteIndented = true })
            );

            // Create a README with instructions
            var readmeContent = $@"# Azure Resource Constraint Resolution

The original deployment failed due to Azure resource constraints:
- VM Size: {currentVmSize} not available in region: {currentRegion}

## Alternative Configurations Generated:

### 1. Alternative VM Size (Same Region)
File: `terraform-alt-vmsize.tfvars.json`
- VM Size: {altVmSize}
- Region: {currentRegion}

Command: `terraform apply -var-file=""terraform-alt-vmsize.tfvars.json"" -auto-approve`

### 2. Alternative Region (Same VM Size)  
File: `terraform-alt-region.tfvars.json`
- VM Size: {currentVmSize}
- Region: {altRegion}

Command: `terraform apply -var-file=""terraform-alt-region.tfvars.json"" -auto-approve`

### 3. Combined Alternative (Different VM + Region)
File: `terraform-alt-combined.tfvars.json`
- VM Size: {altVmSize}
- Region: {altRegion}

Command: `terraform apply -var-file=""terraform-alt-combined.tfvars.json"" -auto-approve`

## Next Steps:
1. Choose one of the alternative configurations above
2. Run the corresponding terraform apply command in this directory
3. Monitor deployment status via the Azure AI Agent dashboard

## Auto-Retry Recommendation:
The system recommends trying: **terraform-alt-combined.tfvars.json** (VM: {altVmSize}, Region: {altRegion})
";

            await File.WriteAllTextAsync(
                Path.Combine(workingDirectory, "AZURE_CONSTRAINT_RESOLUTION.md"),
                readmeContent
            );
            
            Console.WriteLine("Alternative configurations generated successfully in {0}", workingDirectory);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Failed to generate alternative configurations: {0}", ex.Message);
        }
    }
}
