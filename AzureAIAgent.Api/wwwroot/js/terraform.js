/**
 * Terraform Context and Template Management
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.terraform = {

    // Update Terraform context
    updateContext(key, value) {
        if (window.AzureAIAgent.config.terraformContext) {
            window.AzureAIAgent.config.terraformContext[key] = value;
            console.log(`🏗️ Terraform context updated: ${key} = ${value}`);
            
            // Persist to sessionStorage
            try {
                sessionStorage.setItem('terraformContext', JSON.stringify(window.AzureAIAgent.config.terraformContext));
            } catch (error) {
                console.warn('⚠️ Could not persist Terraform context to sessionStorage:', error);
            }
            
            // Update dashboard if relevant
            if (['template_accepted', 'template_rejected', 'direct_deployment', 'preview_requested'].includes(key)) {
                window.AzureAIAgent.dashboard.updateTerraformStatus(key, value);
            }
        }
    },

    // Get Terraform context value
    getContext(key) {
        return window.AzureAIAgent.config.terraformContext ? window.AzureAIAgent.config.terraformContext[key] : null;
    },

    // Clear Terraform context
    clearContext() {
        window.AzureAIAgent.config.terraformContext = {};
        
        try {
            sessionStorage.removeItem('terraformContext');
        } catch (error) {
            console.warn('⚠️ Could not clear Terraform context from sessionStorage:', error);
        }
        
        console.log('🧹 Terraform context cleared');
    },

    // Load Terraform context from sessionStorage
    loadContext() {
        try {
            const stored = sessionStorage.getItem('terraformContext');
            if (stored) {
                window.AzureAIAgent.config.terraformContext = JSON.parse(stored);
                console.log('📥 Terraform context loaded from sessionStorage');
            }
        } catch (error) {
            console.warn('⚠️ Could not load Terraform context from sessionStorage:', error);
            window.AzureAIAgent.config.terraformContext = {};
        }
    },

    // Track template operations
    trackTemplateOperation(operation, details = {}) {
        const timestamp = new Date().toISOString();
        const operationData = {
            operation,
            timestamp,
            details,
            sessionId: window.AzureAIAgent.config.SESSION_ID
        };

        // Store in context
        this.updateContext(`operation_${timestamp}`, operationData);

        // Update dashboard counters
        if (operation === 'template_generated') {
            window.AzureAIAgent.dashboard.incrementCounter('templates');
        } else if (operation === 'deployment_started') {
            window.AzureAIAgent.dashboard.incrementCounter('deployments');
        } else if (operation === 'deployment_completed') {
            window.AzureAIAgent.dashboard.incrementCounter('successful_deployments');
        }

        console.log('📊 Template operation tracked:', operationData);
    },

    // Get deployment history
    getDeploymentHistory() {
        const context = window.AzureAIAgent.config.terraformContext;
        const operations = [];

        for (const [key, value] of Object.entries(context)) {
            if (key.startsWith('operation_') && value.operation) {
                operations.push(value);
            }
        }

        return operations.sort((a, b) => new Date(b.timestamp) - new Date(a.timestamp));
    },

    // Check if template is ready for deployment
    isTemplateReady() {
        const accepted = this.getContext('template_accepted');
        const rejected = this.getContext('template_rejected');
        const directDeploy = this.getContext('direct_deployment');

        return (accepted === true || directDeploy === true) && rejected !== true;
    },

    // Get template status summary
    getTemplateStatus() {
        const context = window.AzureAIAgent.config.terraformContext;
        
        return {
            accepted: context.template_accepted === true,
            rejected: context.template_rejected === true,
            directDeployment: context.direct_deployment === true,
            previewRequested: context.preview_requested === true,
            modificationsRequested: !!context.modifications_requested,
            rejectionReason: context.rejection_reason,
            modifications: context.modifications_requested
        };
    },

    // Reset template status
    resetTemplateStatus() {
        const keysToReset = [
            'template_accepted',
            'template_rejected',
            'direct_deployment',
            'preview_requested',
            'modifications_requested',
            'rejection_reason'
        ];

        keysToReset.forEach(key => {
            this.updateContext(key, null);
        });

        console.log('🔄 Template status reset');
    },

    // Validate template parameters
    validateTemplateParameters(parameters) {
        const errors = [];
        const warnings = [];

        // Required parameters check
        const required = ['resourceGroup', 'clusterName', 'region'];
        required.forEach(param => {
            if (!parameters[param] || parameters[param].trim() === '') {
                errors.push(`Missing required parameter: ${param}`);
            }
        });

        // Parameter format validation
        if (parameters.clusterName && !/^[a-zA-Z][a-zA-Z0-9-]*$/.test(parameters.clusterName)) {
            errors.push('Cluster name must start with a letter and contain only letters, numbers, and hyphens');
        }

        if (parameters.nodeCount && (isNaN(parameters.nodeCount) || parameters.nodeCount < 1 || parameters.nodeCount > 100)) {
            warnings.push('Node count should be between 1 and 100');
        }

        if (parameters.vmSize && !parameters.vmSize.startsWith('Standard_')) {
            warnings.push('VM Size should follow Azure naming convention (e.g., Standard_D2s_v3)');
        }

        return { errors, warnings };
    },

    // Generate template summary
    generateTemplateSummary(parameters) {
        const summary = [];
        
        if (parameters.resourceGroup) summary.push(`Resource Group: ${parameters.resourceGroup}`);
        if (parameters.clusterName) summary.push(`Cluster Name: ${parameters.clusterName}`);
        if (parameters.region) summary.push(`Region: ${parameters.region}`);
        if (parameters.nodeCount) summary.push(`Node Count: ${parameters.nodeCount}`);
        if (parameters.vmSize) summary.push(`VM Size: ${parameters.vmSize}`);
        if (parameters.kubernetesVersion) summary.push(`Kubernetes Version: ${parameters.kubernetesVersion}`);

        return summary.join('\n');
    }
};
