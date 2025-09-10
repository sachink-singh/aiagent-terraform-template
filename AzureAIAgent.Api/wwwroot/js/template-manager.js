// Template Management Integration for Web UI
// This demonstrates how the Adaptive Cards and GitHub template system integrates with the Web API

class TemplateManager {
    constructor(baseUrl = '/api') {
        this.baseUrl = baseUrl;
        this.currentSessionId = this.generateSessionId();
        this.adaptiveCardRenderer = new AdaptiveCards.AdaptiveCard();
    }

    generateSessionId() {
        return 'session_' + Math.random().toString(36).substr(2, 9);
    }

    // 1. Show Template Gallery - Display available templates as interactive cards
    async showTemplateGallery(category = null) {
        try {
            const response = await fetch(`${this.baseUrl}/templates/gallery${category ? `?category=${category}` : ''}`);
            const data = await response.json();

            if (data.success) {
                // Parse and render the Adaptive Card
                const card = JSON.parse(data.adaptiveCard);
                this.renderAdaptiveCard(card, 'template-gallery-container');

                // Store template metadata for later use
                this.availableTemplates = data.templates;

                console.log('Template Gallery loaded:', data.templates.length, 'templates');
                return data.templates;
            } else {
                throw new Error(data.error || 'Failed to load template gallery');
            }
        } catch (error) {
            console.error('Error loading template gallery:', error);
            this.showError('Failed to load templates. Please try again.');
        }
    }

    // 2. Show Parameter Form - Display input form for selected template
    async showTemplateParameterForm(templateId) {
        try {
            const response = await fetch(`${this.baseUrl}/templates/${templateId}/form`);
            const data = await response.json();

            if (data.success) {
                // Parse and render the Adaptive Card form
                const card = JSON.parse(data.adaptiveCard);
                this.renderAdaptiveCard(card, 'parameter-form-container');

                // Store template info for deployment
                this.selectedTemplate = data.template;

                console.log('Parameter form loaded for:', data.template.name);
                return data.template;
            } else {
                throw new Error(data.error || 'Template not found');
            }
        } catch (error) {
            console.error('Error loading parameter form:', error);
            this.showError('Failed to load template form. Please try again.');
        }
    }

    // 3. Deploy Template - Submit form and start deployment
    async deployTemplate(templateId, parameters) {
        try {
            const response = await fetch(`${this.baseUrl}/templates/deploy`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    templateId: templateId,
                    parameters: parameters,
                    sessionId: this.currentSessionId
                })
            });

            const data = await response.json();

            if (data.success) {
                // Show deployment status card
                const statusCard = JSON.parse(data.adaptiveCard);
                this.renderAdaptiveCard(statusCard, 'deployment-status-container');

                // Start polling for status updates
                this.startStatusPolling(data.deploymentId);

                console.log('Deployment started:', data.deploymentId);
                return data.deploymentId;
            } else {
                throw new Error(data.error || 'Deployment failed');
            }
        } catch (error) {
            console.error('Error starting deployment:', error);
            this.showError('Failed to start deployment. Please try again.');
        }
    }

    // 4. Poll Deployment Status - Monitor deployment progress
    async startStatusPolling(deploymentId) {
        const pollInterval = 3000; // Poll every 3 seconds
        let pollCount = 0;
        const maxPolls = 100; // Maximum 5 minutes of polling

        const poll = async () => {
            try {
                const response = await fetch(`${this.baseUrl}/templates/deployments/${deploymentId}/status`);
                const data = await response.json();

                if (data.success) {
                    // Update status card
                    const statusCard = JSON.parse(data.adaptiveCard);
                    this.renderAdaptiveCard(statusCard, 'deployment-status-container');

                    console.log('Deployment status:', data.status);

                    // Stop polling if completed or failed
                    if (data.status === 'Completed' || data.status === 'Failed') {
                        console.log('Deployment finished:', data.status);
                        this.onDeploymentComplete(deploymentId, data.status);
                        return;
                    }
                }

                // Continue polling if not finished and under limit
                pollCount++;
                if (pollCount < maxPolls) {
                    setTimeout(poll, pollInterval);
                } else {
                    console.warn('Status polling timeout reached');
                }
            } catch (error) {
                console.error('Error polling deployment status:', error);
            }
        };

        poll();
    }

    // 5. Render Adaptive Card - Display cards in the UI
    renderAdaptiveCard(cardPayload, containerId) {
        try {
            const container = document.getElementById(containerId);
            if (!container) {
                console.error('Container not found:', containerId);
                return;
            }

            // Clear previous content
            container.innerHTML = '';

            // Create adaptive card
            const adaptiveCard = new AdaptiveCards.AdaptiveCard();
            
            // Set up action handling
            adaptiveCard.onExecuteAction = (action) => {
                this.handleAdaptiveCardAction(action);
            };

            // Parse and render
            adaptiveCard.parse(cardPayload);
            const renderedCard = adaptiveCard.render();

            if (renderedCard) {
                container.appendChild(renderedCard);
            } else {
                throw new Error('Failed to render adaptive card');
            }
        } catch (error) {
            console.error('Error rendering adaptive card:', error);
            this.showError('Failed to display content. Please refresh the page.');
        }
    }

    // 6. Handle Adaptive Card Actions - Process user interactions
    handleAdaptiveCardAction(action) {
        console.log('Adaptive card action:', action);

        const actionData = action.data;

        switch (actionData.action) {
            case 'select_template':
                this.showTemplateParameterForm(actionData.templateId);
                break;

            case 'deploy_template':
                const parameters = this.extractFormParameters(action);
                this.deployTemplate(actionData.templateId, parameters);
                break;

            case 'show_gallery':
                this.showTemplateGallery();
                break;

            default:
                console.warn('Unknown action:', actionData.action);
        }
    }

    // 7. Extract Form Parameters - Get user inputs from form
    extractFormParameters(action) {
        const parameters = {};
        
        // Extract all input values from the form
        if (this.selectedTemplate && this.selectedTemplate.parameters) {
            this.selectedTemplate.parameters.forEach(param => {
                const inputElement = document.querySelector(`input[id="${param.name}"]`);
                if (inputElement) {
                    parameters[param.name] = inputElement.value || param.default || '';
                }
            });
        }

        console.log('Extracted parameters:', parameters);
        return parameters;
    }

    // 8. Integration with Chat Interface
    async sendChatMessage(message) {
        try {
            const response = await fetch(`${this.baseUrl}/agent/chat`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    message: message,
                    sessionId: this.currentSessionId
                })
            });

            const data = await response.json();

            if (data.success) {
                // Check if the AI response includes template gallery triggers
                if (this.shouldShowTemplateGallery(data.message)) {
                    await this.showTemplateGallery();
                }

                return data.message;
            } else {
                throw new Error(data.error || 'Chat request failed');
            }
        } catch (error) {
            console.error('Error sending chat message:', error);
            return 'Sorry, I encountered an error. Please try again.';
        }
    }

    // 9. Helper Methods
    shouldShowTemplateGallery(message) {
        const triggers = [
            'template gallery',
            'show templates',
            'available templates',
            'template options',
            'infrastructure templates'
        ];
        return triggers.some(trigger => message.toLowerCase().includes(trigger));
    }

    onDeploymentComplete(deploymentId, status) {
        if (status === 'Completed') {
            this.showSuccess(`Deployment ${deploymentId} completed successfully!`);
        } else {
            this.showError(`Deployment ${deploymentId} failed. Please check the logs.`);
        }
    }

    showError(message) {
        // Display error notification
        console.error(message);
        // You could integrate with your notification system here
    }

    showSuccess(message) {
        // Display success notification
        console.log(message);
        // You could integrate with your notification system here
    }
}

// Usage Example:
/*
// Initialize the template manager
const templateManager = new TemplateManager();

// Show template gallery when user clicks a button
document.getElementById('show-templates-btn').addEventListener('click', () => {
    templateManager.showTemplateGallery();
});

// Filter templates by category
document.getElementById('compute-templates-btn').addEventListener('click', () => {
    templateManager.showTemplateGallery('compute');
});

// Integrate with existing chat interface
const originalSendMessage = window.sendMessage;
window.sendMessage = async function(message) {
    // Send to AI first
    const response = await templateManager.sendChatMessage(message);
    
    // Display in chat
    addMessageToChat('assistant', response);
    
    return response;
};
*/
