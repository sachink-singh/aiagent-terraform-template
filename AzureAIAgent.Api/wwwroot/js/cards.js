/**
 * Adaptive Cards Management
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.cards = {

    // Handle adaptive card actions
    handleAdaptiveCardAction(actionData) {
        console.log('🎯 Processing adaptive card action:', actionData);
        
        try {
            if (actionData.action === 'submit') {
                this.handleParameterSubmit(actionData);
            } else if (actionData.action === 'cancel') {
                this.handleParameterCancel(actionData);
            } else if (actionData.action === 'acceptTemplate') {
                this.handleAcceptTemplate(actionData);
            } else if (actionData.action === 'rejectTemplate') {
                this.handleRejectTemplate(actionData);
            } else if (actionData.action === 'modifyTemplate') {
                this.handleModifyTemplate(actionData);
            } else if (actionData.action === 'deployTemplate') {
                this.handleDeployTemplate(actionData);
            } else if (actionData.action === 'previewTemplate') {
                this.handlePreviewTemplate(actionData);
            } else {
                console.log('⚠️ Unknown adaptive card action:', actionData.action);
            }
        } catch (error) {
            console.error('❌ Error handling adaptive card action:', error);
            window.AzureAIAgent.ui.showNotification('Error processing card action', 'error');
        }
    },

    // Handle template acceptance
    handleAcceptTemplate(actionData) {
        console.log('✅ Template accepted:', actionData);
        
        // Send acceptance message
        const message = actionData.comment ? 
            `✅ Template accepted. ${actionData.comment}` : 
            '✅ Template accepted. Proceeding with deployment.';
        
        window.AzureAIAgent.chat.sendMessage(message);
        
        // Show deployment indicator
        window.AzureAIAgent.progress.showDeploymentLoading('Preparing deployment...');
        
        // Update Terraform context
        window.AzureAIAgent.terraform.updateContext('template_accepted', true);
    },

    // Handle template rejection
    handleRejectTemplate(actionData) {
        console.log('❌ Template rejected:', actionData);
        
        const message = actionData.comment ? 
            `❌ Template rejected. ${actionData.comment}` : 
            '❌ Template rejected. Please provide alternative requirements.';
        
        window.AzureAIAgent.chat.sendMessage(message);
        
        // Update Terraform context
        window.AzureAIAgent.terraform.updateContext('template_rejected', true);
        window.AzureAIAgent.terraform.updateContext('rejection_reason', actionData.comment || 'No reason provided');
    },

    // Handle template modification
    handleModifyTemplate(actionData) {
        console.log('✏️ Template modification requested:', actionData);
        
        let message = '✏️ Requesting template modifications:';
        
        // Build modification request
        const modifications = [];
        if (actionData.resourceGroup) modifications.push(`Resource Group: ${actionData.resourceGroup}`);
        if (actionData.clusterName) modifications.push(`Cluster Name: ${actionData.clusterName}`);
        if (actionData.nodeCount) modifications.push(`Node Count: ${actionData.nodeCount}`);
        if (actionData.vmSize) modifications.push(`VM Size: ${actionData.vmSize}`);
        if (actionData.region) modifications.push(`Region: ${actionData.region}`);
        if (actionData.kubernetesVersion) modifications.push(`Kubernetes Version: ${actionData.kubernetesVersion}`);
        if (actionData.comment) modifications.push(`Additional Notes: ${actionData.comment}`);
        
        if (modifications.length > 0) {
            message += '\n\n' + modifications.map(mod => `• ${mod}`).join('\n');
        }
        
        window.AzureAIAgent.chat.sendMessage(message);
        
        // Update Terraform context with modifications
        window.AzureAIAgent.terraform.updateContext('modifications_requested', modifications);
    },

    // Handle template deployment
    handleDeployTemplate(actionData) {
        console.log('🚀 Direct deployment requested:', actionData);
        
        const message = actionData.comment ? 
            `🚀 Deploying template directly. ${actionData.comment}` : 
            '🚀 Deploying template directly without further review.';
        
        window.AzureAIAgent.chat.sendMessage(message);
        
        // Show immediate deployment
        window.AzureAIAgent.progress.showDeploymentLoading('Deploying Azure resources...');
        
        // Update context
        window.AzureAIAgent.terraform.updateContext('direct_deployment', true);
    },

    // Handle template preview
    handlePreviewTemplate(actionData) {
        console.log('👁️ Template preview requested:', actionData);
        
        const message = actionData.comment ? 
            `👁️ Requesting template preview. ${actionData.comment}` : 
            '👁️ Please show me a detailed preview of the template before proceeding.';
        
        window.AzureAIAgent.chat.sendMessage(message);
        
        // Update context
        window.AzureAIAgent.terraform.updateContext('preview_requested', true);
    },

    // Create a simple confirmation card
    createConfirmationCard(title, message, confirmAction, cancelAction) {
        return {
            "type": "AdaptiveCard",
            "version": "1.3",
            "body": [
                {
                    "type": "TextBlock",
                    "text": title,
                    "weight": "bolder",
                    "size": "medium"
                },
                {
                    "type": "TextBlock",
                    "text": message,
                    "wrap": true
                }
            ],
            "actions": [
                {
                    "type": "Action.Submit",
                    "title": "Confirm",
                    "id": confirmAction,
                    "style": "positive"
                },
                {
                    "type": "Action.Submit",
                    "title": "Cancel",
                    "id": cancelAction,
                    "style": "destructive"
                }
            ]
        };
    },

    // Create deployment status card
    createDeploymentStatusCard(status, details) {
        const statusColors = {
            'success': 'good',
            'error': 'attention',
            'warning': 'warning',
            'info': 'default'
        };

        return {
            "type": "AdaptiveCard",
            "version": "1.3",
            "body": [
                {
                    "type": "TextBlock",
                    "text": "Deployment Status",
                    "weight": "bolder",
                    "size": "medium"
                },
                {
                    "type": "FactSet",
                    "facts": [
                        {
                            "title": "Status:",
                            "value": status
                        },
                        {
                            "title": "Details:",
                            "value": details
                        },
                        {
                            "title": "Time:",
                            "value": new Date().toLocaleTimeString()
                        }
                    ]
                }
            ]
        };
    },

    // Handle parameter form submission
    async handleParameterSubmit(actionData) {
        console.log('✅ Parameters submitted:', actionData);
        
        // Remove the action property and prepare parameters
        const { action, ...parameters } = actionData;
        
        // Build the exact message format that backend expects for structured form submission
        const parameterPairs = Object.entries(parameters)
            .filter(([key, value]) => value && value.trim())
            .map(([key, value]) => `${key}=${value}`);
        
        const message = `Create AKS cluster: ${parameterPairs.join(', ')}`;
        
        console.log('📤 Sending correctly formatted message:', message);
        
        try {
            // Send the message and await the response
            const result = await window.AzureAIAgent.chat.sendMessage(message);
            
            console.log('📥 Form submission response received:', result);
            
            // Hide typing indicator
            window.AzureAIAgent.ui.hideTyping();
            
            // Process the response
            if (result.contentType === 'adaptive-card' && result.adaptiveCard) {
                console.log('🃏 Handling adaptive card response from form submission');
                window.AzureAIAgent.ui.addAdaptiveCardMessage(result.message, result.adaptiveCard);
            } else if (result && result.message) {
                console.log('📝 Handling text message response from form submission');
                window.AzureAIAgent.ui.addMessage('assistant', result.message);
            } else {
                console.log('⚠️ No message or card data in form submission response');
            }
        } catch (error) {
            console.error('❌ Error in form submission:', error);
            window.AzureAIAgent.ui.hideTyping();
            window.AzureAIAgent.ui.addMessage('assistant', 
                '❌ **Error processing form**: ' + error.message
            );
        }
    },

    // Handle parameter form cancellation
    handleParameterCancel(actionData) {
        console.log('❌ Parameter form cancelled');
        
        // Show cancellation message
        window.AzureAIAgent.ui.addMessage('assistant', 
            '❌ **AKS Cluster Creation Cancelled**\n\nNo worries! You can start over anytime by saying "Create an AKS cluster" again.'
        );
    }
};
