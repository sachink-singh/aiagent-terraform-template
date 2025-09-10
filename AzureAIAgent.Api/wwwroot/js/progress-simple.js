/**
 * Simple Real Terraform Output Display
 * Shows actual terraform logs directly without simulation
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.progress = {

    deploymentId: null,
    pollInterval: null,

    // Start deployment with real terraform output tracking
    showDeploymentLoading(message = 'Starting deployment...') {
        console.log('🚀 Starting real terraform deployment tracking');
        
        // Show status
        const statusElement = document.getElementById('deploymentStatus');
        const statusText = document.getElementById('deploymentStatusText');
        
        if (statusElement && statusText) {
            statusElement.classList.add('visible');
            statusText.textContent = message;
        }

        // Show terraform output block
        const terraformOutput = document.getElementById('terraformLiveOutput');
        if (terraformOutput) {
            terraformOutput.style.display = 'block';
        }

        // Clear previous output
        const outputPre = document.getElementById('terraformOutputPre');
        if (outputPre) {
            outputPre.textContent = '';
        }
    },

    // Start polling for real terraform output
    startPolling(deploymentId) {
        this.deploymentId = deploymentId;
        console.log(`🔄 Starting to poll for deployment: ${deploymentId}`);
        
        this.pollInterval = setInterval(() => {
            this.pollProgress();
        }, 1000); // Poll every second for real-time output
    },

    // Poll for actual terraform progress
    async pollProgress() {
        if (!this.deploymentId) return;

        try {
            const response = await fetch(`/api/azure/deployment-status/${this.deploymentId}`);
            if (response.ok) {
                const status = await response.json();
                console.log('📊 Progress status:', status);
                this.updateFromRealStatus(status);
            }
        } catch (error) {
            console.error('❌ Error polling progress:', error);
        }
    },

    // Update UI with real terraform output
    updateFromRealStatus(status) {
        // Update status text
        if (status.message) {
            const statusText = document.getElementById('deploymentStatusText');
            if (statusText) {
                statusText.textContent = status.message;
            }
        }

        // Update current terraform command
        if (status.currentTerraformCommand) {
            const commandElement = document.getElementById('terraformCurrentCommand');
            if (commandElement) {
                commandElement.textContent = status.currentTerraformCommand;
            }
        }

        // Display real terraform output
        if (status.terraformOutput && status.terraformOutput.length > 0) {
            this.displayRealTerraformOutput(status.terraformOutput);
            
            // Check for success patterns in terraform output
            const fullOutput = status.terraformOutput.join('\n').toLowerCase();
            if (this.isDeploymentSuccessful(fullOutput)) {
                console.log('✅ Success detected in terraform output');
                this.handleSuccess('Deployment completed successfully!');
                return;
            }
        }

        // Handle completion based on status
        if (status.status === 'Completed') {
            this.handleSuccess(status.message || 'Deployment completed successfully!');
        } else if (status.status === 'Failed') {
            this.handleError(status.errorMessage || 'Deployment failed');
        }
    },

    // Display actual terraform output without simulation
    displayRealTerraformOutput(outputLines) {
        const outputPre = document.getElementById('terraformOutputPre');
        if (!outputPre) return;

        console.log('📟 Displaying real terraform output:', outputLines.length, 'lines');

        // Join all output lines with newlines to show complete terraform log
        const fullOutput = outputLines.join('\n');
        outputPre.textContent = fullOutput;

        // Auto-scroll to bottom to show latest output
        const terraformConsole = document.getElementById('terraformConsole');
        if (terraformConsole) {
            terraformConsole.scrollTop = terraformConsole.scrollHeight;
        }
    },

    // Check if deployment was successful based on terraform output
    isDeploymentSuccessful(terraformOutput) {
        const successPatterns = [
            'apply complete!',
            'deployment completed successfully',
            'resources: \\d+ added, \\d+ changed, \\d+ destroyed', // handles both no changes and changes
            'terraform apply completed',
            'deployment successful',
            'infrastructure deployed successfully',
            'resources created successfully'
        ];
        
        return successPatterns.some(pattern => {
            const regex = new RegExp(pattern, 'i');
            return regex.test(terraformOutput);
        });
    },

    // Handle successful deployment
    handleSuccess(message) {
        console.log('✅ Deployment successful:', message);
        this.stopPolling();
        
        const statusText = document.getElementById('deploymentStatusText');
        if (statusText) {
            statusText.textContent = message;
            statusText.style.color = '#16c60c';
        }

        // Keep terraform output visible for review
        setTimeout(() => {
            this.hideStatus();
        }, 10000); // Hide after 10 seconds
    },

    // Handle deployment error
    handleError(error) {
        console.error('❌ Deployment failed:', error);
        this.stopPolling();
        
        const statusText = document.getElementById('deploymentStatusText');
        if (statusText) {
            statusText.textContent = `Error: ${error}`;
            statusText.style.color = '#d83b01';
        }

        // Keep output visible for debugging
    },

    // Stop polling
    stopPolling() {
        if (this.pollInterval) {
            clearInterval(this.pollInterval);
            this.pollInterval = null;
        }
    },

    // Hide deployment status
    hideStatus() {
        const statusElement = document.getElementById('deploymentStatus');
        if (statusElement) {
            statusElement.classList.remove('visible');
        }

        const terraformOutput = document.getElementById('terraformLiveOutput');
        if (terraformOutput) {
            terraformOutput.style.display = 'none';
        }
    },

    // Hide any loading (compatibility with existing code)
    hideDeploymentLoading() {
        this.hideStatus();
        this.stopPolling();
    },

    // Compatibility method for existing event handlers
    trackDeploymentProgress(deploymentId) {
        console.log('🔄 trackDeploymentProgress called with:', deploymentId);
        this.startPolling(deploymentId);
    },

    // Compatibility method for error handling
    handleDeploymentError(error) {
        console.error('❌ handleDeploymentError called:', error);
        this.handleError(error);
    },

    // Compatibility method for adding log entries
    addLogEntry(message) {
        console.log('📝 addLogEntry called:', message);
        // In the simplified version, we just log to console
        // The real terraform output is displayed directly
    },

    // Compatibility method for updating deployment message
    updateDeploymentMessage(message) {
        console.log('📝 updateDeploymentMessage called:', message);
        const statusText = document.getElementById('deploymentStatusText');
        if (statusText) {
            statusText.textContent = message;
        }
    }
};

console.log('✅ Simple terraform progress tracking loaded');
