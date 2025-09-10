/**
 * UI Management and Message Display
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.ui = {

    // Add message to chat
    addMessage(role, content) {
        // Ensure content is defined
        if (content === undefined || content === null) {
            console.warn('⚠️ Message content is undefined or null');
            content = '';
        }
        
        // Get chatMessages from global config
        const chatMessages = window.AzureAIAgent.config.chatMessages;
        if (!chatMessages) {
            console.error('❌ Chat messages container not found');
            return;
        }
        
        const messageDiv = document.createElement('div');
        messageDiv.className = `message ${role}`;
        
        const bubbleDiv = document.createElement('div');
        bubbleDiv.className = 'message-bubble';
        
        // Process content for markdown-like formatting
        const formattedContent = window.AzureAIAgent.formatting.formatMessageContent(content);
        bubbleDiv.innerHTML = formattedContent;
        
        messageDiv.appendChild(bubbleDiv);
        chatMessages.appendChild(messageDiv);
        
        // Scroll to bottom
        chatMessages.scrollTop = chatMessages.scrollHeight;
        
        // Update dashboard stats based on message content
        if (role === 'assistant') {
            window.AzureAIAgent.dashboard.updateFromMessage(content);
            
            // Check for deployment completion indicators
            const lowerContent = content.toLowerCase();
            const hasSuccessIndicators = (
                lowerContent.includes('✅ deployment completed successfully') ||
                lowerContent.includes('apply complete!') ||
                lowerContent.includes('resources: ') && lowerContent.includes('added') ||
                lowerContent.includes('deployment successful') ||
                lowerContent.includes('terraform apply completed') ||
                (lowerContent.includes('success') && lowerContent.includes('deploy'))
            );
            
            if (hasSuccessIndicators) {
                console.log('✅ Success indicators detected in message, updating progress');
                // Use timeout to ensure progress system is ready
                setTimeout(() => {
                    if (window.AzureAIAgent.progress) {
                        window.AzureAIAgent.progress.handleSuccess('Deployment completed successfully!');
                    }
                }, 500);
            }
        }
        
        // Show contextual suggestions for assistant messages
        if (role === 'assistant') {
            setTimeout(() => {
                // Focus on deployment progress tracking
                console.log('Processing assistant message:', content.substring(0, 150) + '...');
                
                const lowerContent = content.toLowerCase();
                const isActuallyDeploying = (
                    lowerContent.includes('great! the deployment is in progress') ||
                    lowerContent.includes('deployment is in progress for your aks cluster') ||
                    lowerContent.includes('applying template') ||
                    lowerContent.includes('deploying infrastructure') ||
                    lowerContent.includes('terraform init') ||
                    lowerContent.includes('terraform plan') ||
                    lowerContent.includes('terraform apply') ||
                    lowerContent.includes('deployment started') ||
                    lowerContent.includes('applying terraform template') ||
                    lowerContent.includes('proceeding with deployment')
                );
                
                // Don't show progress indicator for template displays
                const isTemplateDisplay = lowerContent.includes('github template:') || 
                                        lowerContent.includes('terraform template preview');
                
                if (isActuallyDeploying && !isTemplateDisplay) {
                    console.log('🚀 Deployment detected, showing progress indicator');
                    window.AzureAIAgent.progress.showDeploymentLoading('Deploying Azure resources...');
                    // Don't auto-hide - let the progress tracking handle it
                }
            }, 500);
        }
        
        // Enhance scroll indicators for log blocks
        setTimeout(() => {
            this.enhanceScrollIndicators();
        }, 100);
    },

    // Add adaptive card message
    addAdaptiveCardMessage(textContent, adaptiveCardData) {
        console.log('🃏 addAdaptiveCardMessage called');
        console.log('📝 Text content:', textContent);
        console.log('🎴 Card data:', adaptiveCardData);
        
        // Get chatMessages from global config
        const chatMessages = window.AzureAIAgent.config.chatMessages;
        if (!chatMessages) {
            console.error('❌ Chat messages container not found');
            return;
        }
        
        // Add the text message first if there is any
        if (textContent && textContent.trim()) {
            console.log('📝 Adding text message');
            this.addMessage('assistant', textContent);
        }

        // Create the adaptive card message container
        const messageDiv = document.createElement('div');
        messageDiv.className = 'message assistant';
        
        const bubbleDiv = document.createElement('div');
        bubbleDiv.className = 'message-bubble';
        
        // Create adaptive card container
        const cardContainer = document.createElement('div');
        cardContainer.className = 'adaptive-card-container';
        
        try {
            console.log('🎨 Creating adaptive card...');
            const adaptiveCard = new AdaptiveCards.AdaptiveCard();
            adaptiveCard.hostConfig = new AdaptiveCards.HostConfig({
                fontFamily: "Segoe UI, Helvetica Neue, sans-serif"
            });
            
            // Add debouncing to prevent duplicate submissions
            let isProcessing = false;
            
            adaptiveCard.onExecuteAction = (action) => {
                if (isProcessing) {
                    console.log('🚫 Action already processing, ignoring duplicate');
                    return;
                }
                
                isProcessing = true;
                console.log('🎯 Adaptive card action executed:', action);
                const actionData = action.data || {};
                
                // For Action.Submit, the action type is in action.data.action
                // For other actions, it might be in action.id
                actionData.action = actionData.action || action.id || 'submit';
                
                console.log('🎯 Final action data:', actionData);
                
                // Reset processing flag after a short delay
                setTimeout(() => {
                    isProcessing = false;
                }, 1000);
                
                window.AzureAIAgent.cards.handleAdaptiveCardAction(actionData);
            };
            
            console.log('🔄 Parsing card data...');
            adaptiveCard.parse(adaptiveCardData);
            console.log('🎨 Rendering card...');
            const renderedCard = adaptiveCard.render();
            cardContainer.appendChild(renderedCard);
            console.log('✅ Card rendered successfully');
        } catch (error) {
            console.error('❌ Error rendering adaptive card:', error);
            cardContainer.innerHTML = '<p style="color: red;">Error rendering adaptive card</p>';
        }
        
        bubbleDiv.appendChild(cardContainer);
        messageDiv.appendChild(bubbleDiv);
        
        // Add to chat
        chatMessages.appendChild(messageDiv);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    },

    // Enhance scroll indicators for log blocks
    enhanceScrollIndicators() {
        const logBlocks = document.querySelectorAll('.logs-block');
        logBlocks.forEach(block => {
            // Check if content overflows
            const isScrollable = block.scrollHeight > block.clientHeight;
            
            if (isScrollable) {
                block.classList.add('scrollable-logs');
                
                // Add scroll event listener to hide indicator when scrolled to bottom
                block.addEventListener('scroll', function() {
                    const isAtBottom = this.scrollTop + this.clientHeight >= this.scrollHeight - 5;
                    if (isAtBottom) {
                        this.classList.add('scrolled-to-bottom');
                    } else {
                        this.classList.remove('scrolled-to-bottom');
                    }
                });
            } else {
                block.classList.remove('scrollable-logs');
            }
        });
    },

    // Form and UI state management
    setFormDisabled(disabled) {
        const chatInput = window.AzureAIAgent.config.chatInput;
        const sendButton = window.AzureAIAgent.config.sendButton;
        if (chatInput) chatInput.disabled = disabled;
        if (sendButton) sendButton.disabled = disabled;
    },

    showTyping() {
        const typingIndicator = window.AzureAIAgent.config.typingIndicator;
        if (typingIndicator) {
            typingIndicator.style.display = 'block';
        }
    },

    hideTyping() {
        const typingIndicator = window.AzureAIAgent.config.typingIndicator;
        if (typingIndicator) {
            typingIndicator.style.display = 'none';
        }
    },

    // Status management
    updateStatus(status) {
        if (statusIndicator) {
            statusIndicator.className = `status-indicator ${status}`;
            if (status === 'ready') {
                statusIndicator.title = 'Connected and ready';
            } else if (status === 'error') {
                statusIndicator.title = 'Connection error';
            }
        }
    },

    // Show notification
    showNotification(message, type = 'info') {
        const notification = document.createElement('div');
        notification.className = `notification notification-${type}`;
        notification.innerHTML = `
            <div class="notification-content">
                <span class="notification-icon">${type === 'success' ? '✅' : type === 'error' ? '❌' : 'ℹ️'}</span>
                <span class="notification-message">${message}</span>
            </div>
        `;
        
        document.body.appendChild(notification);
        
        setTimeout(() => {
            notification.classList.add('show');
        }, 10);
        
        setTimeout(() => {
            notification.classList.remove('show');
            setTimeout(() => {
                if (notification.parentNode) {
                    notification.parentNode.removeChild(notification);
                }
            }, 300);
        }, 2000);
    }
};
