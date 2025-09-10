/**
 * Event Handlers and DOM Event Management
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.events = {

    // Initialize all event handlers
    initialize() {
        this.setupChatEvents();
        this.setupFormEvents();
        this.setupKeyboardEvents();
        this.setupUIEvents();
        this.setupResourceClickEvents();
        
        console.log('📋 Event handlers initialized');
    },

    // Setup chat-related events
    setupChatEvents() {
        // Send button click
        if (window.AzureAIAgent.config.sendButton) {
            window.AzureAIAgent.config.sendButton.addEventListener('click', (e) => {
                e.preventDefault();
                this.handleSendMessage();
            });
        }

        // Chat input events
        if (window.AzureAIAgent.config.chatInput) {
            // Enter key handling
            window.AzureAIAgent.config.chatInput.addEventListener('keypress', (e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    this.handleSendMessage();
                }
            });

            // Auto-resize textarea
            window.AzureAIAgent.config.chatInput.addEventListener('input', () => {
                this.autoResizeTextarea(window.AzureAIAgent.config.chatInput);
            });

            // Focus management
            window.AzureAIAgent.config.chatInput.addEventListener('focus', () => {
                console.log('📝 Chat input focused');
            });

            // Terraform action handlers
            document.addEventListener('click', (event) => {
                if (event.target.classList.contains('terraform-action')) {
                    this.handleTerraformAction(event);
                }
            });
        }
    },

    // Setup form-related events
    setupFormEvents() {
        // Chat form submission
        const chatForm = document.getElementById('chat-form');
        if (chatForm) {
            chatForm.addEventListener('submit', (e) => {
                e.preventDefault();
                this.handleSendMessage();
            });
        }

        // Form validation
        document.addEventListener('input', (e) => {
            if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') {
                this.validateFormField(e.target);
            }
        });
    },

    // Setup keyboard shortcuts
    setupKeyboardEvents() {
        document.addEventListener('keydown', (e) => {
            // Ctrl+Enter for send (alternative to Enter)
            if (e.ctrlKey && e.key === 'Enter') {
                e.preventDefault();
                this.handleSendMessage();
            }

            // Escape to clear input
            if (e.key === 'Escape' && window.AzureAIAgent.config.chatInput) {
                window.AzureAIAgent.config.chatInput.value = '';
                window.AzureAIAgent.config.chatInput.focus();
            }

            // Ctrl+/ for help
            if (e.ctrlKey && e.key === '/') {
                e.preventDefault();
                this.showKeyboardShortcuts();
            }
        });
    },

    // Setup UI interaction events
    setupUIEvents() {
        // Window resize
        window.addEventListener('resize', () => {
            this.handleWindowResize();
        });

        // Document click for closing dropdowns, etc.
        document.addEventListener('click', (e) => {
            this.handleDocumentClick(e);
        });

        // Scroll events for chat messages
        if (window.AzureAIAgent.config.chatMessages) {
            window.AzureAIAgent.config.chatMessages.addEventListener('scroll', () => {
                this.handleChatScroll();
            });
        }
    },

    // Setup clickable resource events
    setupResourceClickEvents() {
        // Use event delegation for dynamically added content
        document.addEventListener('click', (e) => {
            if (e.target.classList.contains('clickable-resource')) {
                this.handleResourceClick(e.target);
            } else if (e.target.classList.contains('resource-name') || 
                       e.target.classList.contains('pod-name') || 
                       e.target.closest('.resource-name') ||
                       e.target.closest('.pod-name') ||
                       e.target.classList.contains('clickable-line') ||
                       (e.target.textContent && this.isResourceNameText(e.target.textContent))) {
                this.handleGenericResourceClick(e.target);
            }
        });
    },

    // Check if text looks like a resource name (Azure or Kubernetes)
    isResourceNameText(text) {
        // Common resource name patterns
        const resourcePatterns = [
            // Kubernetes patterns
            /^[a-z0-9]+-[a-z0-9]+-[a-z0-9]+$/i,  // Standard pod names
            /^[a-z0-9]+-[0-9a-f]+-[a-z0-9]+$/i,  // Pod names with hash
            /^.*-[0-9a-f]{8,}$/i,                 // Pods ending with hash
            /^[a-z0-9-]+-[0-9]+$/i,              // Pods ending with numbers
            
            // Azure patterns
            /^rg-[a-z0-9-]+$/i,                  // Resource groups
            /^[a-z0-9-]+-rg$/i,                  // Resource groups (suffix)
            /^aks-[a-z0-9-]+$/i,                 // AKS clusters
            /^acr[a-z0-9]+$/i,                   // Container registries
            /^[a-z0-9-]+-(?:app|webapp|vm|sql|db|kv|keyvault)$/i  // Various Azure resources
        ];
        
        const cleanText = text.trim();
        return resourcePatterns.some(pattern => pattern.test(cleanText)) && 
               cleanText.length > 3 && 
               cleanText.length < 100;
    },

    // Handle send message
    async handleSendMessage() {
        const input = window.AzureAIAgent.config.chatInput;
        if (!input || !input.value.trim()) {
            console.log('⚠️ No message to send');
            return;
        }

        const message = input.value.trim();
        console.log('📤 Sending message:', message);

        // Clear and focus input first
        input.value = '';
        input.focus();
        this.autoResizeTextarea(input);

        try {
            // Show typing indicator
            window.AzureAIAgent.ui.showTyping();
            
            // Add user message to chat
            window.AzureAIAgent.ui.addMessage('user', message);
            
            // Send the message and await response
            const result = await window.AzureAIAgent.chat.sendMessage(message);
            
            console.log('📥 FULL Received result:', JSON.stringify(result, null, 2));
            console.log('📄 Content type:', result.contentType);
            console.log('🃏 Has adaptive card:', !!result.adaptiveCard);
            
            // Hide typing indicator
            window.AzureAIAgent.ui.hideTyping();
            
            // Hide deployment progress indicator if it was shown
            window.AzureAIAgent.progress.hideDeploymentLoading();
            
            // Process the response - handle adaptive cards first
            if (result.contentType === 'adaptive-card' && result.adaptiveCard) {
                console.log('🃏 Handling adaptive card response from events.js');
                console.log('🃏 Card data:', result.adaptiveCard);
                window.AzureAIAgent.ui.addAdaptiveCardMessage(result.message, result.adaptiveCard);
            } else if (result && result.message) {
                console.log('📝 Handling text message response from events.js');
                window.AzureAIAgent.ui.addMessage('assistant', result.message);
            } else {
                console.log('⚠️ No message or card data in response from events.js');
            }
        } catch (error) {
            console.error('❌ Error sending message:', error);
            window.AzureAIAgent.ui.hideTyping();
            window.AzureAIAgent.progress.hideDeploymentLoading();
        }
    },

    // Auto-resize textarea based on content
    autoResizeTextarea(textarea) {
        textarea.style.height = 'auto';
        textarea.style.height = Math.min(textarea.scrollHeight, 120) + 'px';
    },

    // Validate form fields
    validateFormField(field) {
        const value = field.value.trim();
        let isValid = true;
        let message = '';

        // Basic validation based on field type
        if (field.type === 'email' && value) {
            isValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value);
            message = isValid ? '' : 'Please enter a valid email address';
        }

        if (field.required && !value) {
            isValid = false;
            message = 'This field is required';
        }

        // Update field appearance
        field.classList.toggle('invalid', !isValid);
        
        // Show/hide validation message
        let messageElement = field.parentNode.querySelector('.validation-message');
        if (message) {
            if (!messageElement) {
                messageElement = document.createElement('div');
                messageElement.className = 'validation-message';
                field.parentNode.appendChild(messageElement);
            }
            messageElement.textContent = message;
        } else if (messageElement) {
            messageElement.remove();
        }

        return isValid;
    },

    // Handle resource clicks
    handleResourceClick(element) {
        const resourceType = element.dataset.resourceType;
        const resourceName = element.dataset.resourceName;
        
        console.log(`🔗 Resource clicked: ${resourceType} - ${resourceName}`);
        
        // Get the chat input element
        const chatInput = window.AzureAIAgent.config.chatInput;
        if (!chatInput) {
            console.error('❌ Chat input not found');
            return;
        }
        
        // Create natural language queries that the AI agent can understand
        let queryText;
        switch(resourceType.toLowerCase()) {
            case 'pod':
                queryText = `Tell me about pod ${resourceName}`;
                break;
            case 'deployment':
                queryText = `Get details on deployment ${resourceName}`;
                break;
            case 'service':
                queryText = `Show me service ${resourceName} details`;
                break;
            case 'namespace':
                queryText = `Describe namespace ${resourceName}`;
                break;
            case 'configmap':
                queryText = `What's in configmap ${resourceName}?`;
                break;
            case 'secret':
                queryText = `Get details on secret ${resourceName}`;
                break;
            case 'ingress':
                queryText = `Tell me about ingress ${resourceName}`;
                break;
            case 'job':
                queryText = `What's the status of job ${resourceName}?`;
                break;
            case 'cronjob':
                queryText = `Get details on cronjob ${resourceName}`;
                break;
            default:
                // For Azure resources or unknown types, use generic natural language
                queryText = `Tell me more about ${resourceType} ${resourceName}`;
                break;
        }
        
        // Get existing text and append the new query
        const existingText = chatInput.value.trim();
        const newText = existingText ? `${existingText} ${queryText}` : queryText;
        
        // Set the new text in the input
        chatInput.value = newText;
        
        // Focus the input and position cursor at the end
        chatInput.focus();
        chatInput.setSelectionRange(newText.length, newText.length);
        
        // Visual feedback
        element.classList.add('clicked');
        setTimeout(() => {
            element.classList.remove('clicked');
        }, 200);
        
        // Trigger auto-resize if available
        if (this.autoResizeTextarea) {
            this.autoResizeTextarea(chatInput);
        }
    },

    // Handle generic resource clicks (Azure, Kubernetes, etc.)
    handleGenericResourceClick(element) {
        // Find the actual resource name element or text
        let resourceElement = element.classList.contains('resource-name') || 
                             element.classList.contains('pod-name') ||
                             element.classList.contains('clickable-line') ? element : 
                             element.closest('.resource-name, .pod-name, .clickable-line');
        
        if (!resourceElement) resourceElement = element;
        
        // Extract resource name from the element text or data attribute
        let resourceName = resourceElement.dataset.resourceName || 
                          resourceElement.dataset.podName || 
                          resourceElement.textContent.trim();
        
        // Clean up the resource name (remove extra whitespace, status indicators, etc.)
        resourceName = resourceName.replace(/\s+/g, ' ').trim();
        
        // If there are multiple lines or status info, take just the first meaningful part
        const lines = resourceName.split('\n');
        if (lines.length > 0) {
            resourceName = lines[0].trim();
        }
        
        // Extract just the resource name part (before any status or additional info)
        const parts = resourceName.split(/\s+/);
        if (parts.length > 0) {
            resourceName = parts[0];
        }
        
        console.log(`🔗 Resource clicked: ${resourceName}`);
        
        // Append to chat input instead of sending immediately
        const chatInput = window.AzureAIAgent.config.chatInput;
        if (chatInput) {
            const currentValue = chatInput.value.trim();
            const newValue = currentValue ? `${currentValue} ${resourceName}` : resourceName;
            chatInput.value = newValue;
            chatInput.focus();
            
            // Auto-resize if the function exists
            if (this.autoResizeTextarea) {
                this.autoResizeTextarea(chatInput);
            }
        }
        
        // Visual feedback
        resourceElement.classList.add('clicked');
        setTimeout(() => {
            resourceElement.classList.remove('clicked');
        }, 200);
    },

    // Handle window resize
    handleWindowResize() {
        // Adjust chat container height
        const chatContainer = document.querySelector('.chat-container');
        if (chatContainer) {
            const windowHeight = window.innerHeight;
            const headerHeight = document.querySelector('header')?.offsetHeight || 0;
            const footerHeight = document.querySelector('footer')?.offsetHeight || 0;
            const newHeight = windowHeight - headerHeight - footerHeight - 20;
            chatContainer.style.maxHeight = `${newHeight}px`;
        }
    },

    // Handle document clicks
    handleDocumentClick(e) {
        // Close any open dropdowns or modals
        const dropdowns = document.querySelectorAll('.dropdown.open');
        dropdowns.forEach(dropdown => {
            if (!dropdown.contains(e.target)) {
                dropdown.classList.remove('open');
            }
        });
    },

    // Handle chat scroll
    handleChatScroll() {
        const chatMessages = window.AzureAIAgent.config.chatMessages;
        if (!chatMessages) return;
        
        const isAtBottom = chatMessages.scrollTop + chatMessages.clientHeight >= chatMessages.scrollHeight - 10;
        
        // Show/hide scroll to bottom button
        const scrollButton = document.querySelector('.scroll-to-bottom');
        if (scrollButton) {
            scrollButton.style.display = isAtBottom ? 'none' : 'block';
        }
    },

    // Show keyboard shortcuts
    showKeyboardShortcuts() {
        const shortcuts = [
            'Enter - Send message',
            'Shift+Enter - New line',
            'Ctrl+Enter - Send message (alternative)',
            'Escape - Clear input',
            'Ctrl+/ - Show this help'
        ];
        
        const message = '⌨️ Keyboard Shortcuts:\n\n' + shortcuts.join('\n');
        window.AzureAIAgent.ui.showNotification(message, 'info');
    },

    // Add scroll to bottom functionality
    addScrollToBottomButton() {
        if (document.querySelector('.scroll-to-bottom')) return;
        
        const button = document.createElement('button');
        button.className = 'scroll-to-bottom';
        button.innerHTML = '⬇️';
        button.title = 'Scroll to bottom';
        button.style.display = 'none';
        
        button.addEventListener('click', () => {
            if (window.AzureAIAgent.config.chatMessages) {
                window.AzureAIAgent.config.chatMessages.scrollTop = window.AzureAIAgent.config.chatMessages.scrollHeight;
            }
        });
        
        document.body.appendChild(button);
    },

    // Setup tooltips
    setupTooltips() {
        const elementsWithTooltips = document.querySelectorAll('[title]');
        elementsWithTooltips.forEach(element => {
            element.addEventListener('mouseenter', (e) => {
                this.showTooltip(e.target, e.target.title);
            });
            
            element.addEventListener('mouseleave', () => {
                this.hideTooltip();
            });
        });
    },

    // Show tooltip
    showTooltip(element, text) {
        const tooltip = document.createElement('div');
        tooltip.className = 'tooltip';
        tooltip.textContent = text;
        document.body.appendChild(tooltip);
        
        const rect = element.getBoundingClientRect();
        tooltip.style.left = rect.left + (rect.width / 2) - (tooltip.offsetWidth / 2) + 'px';
        tooltip.style.top = rect.top - tooltip.offsetHeight - 10 + 'px';
        
        setTimeout(() => tooltip.classList.add('show'), 10);
    },

    // Hide tooltip
    hideTooltip() {
        const tooltip = document.querySelector('.tooltip');
        if (tooltip) {
            tooltip.remove();
        }
    },

    // Handle terraform action clicks
    handleTerraformAction(event) {
        const action = event.target.dataset.action;
        const deploymentId = event.target.dataset.deploymentId;
        
        console.log(`🎯 Terraform ${action} clicked for deployment: ${deploymentId}`);
        
        switch (action) {
            case 'deploy':
                this.handleDeployAction(deploymentId);
                break;
            case 'edit':
                this.handleEditAction(deploymentId);
                break;
            case 'cancel':
                this.handleCancelAction(deploymentId);
                break;
        }
    },

    // Handle deploy action with real progress tracking
    handleDeployAction(deploymentId) {
        console.log(`🚀 Starting deployment for: ${deploymentId}`);
        
        // Show modern progress modal
        window.AzureAIAgent.progress.showDeploymentLoading('Initializing Terraform deployment...');
        
        // Note: Don't start progress tracking yet - wait for actual deployment ID from API response
        
        // Send deployment message to backend
        const message = `Deploy terraform template: ${deploymentId}`;
        
        // Send message and handle response properly
        window.AzureAIAgent.chat.sendMessage(message).then(result => {
            console.log('📥 Deploy action response:', result);
            
            // Extract the actual deployment ID from the response
            let actualDeploymentId = null;
            if (result && result.message) {
                // Look for the deployment ID in the response message
                const deploymentIdMatch = result.message.match(/Deployment ID[:\*\s]*[`]*([a-f0-9-]{36})[`]*/i);
                if (deploymentIdMatch) {
                    actualDeploymentId = deploymentIdMatch[1];
                    console.log('✅ Extracted actual deployment ID:', actualDeploymentId);
                    
                    // Start tracking with the actual deployment ID immediately
                    console.log('🔄 Starting progress tracking for deployment:', actualDeploymentId);
                    window.AzureAIAgent.progress.trackDeploymentProgress(actualDeploymentId);
                }
                
                if (result.message.includes('deployment started') || result.message.includes('applying terraform')) {
                    window.AzureAIAgent.progress.updateDeploymentMessage('Applying Terraform configuration...');
                    window.AzureAIAgent.progress.addLogEntry('Terraform apply started');
                } else if (result.message.includes('deployment complete') || result.message.includes('successfully deployed')) {
                    this.handleDeploymentSuccess(result.message);
                } else if (result.message.includes('error') || result.message.includes('failed')) {
                    this.handleDeploymentError(result.message);
                } else {
                    // Continue deployment process
                    window.AzureAIAgent.progress.addLogEntry('Processing deployment...');
                    window.AzureAIAgent.ui.addMessage('assistant', result.message);
                }
            }
        }).catch(error => {
            console.error('❌ Deploy action failed:', error);
            this.handleDeploymentError(`Deployment failed: ${error.message}`);
        });
    },

    // Handle edit action  
    handleEditAction(deploymentId) {
        console.log(`✏️ Editing deployment: ${deploymentId}`);
        
        // Send edit message without progress indicator
        const message = `Edit terraform template: ${deploymentId}`;
        
        window.AzureAIAgent.chat.sendMessage(message).then(result => {
            console.log('📥 Edit action response:', result);
            if (result && result.message) {
                window.AzureAIAgent.ui.addMessage('assistant', result.message);
            }
        }).catch(error => {
            console.error('❌ Edit action failed:', error);
        });
    },

    // Handle cancel action
    handleCancelAction(deploymentId) {
        console.log(`❌ Cancelling deployment: ${deploymentId}`);
        
        // Send cancel message without progress indicator
        const message = `Cancel terraform template: ${deploymentId}`;
        
        window.AzureAIAgent.chat.sendMessage(message).then(result => {
            console.log('📥 Cancel action response:', result);
            if (result && result.message) {
                window.AzureAIAgent.ui.addMessage('assistant', result.message);
            }
        }).catch(error => {
            console.error('❌ Cancel action failed:', error);
        });
    }
};
