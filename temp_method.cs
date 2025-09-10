    private async Task<string?> TryDirectMcpCommandAsync(string message)
    {
        try
        {
            // Check for direct Kubernetes commands that can bypass AI processing
            var lowerMessage = message.ToLowerInvariant().Trim();
            
            _logger.LogInformation("Checking direct MCP command for message: '{Message}' (lowercase: '{LowerMessage}')", message, lowerMessage);
            
            // More robust approach: Check for pod-related keywords and extract pod names
            var podRelatedKeywords = new[] { "pod", "pods", "details", "describe", "info", "information", "insights", "status" };
            var hasPodKeywords = podRelatedKeywords.Any(keyword => lowerMessage.Contains(keyword));
            
            if (hasPodKeywords)
            {
                // Extract potential pod names using multiple patterns
                var podName = ExtractPodNameFromMessage(message);
                
                if (!string.IsNullOrEmpty(podName))
                {
                    _logger.LogInformation("Direct MCP command detected: Pod details for {PodName}", podName);
                    
                    // Debug: List all available plugins
                    _logger.LogInformation("Available plugins: {Plugins}", 
                        string.Join(", ", _kernel.Plugins.Select(p => p.Name)));
                    
                    // Get the AKS MCP plugin (working one) instead of the external MCP plugin
                    var aksPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("AksMcp"));
                    _logger.LogInformation("AKS Plugin found: {Found}, Name: {Name}", 
                        aksPlugin != null, aksPlugin?.Name ?? "null");
                    
                    if (aksPlugin != null)
                    {
                        // Debug: List all functions in the plugin
                        _logger.LogInformation("Available functions in AKS plugin: {Functions}", 
                            string.Join(", ", aksPlugin.Select(f => f.Name)));
                        
                        var kubectlFunction = aksPlugin.FirstOrDefault(f => f.Name == "ExecuteKubectlCommand");
                        _logger.LogInformation("ExecuteKubectlCommand function found: {Found}", kubectlFunction != null);
                        
                        if (kubectlFunction != null)
                        {
                            _logger.LogInformation("Invoking kubectl command: describe pod {PodName}", podName);
                            
                            var result = await _kernel.InvokeAsync(kubectlFunction, new KernelArguments
                            {
                                ["deploymentName"] = "aks-dev-aksworkload-si-002", // Current cluster from kubectl context
                                ["command"] = $"describe pod {podName}"
                            });
                            
                            var response = result.GetValue<string>() ?? "No details available";
                            
                            _logger.LogInformation("Direct MCP result obtained, length: {Length}", response.Length);
                            
                            // Format response with clickable cards for better UX
                            return $"**Pod Details: {podName}**\n\n{response}\n\n" +
                                   "💡 *This response was generated using direct AKS plugin access to save AI tokens.*";
                        }
                        else
                        {
                            _logger.LogWarning("ExecuteKubectlCommand function not found in AKS plugin");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("AKS plugin not found. Available plugins: {Plugins}", 
                            string.Join(", ", _kernel.Plugins.Select(p => p.Name)));
                    }
                }
            }
            
            // Check for general pod listing requests
            var podListingKeywords = new[] { "list", "show", "get", "all" };
            var hasPodListingIntent = podListingKeywords.Any(keyword => lowerMessage.Contains(keyword)) && 
                                    (lowerMessage.Contains("pod") || lowerMessage.Contains("container"));
            
            if (hasPodListingIntent && !hasPodKeywords) // Avoid double processing
            {
                _logger.LogInformation("Direct MCP command detected: Pod listing");
                
                var aksPlugin = _kernel.Plugins.FirstOrDefault(p => p.Name.Contains("AksMcp"));
                if (aksPlugin != null)
                {
                    var kubectlFunction = aksPlugin.FirstOrDefault(f => f.Name == "ExecuteKubectlCommand");
                    if (kubectlFunction != null)
                    {
                        var result = await _kernel.InvokeAsync(kubectlFunction, new KernelArguments
                        {
                            ["deploymentName"] = "aks-dev-aksworkload-si-002",
                            ["command"] = "get pods"
                        });
                        
                        var response = result.GetValue<string>() ?? "No pods found";
                        
                        // Format with clickable elements
                        return $"**Pods in AKS Cluster: aks-dev-aksworkload-si-002**\n\n{response}\n\n" +
                               "💡 *Click on any pod name above for details. This response was generated using direct AKS plugin access.*";
                    }
                }
            }
            
            return null; // Not a direct MCP command, proceed with AI processing
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in direct MCP command processing");
            return null; // Fall back to AI processing
        }
    }
