/**
 * Message Formatting and Content Processing - Fixed Version
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.formatting = {

    // Format message content with enhanced markdown support
    formatMessageContent(content) {
        if (!content) return '';
        
        // If content already contains HTML tags, don't process as markdown
        if (content.includes('<pre') || content.includes('<code') || content.includes('<strong>') || content.includes('<br>')) {
            console.log('📝 Content already contains HTML, skipping markdown processing');
            return this.makeContentClickable(content);
        }
        
        // Convert markdown-like formatting to HTML
        let formattedContent = content
            // Code blocks with syntax highlighting - PROCESS FIRST to protect from header conversion
            .replace(/```(\w+)?\n?([\s\S]*?)```/g, (match, lang, code) => {
                const language = lang ? ` data-language="${lang}"` : '';
                // Don't process headers inside code blocks - keep them as plain text
                return `<pre class="code-block"${language}><code>${this.escapeHtml(code.trim())}</code></pre>`;
            })
            // Inline code
            .replace(/`([^`]+)`/g, '<code class="inline-code">$1</code>')
            // Bold
            .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
            // Italic
            .replace(/\*([^*]+)\*/g, '<em>$1</em>')
            // Headers - BUT ONLY OUTSIDE CODE BLOCKS (process after code blocks are converted)
            .replace(/^### (.+)$/gm, '<h3>$1</h3>')
            .replace(/^## (.+)$/gm, '<h2>$1</h2>')
            .replace(/^# (.+)$/gm, '<h1>$1</h1>')
            // Lists
            .replace(/^\* (.+)$/gm, '<li>$1</li>')
            .replace(/(<li>.*<\/li>)/s, '<ul>$1</ul>')
            // Line breaks
            .replace(/\n/g, '<br>');

        // Detect and enhance specific content types
        if (this.containsLogs(content)) {
            formattedContent = this.enhanceLogContent(formattedContent);
        }

        if (this.containsTerraformOutput(content)) {
            formattedContent = this.enhanceTerraformOutput(formattedContent);
        }

        // Make certain content clickable
        formattedContent = this.makeContentClickable(formattedContent);

        return formattedContent;
    },

    // Check if content contains logs
    containsLogs(content) {
        const logIndicators = [
            /\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}/,
            /\[INFO\]|\[ERROR\]|\[WARNING\]|\[DEBUG\]/i,
            /^\s*\d+\s+/m,
            /stdout|stderr/i
        ];
        
        return logIndicators.some(pattern => pattern.test(content));
    },

    // Check if content contains Terraform output
    containsTerraformOutput(content) {
        const terraformIndicators = [
            /terraform\s+(plan|apply|destroy)/i,
            /Plan:\s*\d+\s+to\s+add/i,
            /Apply\s+complete!/i,
            /resource\s+"[^"]+"\s+"[^"]+"/,
            /Changes\s+to\s+Outputs:/i
        ];
        
        return terraformIndicators.some(pattern => pattern.test(content));
    },

    // Enhance log content with syntax highlighting
    enhanceLogContent(content) {
        return content
            .replace(/(\[INFO\][^\n]*)/gi, '<span class="log-info">$1</span>')
            .replace(/(\[ERROR\][^\n]*)/gi, '<span class="log-error">$1</span>')
            .replace(/(\[WARNING\][^\n]*)/gi, '<span class="log-warning">$1</span>')
            .replace(/(\[DEBUG\][^\n]*)/gi, '<span class="log-debug">$1</span>')
            .replace(/(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}[^\s]*)/g, '<span class="timestamp">$1</span>');
    },

    // Enhance Terraform output
    enhanceTerraformOutput(content) {
        return content
            .replace(/(Plan:\s*\d+\s+to\s+add[^\n]*)/gi, '<span class="terraform-plan">$1</span>')
            .replace(/(Apply\s+complete![^\n]*)/gi, '<span class="terraform-success">$1</span>')
            .replace(/(resource\s+"[^"]+"\s+"[^"]+")[^\n]*/gi, '<span class="terraform-resource">$1</span>');
    },

    // Escape HTML characters
    escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },

    // Main function to make content clickable
    makeContentClickable(content) {
        if (!content || typeof content !== 'string') return content;
        
        console.log('🔍 Starting clickable content processing...');
        
        // Make Azure and Kubernetes resources clickable
        let processedContent = this.makeAllResourcesClickable(content);
        
        // Make Terraform deployment actions clickable
        processedContent = this.makeTerraformActionsClickable(processedContent);
        
        return processedContent;
    },

    // Check if content is static (welcome messages, etc.)
    isStaticContent(content) {
        const staticIndicators = [
            /welcome.*azure.*agent/i,
            /getting.*started/i,
            /available.*commands/i,
            /help.*information/i,
            /^(help|welcome|hello)/i,
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
        
        // Use dynamic detection for all resources
        return this.makeResourcesClickableSemanticApproach(content);
    },

    // Dynamic pattern-based approach: detect any Kubernetes resource names
    makeResourcesClickableSemanticApproach(content) {
        console.log('🎯 Using dynamic pattern detection for all Kubernetes resources');
        
        let modifiedContent = content;
        let replacementCount = 0;
        
        // Dynamic patterns for detecting ANY Kubernetes resource names
        const resourcePatterns = [
            // Standard deployment pods: name-hash-hash  
            { pattern: /\b[a-z][a-z0-9-]*-[a-z0-9]{8,10}-[a-z0-9]{5}\b/g, type: 'pod' },
            // DaemonSet pods: name-hash
            { pattern: /\b[a-z][a-z0-9-]*-[a-z0-9]{5}\b/g, type: 'pod' },
            // StatefulSet pods: name-number
            { pattern: /\b[a-z][a-z0-9-]*-[0-9]+\b/g, type: 'pod' },
            // Microsoft services (common in AKS)
            { pattern: /\bmicrosoft-[a-z0-9-]+/g, type: 'pod' },
            // Azure services
            { pattern: /\bazure-[a-z0-9-]+/g, type: 'pod' },
            // CoreDNS and system pods
            { pattern: /\b(coredns|kube-proxy|metrics-server|csi-azure)[a-z0-9-]*/g, type: 'pod' }
        ];
        
        console.log(`🔍 Scanning content with ${resourcePatterns.length} dynamic patterns`);
        
        // Process each pattern
        resourcePatterns.forEach((patternInfo, index) => {
            const matches = [...modifiedContent.matchAll(patternInfo.pattern)];
            
            matches.forEach(match => {
                const resourceName = match[0].trim();
                
                // Skip if already processed, too short, or common words
                if (resourceName.length < 3 || 
                    modifiedContent.includes(`data-resource-name="${resourceName}"`) ||
                    this.isCommonWord(resourceName)) {
                    return;
                }
                
                console.log(`✅ Pattern ${index + 1} found ${patternInfo.type}: '${resourceName}'`);
                
                // Create clickable span replacement
                const replacement = `<span class="clickable-resource" data-resource-name="${resourceName}" data-resource-type="${patternInfo.type}">${resourceName}</span>`;
                
                // Replace the exact match
                const escapedName = resourceName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
                const replacePattern = new RegExp(`\\b${escapedName}\\b`, 'g');
                
                const beforeReplace = modifiedContent;
                modifiedContent = modifiedContent.replace(replacePattern, replacement);
                
                if (modifiedContent !== beforeReplace) {
                    replacementCount++;
                    console.log(`🎯 Made '${resourceName}' clickable as ${patternInfo.type}`);
                }
            });
        });
        
        console.log(`📋 Dynamic pattern detection: processed ${resourcePatterns.length} patterns`);
        
        console.log(`🎯 Dynamic detection complete: ${replacementCount} resources made clickable`);
        return modifiedContent;
    },

    // Check if a word is a common English word (not a resource name)
    isCommonWord(word) {
        const commonWords = [
            'running', 'pending', 'failed', 'succeeded', 'unknown', 'ready', 
            'status', 'restarts', 'age', 'name', 'namespace', 'cluster', 
            'found', 'pods', 'containers', 'the', 'and', 'or', 'in', 'on', 
            'at', 'to', 'for', 'with', 'by', 'from', 'all', 'some', 'any',
            // HTML and technical terms that shouldn't be clickable
            'code-block', 'data-language', 'hcl', 'html', 'div', 'span', 'pre', 'code', 'br', 'strong', 'em', 'ul', 'li', 'ol', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
            'http', 'https', 'www', 'com', 'org', 'net', 'io', 'github', 'azure', 'microsoft', 'terraform', 'kubernetes',
            // Common terraform and Azure terms
            'resource', 'variable', 'output', 'module', 'provider', 'data', 'locals',
            'eastus', 'westus', 'centralus', 'northeurope', 'westeurope', 'dev', 'test', 'prod', 'staging', 'production-ready',
            // GitHub usernames and common identifiers
            'sachink-singh', 'azure-ai-agent'
        ];
        
        return commonWords.includes(word.toLowerCase());
    },

    // Check if text looks like a Kubernetes pod name
    looksLikeKubernetesPod(text) {
        if (!text || text.length < 3 || text.length > 63) return false;
        
        // Kubernetes naming rules: lowercase alphanumeric and hyphens
        if (!/^[a-z0-9-]+$/.test(text)) return false;
        
        // Must start and end with alphanumeric
        if (!/^[a-z0-9].*[a-z0-9]$/.test(text)) return false;
        
        // Common pod patterns
        const podPatterns = [
            /-[a-z0-9]{5}$/,           // DaemonSet: name-hash
            /-[a-z0-9]{8,10}-[a-z0-9]{5}$/, // Deployment: name-hash-hash
            /-\d+$/,                    // StatefulSet: name-number
            /^(azure|microsoft|kube|coredns|metrics)/  // Common prefixes
        ];
        
        return podPatterns.some(pattern => pattern.test(text));
    },

    // Validate if a resource name is valid for Azure/Kubernetes
    isValidResourceName(name, type = 'pod') {
        if (!name || typeof name !== 'string') return false;
        
        // Basic length check
        if (name.length < 2 || name.length > 63) return false;
        
        // Check for common words that aren't resource names
        if (this.isCommonWord(name)) return false;
        
        // Type-specific validation
        switch(type.toLowerCase()) {
            case 'pod':
                return this.looksLikeKubernetesPod(name);
            case 'service':
            case 'deployment':
            case 'namespace':
                return /^[a-z0-9-]+$/.test(name) && /^[a-z0-9].*[a-z0-9]$/.test(name);
            default:
                return true;
        }
    },

    // Make Terraform deployment actions clickable
    makeTerraformActionsClickable(content) {
        console.log('🎯 Processing Terraform deployment actions...');
        
        // Look for deployment ID pattern in terraform template responses
        // Try multiple patterns for deployment ID
        const deploymentIdPatterns = [
            /📁\s*\*\*Deployment ID:\*\*\s*`([^`]+)`/,
            /📁\s*<strong>Deployment ID:<\/strong>\s*<[^>]*>([^<]+)</,
            /Deployment ID.*?([a-z0-9-]{20,})/i,
            /aks-cluster-github-[\d-]+/
        ];
        
        let deploymentId = null;
        for (const pattern of deploymentIdPatterns) {
            const match = content.match(pattern);
            if (match) {
                deploymentId = match[1] || match[0];
                console.log(`✅ Found deployment ID with pattern: ${deploymentId}`);
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
