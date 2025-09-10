/**
 * Modern Progress Tracking and Deployment Management
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.progress = {

    // Show deployment loading indicator - Modern modal style
    showDeploymentLoading(message = 'Initializing deployment...') {
        // Remove any existing progress indicator
        this.hideDeploymentLoading();
        
        // Create backdrop
        const backdrop = document.createElement('div');
        backdrop.className = 'deployment-modal-backdrop';
        
        // Create modal container
        const modal = document.createElement('div');
        modal.className = 'deployment-modal';
        
        modal.innerHTML = `
            <div class="deployment-modal-content">
                <div class="deployment-modal-header">
                    <div class="deployment-modal-icon">🚀</div>
                    <h3 class="deployment-modal-title">Terraform Deployment</h3>
                    <div class="deployment-status-badge">In Progress</div>
                </div>
                
                <div class="deployment-progress-section">
                    <div class="deployment-message">${message}</div>
                    <div class="deployment-progress-bar-container">
                        <div class="deployment-progress-bar">
                            <div class="deployment-progress-fill"></div>
                        </div>
                    </div>
                </div>
                
                <div class="deployment-steps">
                    <div class="deployment-step active" data-step="init">
                        <div class="step-circle">
                            <span class="step-number">1</span>
                            <div class="step-spinner"></div>
                        </div>
                        <div class="step-content">
                            <div class="step-title">Initialize</div>
                            <div class="step-description">Preparing Terraform</div>
                        </div>
                    </div>
                    <div class="deployment-step" data-step="plan">
                        <div class="step-circle">
                            <span class="step-number">2</span>
                            <div class="step-spinner"></div>
                        </div>
                        <div class="step-content">
                            <div class="step-title">Plan</div>
                            <div class="step-description">Analyzing changes</div>
                        </div>
                    </div>
                    <div class="deployment-step" data-step="apply">
                        <div class="step-circle">
                            <span class="step-number">3</span>
                            <div class="step-spinner"></div>
                        </div>
                        <div class="step-content">
                            <div class="step-title">Apply</div>
                            <div class="step-description">Creating resources</div>
                        </div>
                    </div>
                    <div class="deployment-step" data-step="complete">
                        <div class="step-circle">
                            <span class="step-number">4</span>
                            <div class="step-spinner"></div>
                        </div>
                        <div class="step-content">
                            <div class="step-title">Complete</div>
                            <div class="step-description">Deployment finished</div>
                        </div>
                    </div>
                </div>
                
                <div class="deployment-logs">
                    <div class="logs-header">
                        <span>📋 Deployment Logs</span>
                        <button class="logs-toggle" onclick="this.closest('.deployment-logs').classList.toggle('expanded')">
                            <span>▼</span>
                        </button>
                    </div>
                    <div class="logs-content">
                        <div class="log-entry">Starting Terraform deployment...</div>
                    </div>
                </div>
            </div>
        `;
        
        backdrop.appendChild(modal);
        document.body.appendChild(backdrop);
        
        // Show with animation
        setTimeout(() => {
            backdrop.classList.add('show');
        }, 10);
        
        // Start the progress animation
        this.startProgressAnimation();
        
        console.log('� Modern deployment progress modal shown:', message);
        
        return { backdrop, modal };
    },

    // Update deployment message
    updateDeploymentMessage(message) {
        const messageElement = document.querySelector('.deployment-message');
        if (messageElement) {
            messageElement.textContent = message;
            console.log('💬 Deployment message updated:', message);
        }
    },

    // Hide deployment loading indicator
    hideDeploymentLoading() {
        const backdrop = document.querySelector('.deployment-modal-backdrop');
        if (backdrop) {
            backdrop.classList.remove('show');
            setTimeout(() => {
                if (backdrop.parentNode) {
                    backdrop.parentNode.removeChild(backdrop);
                }
            }, 300);
            
            console.log('✅ Deployment progress modal hidden');
        }
    },

    // Start progress animation
    startProgressAnimation() {
        const steps = document.querySelectorAll('.deployment-step');
        let currentStep = 0;
        let progress = 0;
        
        const advanceStep = () => {
            if (currentStep < steps.length) {
                // Mark current step as active
                steps[currentStep].classList.add('active');
                steps[currentStep].classList.add('processing');
                
                // Update progress bar
                progress = ((currentStep + 1) / steps.length) * 100;
                const progressFill = document.querySelector('.deployment-progress-fill');
                if (progressFill) {
                    progressFill.style.width = `${progress}%`;
                }
                
                // Add log entry
                this.addLogEntry(`Step ${currentStep + 1}: ${steps[currentStep].querySelector('.step-title').textContent}`);
                
                currentStep++;
                
                if (currentStep < steps.length) {
                    setTimeout(advanceStep, 3000); // 3 seconds per step
                }
            }
        };
        
        // Start animation after a delay
        setTimeout(advanceStep, 1000);
    },

    // Add log entry to deployment logs
    addLogEntry(message) {
        const logsContent = document.querySelector('.logs-content');
        if (logsContent) {
            const logEntry = document.createElement('div');
            logEntry.className = 'log-entry';
            logEntry.textContent = `${new Date().toLocaleTimeString()}: ${message}`;
            logsContent.appendChild(logEntry);
            
            // Scroll to bottom
            logsContent.scrollTop = logsContent.scrollHeight;
        }
    },

    // Show deployment status
    showDeploymentStatus(status, message, details = null) {
        const statusDiv = document.createElement('div');
        statusDiv.className = `deployment-status deployment-status-${status}`;
        
        let statusIcon = '📋';
        switch (status) {
            case 'success': statusIcon = '✅'; break;
            case 'error': statusIcon = '❌'; break;
            case 'warning': statusIcon = '⚠️'; break;
            case 'info': statusIcon = 'ℹ️'; break;
        }
        
        statusDiv.innerHTML = `
            <div class="deployment-status-content">
                <div class="status-header">
                    <span class="status-icon">${statusIcon}</span>
                    <span class="status-message">${message}</span>
                </div>
                ${details ? `<div class="status-details">${details}</div>` : ''}
            </div>
        `;
        
        document.body.appendChild(statusDiv);
        
        setTimeout(() => {
            statusDiv.classList.add('show');
        }, 10);
        
        // Auto-hide after delay
        setTimeout(() => {
            statusDiv.classList.remove('show');
            setTimeout(() => {
                if (statusDiv.parentNode) {
                    statusDiv.parentNode.removeChild(statusDiv);
                }
            }, 300);
        }, 5000);
        
        // Update dashboard
        window.AzureAIAgent.dashboard.updateLastOperation(status, message);
        
        console.log(`📊 Deployment status shown: ${status} - ${message}`);
    },

    // Get current deployment status
    getCurrentDeploymentStatus() {
        return window.AzureAIAgent.terraform.getContext('current_progress');
    },

    // Show mini progress indicator for quick operations
    showMiniProgress(message) {
        const existing = document.querySelector('.mini-progress');
        if (existing) {
            existing.remove();
        }
        
        const miniProgress = document.createElement('div');
        miniProgress.className = 'mini-progress';
        miniProgress.innerHTML = `
            <div class="mini-progress-content">
                <div class="mini-spinner"></div>
                <span>${message}</span>
            </div>
        `;
        
        document.body.appendChild(miniProgress);
        
        setTimeout(() => {
            miniProgress.classList.add('show');
        }, 10);
        
        return miniProgress;
    },

    // Hide mini progress indicator
    hideMiniProgress() {
        const miniProgress = document.querySelector('.mini-progress');
        if (miniProgress) {
            miniProgress.classList.remove('show');
            setTimeout(() => {
                if (miniProgress.parentNode) {
                    miniProgress.parentNode.removeChild(miniProgress);
                }
            }, 300);
        }
    },

    // Track deployment progress with real backend status
    trackDeploymentProgress(deploymentId) {
        // Only track actual deployment IDs, not application events
        if (!deploymentId || deploymentId === 'Application Started' || deploymentId.startsWith('Application ')) {
            console.log('⏭️ Skipping progress tracking for application event:', deploymentId);
            return;
        }

        console.log('🔄 Starting deployment progress tracking for:', deploymentId);
        
        let pollCount = 0;
        const maxPolls = 150; // 5 minutes max (150 * 2 seconds)
        
        const checkProgress = async () => {
            try {
                pollCount++;
                console.log(`📊 Polling deployment status (${pollCount}/${maxPolls}):`, deploymentId);
                
                const response = await fetch(`/api/azure/deployment-status/${deploymentId}`);
                if (response.ok) {
                    const status = await response.json();
                    console.log('📥 Deployment status received:', status);
                    this.updateProgressFromStatus(status);
                    
                    // Continue polling if deployment is still in progress
                    if ((status.status === 'InProgress' || status.status === 'Pending') && pollCount < maxPolls) {
                        setTimeout(checkProgress, 2000); // Poll every 2 seconds
                    } else if (status.status === 'Completed') {
                        this.handleDeploymentComplete(status);
                    } else if (status.status === 'Failed') {
                        this.handleDeploymentFailed(status);
                    } else if (pollCount >= maxPolls) {
                        console.log('⏰ Max polling attempts reached, stopping');
                        this.addLogEntry('Polling timeout reached. Check deployment status manually.');
                    }
                } else if (response.status === 404) {
                    console.log('📋 Deployment not found in cache yet, will retry...');
                    if (pollCount < 10) { // Give it 20 seconds to appear in cache
                        setTimeout(checkProgress, 2000);
                    } else {
                        console.log('❌ Deployment ID not found after retries');
                        this.addLogEntry('Deployment tracking failed: ID not found in system');
                    }
                } else {
                    console.error('❌ Failed to fetch deployment status:', response.status);
                    this.addLogEntry(`Error fetching status: ${response.status}`);
                }
            } catch (error) {
                console.error('❌ Error tracking deployment progress:', error);
                this.addLogEntry(`Error tracking deployment: ${error.message}`);
            }
        };
        
        // Start polling immediately
        checkProgress();
    },

    // Handle deployment completion
    handleDeploymentComplete(status) {
        this.addLogEntry('✅ Deployment completed successfully!');
        this.updateDeploymentMessage('Deployment completed successfully!');
        
        // Update status badge
        const statusBadge = document.querySelector('.deployment-status-badge');
        if (statusBadge) {
            statusBadge.textContent = 'Completed';
            statusBadge.style.background = 'rgba(46, 160, 67, 0.2)';
        }
        
        // Hide modal after a delay
        setTimeout(() => {
            this.hideDeploymentLoading();
        }, 3000);
    },

    // Handle deployment failure
    handleDeploymentFailed(status) {
        this.addLogEntry('❌ Deployment failed');
        this.updateDeploymentMessage('Deployment failed');
        
        // Update status badge
        const statusBadge = document.querySelector('.deployment-status-badge');
        if (statusBadge) {
            statusBadge.textContent = 'Failed';
            statusBadge.style.background = 'rgba(218, 54, 51, 0.2)';
        }
        
        // Show error state
        const steps = document.querySelectorAll('.deployment-step');
        steps.forEach(step => {
            if (step.classList.contains('active')) {
                step.classList.add('error');
            }
        });
        
        // Don't auto-hide, let user see the error
    },

    // Update progress UI from backend status
    updateProgressFromStatus(status) {
        // Update message
        if (status.message) {
            this.updateDeploymentMessage(status.message);
        }
        
        // Update progress bar
        if (status.progress !== undefined) {
            const progressFill = document.querySelector('.deployment-progress-fill');
            if (progressFill) {
                progressFill.style.width = `${status.progress}%`;
            }
        }
        
        // Update terraform output in real-time
        if (status.terraformOutput && status.terraformOutput.length > 0) {
            this.updateTerraformOutput(status.terraformOutput, status.currentTerraformCommand);
        }
        
        // Update steps based on terraform phase
        if (status.terraformPhase) {
            this.updateStepsFromTerraformPhase(status.terraformPhase);
        }
        
        // Add log entry
        if (status.detailedMessage) {
            this.addLogEntry(status.detailedMessage);
        }
        
        // Handle completion
        if (status.status === 'Completed') {
            this.handleDeploymentSuccess('Deployment completed successfully!');
        } else if (status.status === 'Failed') {
            this.handleDeploymentError(status.errorMessage || 'Deployment failed');
        }
    },

    // Update terraform output in real-time code block
    updateTerraformOutput(outputLines, currentCommand) {
        let outputContainer = document.querySelector('.terraform-output-container');
        
        // Create terraform output container if it doesn't exist
        if (!outputContainer) {
            const modal = document.querySelector('.deployment-progress-modal');
            if (modal) {
                outputContainer = document.createElement('div');
                outputContainer.className = 'terraform-output-container';
                outputContainer.innerHTML = `
                    <div class="terraform-output-header">
                        <span class="terraform-output-title">🖥️ Terraform Output</span>
                        <span class="terraform-current-command">${currentCommand || ''}</span>
                    </div>
                    <div class="terraform-output-content">
                        <pre class="terraform-output-code"></pre>
                    </div>
                `;
                
                // Add CSS styles
                outputContainer.style.cssText = `
                    margin-top: 20px;
                    border: 1px solid #444;
                    border-radius: 8px;
                    background: #1a1a1a;
                    overflow: hidden;
                `;
                
                const header = outputContainer.querySelector('.terraform-output-header');
                header.style.cssText = `
                    padding: 12px 16px;
                    background: #2d2d2d;
                    color: #ffffff;
                    font-weight: 600;
                    border-bottom: 1px solid #444;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                `;
                
                const content = outputContainer.querySelector('.terraform-output-content');
                content.style.cssText = `
                    max-height: 300px;
                    overflow-y: auto;
                    padding: 0;
                `;
                
                const codeBlock = outputContainer.querySelector('.terraform-output-code');
                codeBlock.style.cssText = `
                    margin: 0;
                    padding: 16px;
                    background: #1a1a1a;
                    color: #00ff00;
                    font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
                    font-size: 12px;
                    line-height: 1.4;
                    white-space: pre-wrap;
                    word-break: break-all;
                `;
                
                // Insert before the actions div
                const actionsDiv = modal.querySelector('.deployment-actions');
                if (actionsDiv) {
                    modal.insertBefore(outputContainer, actionsDiv);
                } else {
                    modal.appendChild(outputContainer);
                }
            }
        }
        
        // Update current command
        if (currentCommand) {
            const commandSpan = outputContainer.querySelector('.terraform-current-command');
            if (commandSpan) {
                commandSpan.textContent = currentCommand;
            }
        }
        
        // Update output content
        const codeBlock = outputContainer.querySelector('.terraform-output-code');
        if (codeBlock && outputLines) {
            // Show last 100 lines to prevent UI lag
            const recentLines = outputLines.slice(-100);
            codeBlock.textContent = recentLines.join('\n');
            
            // Auto-scroll to bottom
            const contentDiv = outputContainer.querySelector('.terraform-output-content');
            if (contentDiv) {
                contentDiv.scrollTop = contentDiv.scrollHeight;
            }
        }
    },

    // Update steps based on terraform phase
    updateStepsFromTerraformPhase(phase) {
        const stepMapping = {
            'initializing': 0,
            'downloading': 0,
            'preparing': 1,
            'planning': 1,
            'applying': 2,
            'provisioning': 2,
            'finalizing': 3,
            'completed': 4,
            'error': -1
        };
        
        const stepIndex = stepMapping[phase] || 0;
        this.updateActiveStep(stepIndex);
    },

    // Update steps based on current operation
    updateStepsFromOperation(operation) {
        const stepMap = {
            'terraform init': 'init',
            'terraform plan': 'plan', 
            'terraform apply': 'apply',
            'deployment complete': 'complete'
        };
        
        const currentStep = stepMap[operation?.toLowerCase()] || 'init';
        const steps = document.querySelectorAll('.deployment-step');
        
        steps.forEach(step => {
            const stepName = step.dataset.step;
            if (stepName === currentStep) {
                step.classList.add('active', 'processing');
            } else if (this.isStepBefore(stepName, currentStep)) {
                step.classList.add('completed');
                step.classList.remove('active', 'processing');
            }
        });
    },

    // Check if step comes before current step
    isStepBefore(stepName, currentStep) {
        const stepOrder = ['init', 'plan', 'apply', 'complete'];
        return stepOrder.indexOf(stepName) < stepOrder.indexOf(currentStep);
    },

    // Handle deployment success
    handleDeploymentSuccess(message) {
        setTimeout(() => {
            this.hideDeploymentLoading();
            this.showDeploymentStatus('success', 'Deployment completed successfully!', message);
        }, 1000);
    },

    // Handle deployment error
    handleDeploymentError(message) {
        setTimeout(() => {
            this.hideDeploymentLoading();
            this.showDeploymentStatus('error', 'Deployment failed', message);
        }, 1000);
    }
};
