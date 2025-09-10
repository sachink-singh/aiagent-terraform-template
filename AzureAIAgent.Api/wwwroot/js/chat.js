/**
 * Chat and Messaging System
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.chat = {

    // Send message to the server
    async sendMessage(message) {
        console.log('🆔 SESSION_ID:', window.AzureAIAgent.config.SESSION_ID);
        console.log('📤 Making API call to:', `${window.AzureAIAgent.config.API_BASE_URL}/api/agent/chat`);
        console.log('📤 Request payload:', { message: message, sessionId: window.AzureAIAgent.config.SESSION_ID });
        
        try {
            const response = await fetch(`${window.AzureAIAgent.config.API_BASE_URL}/api/agent/chat`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    message: message,
                    sessionId: window.AzureAIAgent.config.SESSION_ID
                })
            });
            
            console.log('📥 Raw response received:', response);
            console.log('📥 Response status:', response.status);
            console.log('📥 Response ok:', response.ok);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }

            const result = await response.json();
            console.log('📥 Parsed JSON result:', result);
            return result;
        } catch (error) {
            console.error('❌ Error in sendMessage:', error);
            console.error('❌ Error details:', {
                name: error.name,
                message: error.message,
                stack: error.stack
            });
            window.AzureAIAgent.ui.addMessage('assistant', 
                '❌ **Connection Error**\n\nSorry, I\'m having trouble connecting to the Azure services. Please check your connection and try again.\n\n' +
                `Error: ${error.message}`
            );
            throw error;
        }
    },

    // Send message with Terraform context
    async sendMessageWithContext(message, terraformContext) {
        console.log('📡 Sending message with Terraform context');
        
        try {
            const response = await fetch(`${window.AzureAIAgent.config.API_BASE_URL}/api/agent/chat`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    message: message,
                    sessionId: window.AzureAIAgent.config.SESSION_ID,
                    terraformContext: terraformContext
                })
            });
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}: ${response.statusText}`);
            }
            
            const result = await response.json();
            
            // Handle adaptive cards in the context function too
            if (result.contentType === 'adaptive-card' && result.adaptiveCard) {
                console.log('🃏 Handling adaptive card in context function');
                window.AzureAIAgent.ui.addAdaptiveCardMessage(result.message, result.adaptiveCard);
                return { success: true, message: result.message, handledAsAdaptiveCard: true };
            }
            
            return result;
        } catch (error) {
            console.error('❌ Error in sendMessageWithContext:', error);
            throw error;
        }
    },

    // Handle form submission
    async handleSubmit(e) {
        if (e) {
            e.preventDefault();
        }
        
        const chatInput = window.AzureAIAgent.config.chatInput;
        if (!chatInput) {
            console.error('❌ Chat input not found');
            return;
        }
        
        const message = chatInput.value.trim();
        if (!message) return;
        
        console.log('📤 Form submitted with message:', message);
        
        // Clear input immediately for better UX
        chatInput.value = '';
        
        // Add user message to chat
        window.AzureAIAgent.ui.addMessage('user', message);
        
        // Handle special modes
        if (this.handleSpecialModes(message)) {
            return;
        }
        
        // Show typing indicator
        window.AzureAIAgent.ui.showTyping();
        window.AzureAIAgent.ui.setFormDisabled(true);
        
        try {
            const result = await this.sendMessage(message);
            
            console.log('📥 FULL Received result:', JSON.stringify(result, null, 2));
            console.log('📄 Content type:', result.contentType);
            console.log('🃏 Has adaptive card:', !!result.adaptiveCard);
            console.log('🃏 Adaptive card type:', typeof result.adaptiveCard);
            console.log('🃏 Card data keys:', result.adaptiveCard ? Object.keys(result.adaptiveCard) : 'none');
            
            if (result.contentType === 'adaptive-card' && result.adaptiveCard) {
                console.log('🃏 Handling adaptive card response');
                console.log('🃏 Card data:', result.adaptiveCard);
                window.AzureAIAgent.ui.addAdaptiveCardMessage(result.message, result.adaptiveCard);
            } else if (result.message) {
                console.log('📝 Handling text message response');
                window.AzureAIAgent.ui.addMessage('assistant', result.message);
            } else {
                console.log('⚠️ No message or card data in response');
            }
        } catch (error) {
            console.error('❌ Error handling submission:', error);
        } finally {
            window.AzureAIAgent.ui.hideTyping();
            window.AzureAIAgent.ui.setFormDisabled(false);
            const chatInput = window.AzureAIAgent.config.chatInput;
            if (chatInput) {
                chatInput.focus();
            }
        }
    },

    // Handle special conversation modes
    handleSpecialModes(message) {
        const lowerMessage = message.toLowerCase();
        
        // Handle Terraform edit mode
        if (window.waitingForTerraformEdit) {
            window.AzureAIAgent.terraform.handleEditRequest(message);
            return true;
        }
        
        // Handle mandatory parameters mode
        if (window.waitingForMandatoryParams) {
            window.AzureAIAgent.terraform.handleMandatoryParametersResponse(message);
            return true;
        }
        
        // Handle deployment confirmation mode
        if (window.waitingForDeploymentConfirmation) {
            window.AzureAIAgent.terraform.handleDeploymentConfirmation(message);
            return true;
        }
        
        return false;
    },

    // Initialize session with state sync
    async initializeSession() {
        try {
            const response = await fetch(`${window.AzureAIAgent.config.API_BASE_URL}/api/agent/session/init`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    sessionId: window.AzureAIAgent.config.SESSION_ID
                })
            });
            
            if (response.ok) {
                const result = await response.json();
                console.log('✅ Session initialized:', result);
                window.AzureAIAgent.ui.updateStatus('ready');
            }
        } catch (error) {
            console.error('❌ Session initialization failed:', error);
            window.AzureAIAgent.ui.updateStatus('error');
        }
    }
};
