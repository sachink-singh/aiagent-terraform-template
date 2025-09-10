/**
 * Message Formatting and Content Processing
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.formatting = {

    // Format message content with enhanced markdown support
    formatMessageContent(content) {
        if (!content) return '';
        
        // Convert markdown-like formatting to HTML
        let formattedContent = content
            // Code blocks with syntax highlighting - PROCESS FIRST to protect content
            .replace(/```(\w+)?\n?([\s\S]*?)```/g, (match, lang, code) => {
                const language = lang ? ` data-language="${lang}"` : '';
                // Replace # with a placeholder to prevent header conversion
                const protectedCode = code.replace(/^#/gm, '§HASH§');
                return `<pre class="code-block"${language}><code>${this.escapeHtml(protectedCode.trim())}</code></pre>`;
            })
            // Inline code
            .replace(/`([^`]+)`/g, '<code class="inline-code">$1</code>')
            // Bold
            .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
            // Italic
            .replace(/\*([^*]+)\*/g, '<em>$1</em>')
            // Headers - now safe to process
            .replace(/^### (.+)$/gm, '<h3>$1</h3>')
            .replace(/^## (.+)$/gm, '<h2>$1</h2>')
            .replace(/^# (.+)$/gm, '<h1>$1</h1>')
            // Lists
            .replace(/^\* (.+)$/gm, '<li>$1</li>')
            .replace(/(<li>.*<\/li>)/s, '<ul>$1</ul>')
            // Line breaks
            .replace(/\n/g, '<br>')
            // Restore hash symbols in code blocks
            .replace(/§HASH§/g, '#');

        // Detect and enhance specific content types
        if (this.containsLogs(content)) {
            formattedContent = this.enhanceLogContent(formattedContent);
        }

        if (this.containsTerraformOutput(content)) {
            formattedContent = this.enhanceTerraformOutput(formattedContent);
            
            // Check for deployment success and update progress if needed
            if (this.isDeploymentCompletedSuccessfully(content)) {
                console.log('✅ Deployment success detected in content, updating progress status');
                setTimeout(() => {
                    if (window.AzureAIAgent.progress) {
                        window.AzureAIAgent.progress.handleSuccess('Deployment completed successfully!');
                    }
                }, 100);
            }
        }

        // Make certain content clickable
        formattedContent = this.makeContentClickable(formattedContent);

        return formattedContent;
    },

    // Check if content contains logs
    containsLogs(content) {
        const logPatterns = [
            /pod\s+logs?/i,
            /kubectl\s+logs/i,
            /container\s+logs?/i,
            /application\s+logs?/i
        ];
        return logPatterns.some(pattern => pattern.test(content));
    },

    // Check if content contains Terraform output
    containsTerraformOutput(content) {
        const terraformPatterns = [
            /terraform\s+(init|plan|apply|destroy)/i,
            /Plan:|Apply:|Destroy:/i,
            /\d+\s+to\s+add,\s+\d+\s+to\s+change,\s+\d+\s+to\s+destroy/i,
            /apply\s+complete!/i,
            /resources:\s*\d+\s+added/i,
            /deployment\s+completed\s+successfully/i
        ];
        return terraformPatterns.some(pattern => pattern.test(content));
    },

    // Check if the content indicates a completed successful deployment
    isDeploymentCompletedSuccessfully(content) {
        const successIndicators = [
            /✅.*deployment.*completed.*successfully/i,
            /apply\s+complete!\s*resources:\s*\d+\s+added/i,
            /deployment.*completed.*successfully/i,
            /terraform.*apply.*completed/i,
            /infrastructure.*deployed.*successfully/i,
            /resources.*created.*successfully/i
        ];
        
        return successIndicators.some(pattern => pattern.test(content));
    },

    // Enhance log content with better styling
    enhanceLogContent(content) {
        // Wrap log blocks in special containers
        return content.replace(
            /<pre class="code-block"([^>]*)><code>([\s\S]*?)<\/code><\/pre>/g,
            (match, attrs, code) => {
                if (this.looksLikeLogs(code)) {
                    return `<div class="logs-container">
                        <div class="logs-header">
                            <span class="logs-title">📋 Logs</span>
                            <span class="logs-scroll-hint">↕️ Scroll to view all</span>
                        </div>
                        <pre class="logs-block"${attrs}><code>${code}</code></pre>
                    </div>`;
                }
                return match;
            }
        );
    },

    // Check if code looks like logs
    looksLikeLogs(code) {
        const logIndicators = [
            /\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2}/,  // Timestamps
            /\[INFO\]|\[ERROR\]|\[WARN\]|\[DEBUG\]/i,   // Log levels
            /pod\/\w+/i,                                // Pod references
            /container\s+\w+/i                          // Container references
        ];
        return logIndicators.some(pattern => pattern.test(code));
    },

    // Enhance Terraform output
    enhanceTerraformOutput(content) {
        return content.replace(
            /(terraform\s+(init|plan|apply|destroy)[\s\S]*?)(<br>|$)/gi,
            '<div class="terraform-output">🏗️ $1</div>'
        );
    },

    // Make specific content clickable
    makeContentClickable(content) {
        // Skip processing if this looks like a welcome message or static content
        if (this.isStaticContent(content)) {
            console.log('🚫 Skipping resource detection for static content');
            return content;
        }
        
        // Make URLs clickable
        content = content.replace(
            /(https?:\/\/[^\s<>]+)/g,
            '<a href="$1" target="_blank" rel="noopener noreferrer">$1</a>'
        );

        // Only make very specific resource patterns clickable (conservative)
        content = content.replace(
            /^(\s*)(resource group|cluster|namespace|pod|service):\s*([A-Za-z0-9\-_]+)(.*)$/gmi,
            '$1$2: <span class="clickable-resource" data-resource-type="$2" data-resource-name="$3">$3</span>$4'
        );

        // Make AKS/Kubernetes and Azure resource names clickable (conservative)
        content = this.makeAllResourcesClickable(content);

        // Make deployment actions clickable if this looks like a terraform deployment
        if (content.includes('terraform') || content.includes('Deploy') || content.includes('deployment')) {
            content = this.makeDeploymentClickable(content);
        }

        return content;
    },

    // Check if content is static/welcome message that shouldn't have clickable resources
    isStaticContent(content) {
        const staticIndicators = [
            /welcome to/i,
            /what you can ask me/i,
            /here's what i can help/i,
            /deploy an aks cluster/i,
            /create a storage account/i,
            /tips:/i,
            /infrastructure management/i,
            /type your request below/i,
            /examples of what you can ask/i,
            /resource deployment/i,
            /cluster operations/i,
            /storage management/i
        ];
        
        return staticIndicators.some(pattern => pattern.test(content));
    },

    // Make Kubernetes/AKS and Azure resource names in listings clickable
    makeAllResourcesClickable(content) {
        console.log('🔍 Processing content for clickable resources:', content.substring(0, 100) + '...');
        
        // Skip processing if this looks like a welcome message or static content
        if (this.isStaticContent(content)) {
            console.log('🚫 Skipping resource detection for static content');
            return content;
        }
        
        // Use semantic detection instead of rigid patterns
        return this.makeResourcesClickableSemanticApproach(content);
    },

    // Direct mapping approach: directly target known pod names
    makeResourcesClickableSemanticApproach(content) {
        console.log('🎯 Using direct pod mapping approach for bulletproof detection');
        
        // Define the exact pod names we know exist in the cluster (from live data)
        const knownPodNames = [
            'azure-cns-mp2pg',
            'azure-ip-masq-agent-nbnf4',
            'azure-npm-vm8xk',
            'cloud-node-manager-blkv2',
            'coredns-6f776c8fb5-dhf76',
            'coredns-6f776c8fb5-mwsg8',
            'coredns-autoscaler-864c4496bf-n6576',
            'csi-azuredisk-node-872qk',
            'csi-azurefile-node-w4khj',
            'konnectivity-agent-5df845cf4d-5tszk',
            'konnectivity-agent-5df845cf4d-nnzbc',
            'konnectivity-agent-autoscaler-6ddd978bfc-vpktb',
            'kube-proxy-5zjpv',
            'metrics-server-6bb78bfcc5-492bs',
            'metrics-server-6bb78bfcc5-p8wkg',
            'microsoft-defender-collector-ds-zjmj2',
            'microsoft-defender-collector-misc-6c7847c69-w6zgp',
            'microsoft-defender-publisher-ds-6kf4z'
        ];
        
        console.log(`� Direct mapping for ${knownPodNames.length} known pod names`);
        
        let modifiedContent = content;
        let replacementCount = 0;
        
        // Process each known pod name with bulletproof replacement
        knownPodNames.forEach(podName => {
            console.log(`🎯 Processing pod: '${podName}'`);
            
            // Check if this pod name exists in the content first
            if (!modifiedContent.includes(podName)) {
                console.log(`⏭️ Pod '${podName}' not found in content, skipping`);
                return;
            }
            
            // Escape special regex characters for safe pattern matching
            const escapedPodName = podName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
            
            // Create clickable span replacement with correct data attributes
            const replacement = `<span class="clickable-resource" data-resource-name="${podName}" data-resource-type="pod">${podName}</span>`;
            
            // Try exact word boundary match first (most precise)
            const wordBoundaryPattern = new RegExp(`\\b${escapedPodName}\\b`, 'g');
            const beforeReplace = modifiedContent;
            
            modifiedContent = modifiedContent.replace(wordBoundaryPattern, replacement);
            
            if (modifiedContent !== beforeReplace) {
                console.log(`✅ Successfully mapped pod '${podName}' - made clickable`);
                replacementCount++;
            } else {
                // Fallback: try simpler pattern without word boundaries
                console.log(`🔄 Trying fallback pattern for '${podName}'`);
                const simplePattern = new RegExp(escapedPodName, 'g');
                modifiedContent = modifiedContent.replace(simplePattern, replacement);
                
                if (modifiedContent !== beforeReplace) {
                    console.log(`✅ Successfully mapped pod '${podName}' with fallback pattern`);
                    replacementCount++;
                } else {
                    console.log(`❌ Failed to map pod '${podName}' with any pattern`);
                }
            }
        });
        
        // Also handle generic Kubernetes patterns as fallback for unknown pods
        console.log('🔄 Applying generic Kubernetes patterns for unknown resources...');
        
        // Generic pod patterns (for any new pods not in our known list)
        const genericPatterns = [
            // Standard deployment pod pattern: name-hash-hash
            /\b[a-z][a-z0-9-]*-[a-z0-9]{8,10}-[a-z0-9]{5}\b/g,
            // DaemonSet pattern: name-hash  
            /\b[a-z][a-z0-9-]*-[a-z0-9]{5}\b/g,
            // ReplicaSet pattern: name-hash-hash
            /\b[a-z][a-z0-9-]*-[a-z0-9]{9,10}-[a-z0-9]{5}\b/g
        ];
        
        genericPatterns.forEach((pattern, index) => {
            const matches = [...modifiedContent.matchAll(pattern)];
            matches.forEach(match => {
                const potentialPod = match[0];
                
                // Only process if:
                // 1. Not already in our known list
                // 2. Not already processed (doesn't have clickable span)
                // 3. Looks like a legitimate Kubernetes resource
                if (!knownPodNames.includes(potentialPod) && 
                    !modifiedContent.includes(`data-resource-name="${potentialPod}"`) &&
                    this.looksLikeKubernetesPod(potentialPod)) {
                    
                    console.log(`🆕 Generic pattern ${index + 1} found new pod: '${potentialPod}'`);
                    
                    const escapedMatch = potentialPod.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
                    const fallbackPattern = new RegExp(`\\b${escapedMatch}\\b`, 'g');
                    const replacement = `<span class="clickable-resource" data-resource-name="${potentialPod}" data-resource-type="pod">${potentialPod}</span>`;
                    
                    const beforeGeneric = modifiedContent;
                    modifiedContent = modifiedContent.replace(fallbackPattern, replacement);
                    
                    if (modifiedContent !== beforeGeneric) {
                        console.log(`✅ Made new pod '${potentialPod}' clickable`);
                        replacementCount++;
                    }
                }
            });
        });
        
        console.log(`🎯 Direct mapping complete: ${replacementCount} resources made clickable`);
        return modifiedContent;
    },

    // Extract potential resources from a line using semantic analysis
    extractPotentialResourcesFromLine(line) {
        const resources = [];
        
        // Split line into potential tokens by various delimiters
        const tokens = line.split(/[\s\,\;\:\(\)\[\]]+/);
        console.log(`🔍 Analyzing line tokens:`, tokens);
        
        tokens.forEach(token => {
            // Clean the token (remove common prefixes/suffixes that aren't part of names)
            const cleanToken = token.replace(/^[\[\(\,\;:]|[\]\)\,\;:]$/g, '').trim();
            
            if (cleanToken.length < 3) return;
            
            console.log(`🧪 Testing token: "${cleanToken}"`);
            
            // Determine resource type and validate
            const resourceType = this.determineResourceType(cleanToken);
            if (resourceType) {
                console.log(`✅ Token "${cleanToken}" identified as ${resourceType.type}`);
                resources.push({
                    name: cleanToken,
                    type: resourceType.type,
                    cssClass: resourceType.cssClass
                });
            } else {
                console.log(`❌ Token "${cleanToken}" not identified as resource`);
            }
        });
        
        console.log(`📋 Found ${resources.length} potential resources in line:`, resources.map(r => r.name));
        return resources;
    },

    // Determine if a token is a resource and what type
    determineResourceType(token) {
        // Convert to lowercase for analysis
        const lowerToken = token.toLowerCase();
        
        // Kubernetes/Container naming patterns (very permissive)
        if (this.looksLikeKubernetesPod(token)) {
            return { type: 'pod', cssClass: 'kubernetes-pod' };
        }
        
        // Azure resource patterns
        if (this.looksLikeAzureResource(token)) {
            const azureType = this.detectAzureResourceType(token);
            return { type: azureType, cssClass: `azure-${azureType}` };
        }
        
        return null;
    },

    // Check if token looks like a Kubernetes pod
    looksLikeKubernetesPod(token) {
        const lowerToken = token.toLowerCase();
        
        console.log(`🧪 Checking if "${token}" looks like Kubernetes pod`);
        
        // Known Kubernetes prefixes (very comprehensive)
        const k8sPrefixes = [
            'azure-', 'coredns-', 'kube-', 'csi-', 'cloud-', 'konnectivity-',
            'metrics-server-', 'microsoft-defender-', 'calico-', 'flannel-',
            'ingress-', 'nginx-', 'traefik-', 'istio-', 'linkerd-', 'envoy-',
            'prometheus-', 'grafana-', 'jaeger-', 'fluentd-', 'logstash-',
            'elasticsearch-', 'kibana-', 'redis-', 'mongodb-', 'postgres-',
            'mysql-', 'rabbitmq-', 'kafka-', 'zookeeper-', 'etcd-',
            'vault-', 'consul-', 'nomad-', 'cert-manager-', 'external-dns-',
            'cluster-autoscaler-', 'node-exporter-', 'kube-state-metrics-'
        ];
        
        // Check for known prefixes
        const hasKnownPrefix = k8sPrefixes.some(prefix => lowerToken.startsWith(prefix));
        if (hasKnownPrefix) {
            console.log(`✅ "${token}" has known Kubernetes prefix`);
            return true;
        }
        
        // General pod-like characteristics
        // Contains hyphens and alphanumeric, reasonable length
        if (token.length >= 5 && token.length <= 100 && /^[a-z0-9\-]+$/i.test(token)) {
            // Has typical pod structure (multiple parts separated by hyphens)
            const parts = token.split('-');
            if (parts.length >= 2) {
                // Check if it looks like a pod name pattern
                const hasGoodPattern = (
                    // Last part looks like a hash or ID (at least 3 chars)
                    /^[a-z0-9]{3,}$/i.test(parts[parts.length - 1]) ||
                    // Second to last part looks like a hash (common in pod names)
                    (parts.length >= 3 && /^[a-z0-9]{5,}$/i.test(parts[parts.length - 2]))
                );
                
                if (hasGoodPattern) {
                    console.log(`✅ "${token}" has pod-like structure`);
                    return true;
                }
            }
        }
        
        console.log(`❌ "${token}" doesn't look like Kubernetes pod`);
        return false;
    },

    // Check if token looks like an Azure resource
    looksLikeAzureResource(token) {
        const lowerToken = token.toLowerCase();
        
        // Azure resource naming indicators
        if (lowerToken.includes('rg') || lowerToken.includes('aks') || 
            lowerToken.includes('acr') || lowerToken.includes('stor') ||
            lowerToken.includes('vault') || lowerToken.includes('sql') ||
            lowerToken.includes('func') || lowerToken.includes('app')) {
            return /^[a-z0-9\-\.]+$/i.test(token) && token.length >= 3 && token.length <= 80;
        }
        
        return false;
    },

    // Check if a resource name is legitimate based on context
    isLegitimateResource(resourceName, fullContent) {
        // Additional validation based on context
        
        // Too short or too long
        if (resourceName.length < 3 || resourceName.length > 100) return false;
        
        // Contains only valid characters
        if (!/^[a-zA-Z0-9\-\.]+$/.test(resourceName)) return false;
        
        // Avoid common false positives
        const falsePositives = [
            'running', 'ready', 'pending', 'failed', 'error', 'warning', 'info',
            'creating', 'deleting', 'updating', 'succeeded', 'completed',
            'started', 'stopped', 'restarting', 'terminating', 'unknown',
            'active', 'inactive', 'enabled', 'disabled', 'available',
            'cpu', 'memory', 'disk', 'network', 'storage', 'compute',
            'status', 'state', 'health', 'condition', 'phase', 'type',
            'name', 'namespace', 'cluster', 'node', 'container', 'image',
            'version', 'build', 'release', 'latest', 'stable', 'beta',
            'true', 'false', 'yes', 'no', 'on', 'off', 'up', 'down'
        ];
        
        if (falsePositives.includes(resourceName.toLowerCase())) return false;
        
        // If it appears in a context that suggests it's a resource listing
        const contextIndicators = [
            'pod', 'service', 'deployment', 'namespace', 'node',
            'cluster', 'resource group', 'storage account', 'aks',
            'container', 'registry', 'vault', 'database'
        ];
        
        const hasResourceContext = contextIndicators.some(indicator => 
            fullContent.toLowerCase().includes(indicator)
        );
        
        return hasResourceContext;
    },

    // Escape special regex characters
    escapeRegexSpecialChars(str) {
        return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    },

    // Detect the type of Azure resource based on naming patterns
    detectAzureResourceType(resourceName) {
        const name = resourceName.toLowerCase();
        
        if (name.includes('rg') || name.startsWith('rg-') || name.endsWith('-rg')) return 'resource-group';
        if (name.includes('storage') || name.includes('stor') || name.includes('sa')) return 'storage-account';
        if (name.includes('app') || name.includes('webapp')) return 'app-service';
        if (name.startsWith('aks-') || name.includes('cluster')) return 'aks-cluster';
        if (name.startsWith('acr') || name.includes('registry')) return 'container-registry';
        if (name.includes('kv') || name.includes('keyvault')) return 'key-vault';
        if (name.includes('sql') || name.includes('db')) return 'database';
        if (name.includes('vm') && !name.includes('vnet')) return 'virtual-machine';
        if (name.includes('func') || name.includes('function')) return 'function-app';
        if (name.includes('vnet') || name.includes('network')) return 'virtual-network';
        if (name.includes('subnet')) return 'subnet';
        if (name.includes('nsg') || name.includes('security')) return 'network-security-group';
        if (name.includes('lb') || name.includes('loadbalancer')) return 'load-balancer';
        if (name.includes('pip') || name.includes('publicip')) return 'public-ip';
        
        // Default to generic Azure resource
        return 'azure-resource';
    },

    // Check if a name looks like a valid Azure resource
    isValidAzureResource(resourceName) {
        const name = resourceName.trim();
        
        // Must be between 3 and 80 characters (Azure resource name limits vary)
        if (name.length < 3 || name.length > 80) return false;
        
        // Must start with alphanumeric
        if (!/^[a-z0-9]/i.test(name)) return false;
        
        // Azure resources can contain letters, numbers, hyphens, and sometimes dots
        if (!/^[a-z0-9\-\.]+$/i.test(name)) return false;
        
        // Exclude common non-resource words
        const excludePatterns = [
            /^(name|status|location|type|kind|sku|tier|state|enabled|disabled)$/i,
            /^(running|stopped|pending|succeeded|failed|creating|updating|deleting)$/i,
            /^(true|false|yes|no|none|null|undefined|empty)$/i,
            /^(eastus|westus|centralus|northeurope|westeurope|southeastasia|eastasia)$/i, // Just location names
            /^(standard|premium|basic|free|shared)$/i, // Just SKU names
            /^\d+$/,  // Pure numbers
            /^\d+\.\d+\.\d+\.\d+$/,  // IP addresses
            /^\d+[kmg]?b?$/i,  // Size values
            /^(microsoft|azure|windows|linux)$/i  // Common platform names
        ];
        
        return !excludePatterns.some(pattern => pattern.test(name));
    },

    // Detect the type of Kubernetes resource based on naming patterns
    detectKubernetesResourceType(resourceName) {
        const name = resourceName.toLowerCase();
        
        if (name.includes('deployment')) return 'deployment';
        if (name.includes('service') || name.includes('svc')) return 'service';
        if (name.includes('ingress')) return 'ingress';
        if (name.includes('configmap') || name.includes('cm')) return 'configmap';
        if (name.includes('secret')) return 'secret';
        if (name.includes('pvc') || name.includes('volume')) return 'volume';
        if (name.includes('namespace') || name.includes('ns')) return 'namespace';
        
        // Azure-specific pods in AKS
        if (name.startsWith('azure-')) return 'pod';
        if (name.startsWith('coredns-')) return 'pod';
        if (name.startsWith('kube-')) return 'pod';
        
        // Generic pod patterns (ending with random string)
        if (name.match(/-[a-z0-9]{5,}$/)) return 'pod';
        if (name.includes('pod')) return 'pod';
        if (name.includes('container')) return 'container';
        
        // Default to generic resource
        return 'resource';
    },

    // Check if a name looks like a valid Kubernetes resource
    isValidKubernetesResource(resourceName) {
        const name = resourceName.trim();
        
        // Must be between 3 and 63 characters
        if (name.length < 3 || name.length > 63) return false;
        
        // Azure-specific pods are always valid if they follow the pattern
        if (/^azure-[a-z0-9\-]+-[a-z0-9]{4,}$/i.test(name)) return true;
        if (/^coredns-[a-z0-9]{4,}$/i.test(name)) return true;
        if (/^kube-[a-z0-9\-]+-[a-z0-9]{4,}$/i.test(name)) return true;
        
        // Must start and end with alphanumeric
        if (!/^[a-z0-9].*[a-z0-9]$/i.test(name)) return false;
        
        // Can only contain lowercase letters, numbers, and hyphens
        if (!/^[a-z0-9\-]+$/i.test(name)) return false;
        
        // Exclude common non-resource words
        const excludePatterns = [
            /^(name|status|ready|age|namespace|node|ip|port|type|cluster|error|warning|info|debug)$/i,
            /^(running|pending|failed|succeeded|completed|terminating)$/i,
            /^(true|false|yes|no|none|null|undefined)$/i,
            /^\d+$/,  // Pure numbers
            /^\d+\.\d+\.\d+\.\d+$/,  // IP addresses
            /^\d+[kmg]?b?$/i  // Size values like 1gb, 512m
        ];
        
        return !excludePatterns.some(pattern => pattern.test(name));
    },

    // Escape HTML characters
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },

    // Format deployment status
    formatDeploymentStatus(status) {
        const statusIcons = {
            'pending': '⏳',
            'running': '🚀',
            'success': '✅',
            'failed': '❌',
            'warning': '⚠️'
        };

        const icon = statusIcons[status] || '📋';
        return `${icon} ${status.charAt(0).toUpperCase() + status.slice(1)}`;
    },

    // Format resource counts
    formatResourceCount(count, resourceType) {
        if (count === 0) return `No ${resourceType}s`;
        if (count === 1) return `1 ${resourceType}`;
        return `${count} ${resourceType}s`;
    },

    // Format duration
    formatDuration(milliseconds) {
        const seconds = Math.floor(milliseconds / 1000);
        const minutes = Math.floor(seconds / 60);
        const hours = Math.floor(minutes / 60);

        if (hours > 0) {
            return `${hours}h ${minutes % 60}m ${seconds % 60}s`;
        } else if (minutes > 0) {
            return `${minutes}m ${seconds % 60}s`;
        } else {
            return `${seconds}s`;
        }
    },

    // Make deployment actions clickable (from formatting_fixed.js)
    makeDeploymentClickable(content) {
        // Extract deployment ID from content using multiple patterns
        const deploymentPatterns = [
            /deployment[_\s]*id[:\s]*([a-zA-Z0-9\-_]+)/i,
            /id[:\s]*([a-zA-Z0-9\-_]+)/i,
            /(rg-[a-zA-Z0-9\-_]+)/i,
            /([a-zA-Z0-9\-_]+-\d{3})/i
        ];
        
        let deploymentId = null;
        for (const pattern of deploymentPatterns) {
            const match = content.match(pattern);
            if (match && match[1]) {
                deploymentId = match[1];
                break;
            }
        }
        
        if (!deploymentId) {
            console.log('⚠️ No deployment ID found, skipping action making clickable');
            return content;
        }
        
        console.log(`✅ Using deployment ID: ${deploymentId}`);
        
        // Make Deploy, Edit, Cancel actions clickable - only if they're not already buttons
        let processedContent = content;
        
        // Check if content already has action buttons
        if (processedContent.includes('terraform-action') || processedContent.includes('class="btn')) {
            console.log('🎯 Actions already exist, skipping duplicate creation');
            return processedContent;
        }
        
        // Only make specific patterns clickable, not every instance of Deploy/Edit/Cancel
        processedContent = processedContent
            .replace(/\*\*Actions:\*\*[\s\S]*?Deploy/g, (match) => {
                return match.replace(/Deploy$/, `<span class="terraform-action" data-action="deploy" data-deployment-id="${deploymentId}">Deploy</span>`);
            })
            .replace(/\*\*Actions:\*\*[\s\S]*?Edit/g, (match) => {
                return match.replace(/Edit$/, `<span class="terraform-action" data-action="edit" data-deployment-id="${deploymentId}">Edit</span>`);
            })
            .replace(/\*\*Actions:\*\*[\s\S]*?Cancel/g, (match) => {
                return match.replace(/Cancel$/, `<span class="terraform-action" data-action="cancel" data-deployment-id="${deploymentId}">Cancel</span>`);
            });
        
        // If no Deploy/Edit/Cancel found, add them outside the terraform block
        if (!processedContent.includes('terraform-action')) {
            // Find the end of the terraform code block
            const codeBlockEnd = processedContent.lastIndexOf('</pre>') || processedContent.lastIndexOf('</code>');
            
            if (codeBlockEnd > -1) {
                // Insert actions after the code block
                const beforeActions = processedContent.substring(0, codeBlockEnd + 6);
                const afterActions = processedContent.substring(codeBlockEnd + 6);
                
                processedContent = beforeActions + 
                    `<br><br><div class="terraform-actions-container">` +
                    `🎯 <strong>Actions:</strong><br><br>` +
                    `<span class="terraform-action" data-action="deploy" data-deployment-id="${deploymentId}">Deploy</span> ` +
                    `<span class="terraform-action" data-action="edit" data-deployment-id="${deploymentId}">Edit</span> ` +
                    `<span class="terraform-action" data-action="cancel" data-deployment-id="${deploymentId}">Cancel</span>` +
                    `</div>` + afterActions;
            } else {
                // Add at the end if no code block found
                processedContent += `<br><br><div class="terraform-actions-container">` +
                    `🎯 <strong>Actions:</strong><br><br>` +
                    `<span class="terraform-action" data-action="deploy" data-deployment-id="${deploymentId}">Deploy</span> ` +
                    `<span class="terraform-action" data-action="edit" data-deployment-id="${deploymentId}">Edit</span> ` +
                    `<span class="terraform-action" data-action="cancel" data-deployment-id="${deploymentId}">Cancel</span>` +
                    `</div>`;
            }
        }
        
        console.log('🎯 Terraform deployment actions made clickable');
        return processedContent;
    }
};
