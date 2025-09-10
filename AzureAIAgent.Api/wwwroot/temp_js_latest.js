        // Initialize the application
        function initializeApp() {
            console.log('Initializing Azure AI Agent Enterprise Dashboard...');
            
            // Initialize session
            updateSessionDisplay();
            updateStatus('ready');
            
            // Initialize dashboard stats
            initializeDashboardStats();
            
            // Start session timer
            startSessionTimer();
            
            // Initialize template management system
            if (typeof TemplateManager !== 'undefined') {
                try {
                    window.templateManager = new TemplateManager(API_BASE_URL);
                    console.log('✅ Template management system initialized');
                } catch (error) {
                    console.warn('⚠️ Template management initialization failed:', error);
                }
            }
            
            // Hide suggestion chips on startup (disabled for now)
            hideSuggestionChips();
            
            // Note: Form handler is already set up separately in the DOM event listeners below
            
            // Initialize status
            console.log('Enterprise Dashboard initialized successfully');
        }

        // Dashboard Statistics Management
        let sessionStartTime = Date.now();
        let deploymentCount = 0;
        let resourceCount = 0;

        function initializeDashboardStats() {
            updateStat('deploymentCount', 0);
            updateStat('resourceCount', 0);
            updateStat('sessionTime', '00:00');
        }

        function updateStat(statId, value) {
            const element = document.getElementById(statId);
            if (element) {
                if (typeof value === 'number' && value !== parseInt(element.textContent)) {
                    // Animate number change
                    animateCounter(element, parseInt(element.textContent) || 0, value);
                } else {
                    element.textContent = value;
                }
            }
        }

        function animateCounter(element, from, to) {
            const duration = 600;
            const start = Date.now();
            
            function update() {
                const elapsed = Date.now() - start;
                const progress = Math.min(elapsed / duration, 1);
                const current = Math.round(from + (to - from) * easeOutCubic(progress));
                
                element.textContent = current;
                
                if (progress < 1) {
                    requestAnimationFrame(update);
                }
            }
            
            requestAnimationFrame(update);
        }

        function easeOutCubic(t) {
            return 1 - Math.pow(1 - t, 3);
        }

        function startSessionTimer() {
            setInterval(() => {
                const elapsed = Date.now() - sessionStartTime;
                const minutes = Math.floor(elapsed / 60000);
                const seconds = Math.floor((elapsed % 60000) / 1000);
                updateStat('sessionTime', `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`);
            }, 1000);
        }

        function incrementDeploymentCount() {
            deploymentCount++;
            updateStat('deploymentCount', deploymentCount);
        }

        function updateResourceCount(count) {
            resourceCount = count;
            updateStat('resourceCount', resourceCount);
        }

        // Update dashboard stats based on message content
        function updateDashboardFromMessage(content) {
            const lowerContent = content.toLowerCase();
            
            // Check for deployment completion
            if (lowerContent.includes('deployment complete') || 
                lowerContent.includes('deployment completed') ||
                lowerContent.includes('✅ deployment') ||
                lowerContent.includes('all azure resources created successfully')) {
                incrementDeploymentCount();
            }
            
            // Extract resource count from messages
            const resourceMatches = content.match(/(\d+)\s+(resource|deployment)/gi);
            if (resourceMatches) {
                const numbers = resourceMatches.map(match => parseInt(match.match(/\d+/)[0]));
                if (numbers.length > 0) {
                    updateResourceCount(Math.max(...numbers));
                }
            }
            
            // Check for specific resource types and update count
            const resourceTypes = ['resource group', 'storage account', 'virtual machine', 'aks cluster', 'app service'];
            let foundResources = 0;
            resourceTypes.forEach(type => {
                const regex = new RegExp(type.replace(' ', '\\s+'), 'gi');
                const matches = content.match(regex);
                if (matches) {
                    foundResources += matches.length;
                }
            });
            
            if (foundResources > 0) {
                updateResourceCount(Math.max(resourceCount, foundResources));
            }
        }
        
        // Initialize the application when DOM is loaded
        document.addEventListener('DOMContentLoaded', function() {
            initializeApp();
            
            // Add click handlers to all terraform blocks (existing and future) - DISABLED FOR NOW
            /*
            document.addEventListener('click', function(e) {
                if (e.target.classList.contains('terraform-block') || e.target.closest('.terraform-block')) {
                    const terraformBlock = e.target.classList.contains('terraform-block') ? e.target : e.target.closest('.terraform-block');
                    const codeId = terraformBlock.id;
                    if (codeId && codeId.startsWith('terraform-')) {
                        console.log('Terraform block clicked:', codeId);
                        showTerraformSuggestions(codeId);
                    }
                }
            });
            */
            
            // Add a manual test function accessible from browser console (for development) - DISABLED
            window.testChips = function() {
                console.log('Manual chip test triggered - BUT DISABLED FOR NOW');
                // DISABLED - showSuggestionChips([
                //     { text: '🧪 Manual Test Chip', action: 'test', type: 'primary' },
                //     { text: '📝 Debug Chip', action: 'debug', type: 'edit' }
                // ]);
            };
        });

        // Configuration with Terraform state management
        // Get the current URL to determine the correct API base URL
        const currentProtocol = window.location.protocol;
        const currentHost = window.location.host;
        const API_BASE_URL = `${currentProtocol}//${currentHost}`;
        console.log('API_BASE_URL:', API_BASE_URL);
        const SESSION_ID = 'chat-session-' + Date.now();
        
        // DOM elements
        const chatMessages = document.getElementById('chatMessages');
        const chatInput = document.getElementById('chatInput');
        const sendButton = document.getElementById('sendButton');
        const chatForm = document.getElementById('chatForm');
        const typingIndicator = document.getElementById('typingIndicator');
        const progressIndicator = document.getElementById('progressIndicator');
        const statusIndicator = document.getElementById('statusIndicator');
        const sessionIdElement = document.getElementById('sessionId');
        
        // Initialize
        sessionIdElement.textContent = SESSION_ID;
        
        // Event listeners
        console.log('Setting up event listeners');
        console.log('Chat form element:', chatForm);
        console.log('Chat input element:', chatInput);
        console.log('Send button element:', sendButton);
        
        if (!chatForm) {
            console.error('❌ Chat form not found!');
            alert('Chat form not found! Please refresh the page.');
        } else if (!chatInput) {
            console.error('❌ Chat input not found!');
            alert('Chat input not found! Please refresh the page.');
        } else {
            // Only set up event listeners if elements exist
            console.log('✅ All elements found, setting up event listeners');
            
            // Test if basic form submission works
            chatForm.addEventListener('submit', function(e) {
                console.log('🚀 Form submit event triggered!');
                e.preventDefault();
                e.stopPropagation();
                handleSubmit(e);
            });
            
            chatInput.addEventListener('keypress', function(e) {
                if (e.key === 'Enter' && !e.shiftKey) {
                    console.log('🚀 Enter key pressed!');
                    e.preventDefault();
                    e.stopPropagation();
                    handleSubmit(e);
                }
            });
            
            // Also add click event to send button as backup
            if (sendButton) {
                sendButton.addEventListener('click', function(e) {
                    console.log('🚀 Send button clicked!');
                    e.preventDefault();
                    e.stopPropagation();
                    handleSubmit(e);
                });
            }
        }
        
        console.log('Event listeners attached');
        
        // Check API status on load and initialize session
        checkApiStatus();
        initializeSessionWithStateSync();
        
        // Session management initialization with Terraform state sync
        async function initializeSessionWithStateSync() {
            try {
                // Check if we have existing Terraform state
                const response = await fetch(`${API_BASE_URL}/api/agent/status`);
                const status = await response.json();
                
                console.log('🔄 Azure AI Agent initialized with session management');
                console.log('📋 Session ID:', SESSION_ID);
                console.log('🌍 Terraform state synchronization: Active');
                console.log('🔒 Session persistence: Enabled');
                
                // Show session info in UI
                const sessionIdElement = document.getElementById('sessionId');
                if (sessionIdElement) {
                    sessionIdElement.textContent = SESSION_ID;
                }
                
                // Simple welcome message with clear workflow
                if (status.aiServiceConfigured) {
                    setTimeout(() => {
                        addMessage('system', 
                            '🤖 **Azure AI Agent Ready**\n\n' +
                            'I can help you build Azure infrastructure step by step:\n\n' +
                            '**Simple Workflow:**\n' +
                            '1️⃣ Tell me what you want to build\n' +
                            '2️⃣ I\'ll create the infrastructure template\n' +
                            '3️⃣ Click **Apply** to deploy to Azure\n' +
                            '4️⃣ Start using your resources!\n\n' +
                            '**Try saying:**\n' +
                            '• "Create a web app with database"\n' +
                            '• "I need a Kubernetes cluster"\n' +
                            '• "Set up storage for my files"\n\n' +
                            'What would you like to create?',
                            false
                        );
                    }, 500);
                }
            } catch (error) {
                console.warn('Session initialization warning:', error);
            }
        }
        
        async function handleSubmit(e) {
            e.preventDefault();
            
            const message = chatInput.value.trim();
            
            if (!message) {
                return;
            }

            // Disable form during processing
            setFormDisabled(true);
            
            // Add user message to chat
            addMessage('user', message);
            chatInput.value = '';
            
            // Check if we're in a special workflow state
            if (window.waitingForMandatoryParams) {
                handleMandatoryParametersResponse(message);
                setFormDisabled(false);
                return;
            }
            
            if (window.waitingForTerraformEdit) {
                handleTerraformEditRequest(message);
                setFormDisabled(false);
                return;
            }
            
            if (window.waitingForDeploymentConfirmation) {
                handleDeploymentConfirmation(message);
                return; // Don't re-enable form yet, deployment will handle it
            }
            
            // Reset placeholder to default when user sends a normal message
            chatInput.placeholder = "Ask me to create Azure resources (e.g., 'Create an AKS cluster')";
            
            // Clear any stored Terraform context for normal conversations
            if (!window.waitingForMandatoryParams && !window.waitingForTerraformEdit && !window.waitingForDeploymentConfirmation) {
                window.currentTerraformContext = null;
                // Note: Keep extractedTerraformParams for potential edit operations
            }

            // Check if user wants to edit Terraform and auto-provide context
            const isEditTerraformRequest = /edit.*terraform|terraform.*edit|modify.*terraform|update.*terraform/i.test(message);
            if (isEditTerraformRequest) {
                // Try to find the most recent Terraform code block
                const terraformBlocks = document.querySelectorAll('.terraform-block');
                if (terraformBlocks.length > 0) {
                    // Use the most recent (last) Terraform block
                    const lastTerraformBlock = terraformBlocks[terraformBlocks.length - 1];
                    const rawCode = lastTerraformBlock.getAttribute('data-raw-code');
                    
                    if (rawCode) {
                        console.log('🔍 Auto-detected Terraform edit request, providing context');
                        
                        // Send message with Terraform context
                        try {
                            await sendMessageWithContext(message, rawCode);
                            hideTyping();
                            hideDeploymentProgress();
                            setFormDisabled(false);
                            return; // Exit early, don't send regular message
                        } catch (error) {
                            console.error('Error sending Terraform edit request:', error);
                            addMessage('assistant', '❌ Sorry, I encountered an error processing your Terraform edit request. Please try again.');
                            hideTyping();
                            hideDeploymentProgress();
                            setFormDisabled(false);
                            return;
                        }
                    }
                }
            }

            // Check if this might be a deployment request
            const isDeploymentRequest = isResourceCreationRequest(message) || 
                                      message.toLowerCase().includes('deploy') ||
                                      message.toLowerCase().includes('apply') ||
                                      message.toLowerCase().includes('yes') ||
                                      message.toLowerCase().includes('confirm');
            
            if (isDeploymentRequest) {
                showDeploymentProgress();
            } else {
                showTyping();
            }
            
            try {
                const response = await sendMessage(message);
                hideTyping();
                hideDeploymentProgress();
                setFormDisabled(false);
                
                if (response.success) {
                    // If it was handled as an adaptive card, don't add a regular message
                    if (!response.handledAsAdaptiveCard) {
                        addMessage('assistant', response.message);
                        
                        // Check if the response indicates deployment has started
                        if (isDeploymentStartingResponse(response.message)) {
                            showDeploymentProgress();
                            // Form stays disabled during actual deployment
                            simulateDeploymentProgress();
                        }
                    }
                } else {
                    addMessage('assistant', `❌ Error: ${response.error || 'Unknown error'}`);
                }
                
            } catch (error) {
                hideTyping();
                hideDeploymentProgress();
                setFormDisabled(false);
                addMessage('assistant', `❌ Connection error: ${error.message}`);
                updateStatus('error');
            }
        }
        
        function isResourceCreationRequest(message) {
            const createKeywords = ['create', 'deploy', 'provision', 'generate', 'setup', 'build'];
            const resourceKeywords = ['resource group', 'storage account', 'virtual machine', 'app service', 'database', 'function app'];
            const executeKeywords = ['apply', 'execute', 'run', 'deploy template', 'apply template', 'execute template'];
            
            const lowerMessage = message.toLowerCase();
            
            // Check for execution commands (apply, execute, etc.)
            const hasExecuteKeyword = executeKeywords.some(keyword => lowerMessage.includes(keyword));
            if (hasExecuteKeyword) {
                return true; // Always show progress for execution commands
            }
            
            // Check for resource creation commands
            const hasCreateKeyword = createKeywords.some(keyword => lowerMessage.includes(keyword));
            const hasResourceKeyword = resourceKeywords.some(keyword => lowerMessage.includes(keyword));
            
            return hasCreateKeyword && hasResourceKeyword;
        }
        
        function isDeploymentStartingResponse(responseMessage) {
            const lowerResponse = responseMessage.toLowerCase();
            
            // Check for deployment starting indicators in the assistant's response
            const deploymentStartIndicators = [
                "i'm deploying",
                "deploying now",
                "starting deployment",
                "applying terraform",
                "provisioning",
                "creating resources",
                "deployment in progress",
                "executing terraform",
                "applying configuration",
                "terraform apply",
                "applyterraformtemplate function called",
                "function call successful",
                "🎉 **function call successful**",
                "template content length:",
                "creating deployment directory:",
                "terraform template content",
                "deployment directory:",
                "terraform init",
                "terraform plan",
                "🚀🚀🚀 applyterraformtemplate function called!",
                "✅ template content received",
                "proceeding with deployment",
                "📝 template size:",
                "deploying infrastructure",
                "✅ **deployment complete!**"
            ];
            
            return deploymentStartIndicators.some(indicator => lowerResponse.includes(indicator));
        }
        
        async function simulateProgress(userMessage) {
            const lowerMessage = userMessage.toLowerCase();
            
            let steps, timings, stepLabels;
            
            // Different progress steps based on message type
            if (lowerMessage.includes('apply') || lowerMessage.includes('execute')) {
                // Terraform execution progress
                steps = ['step-analyzing', 'step-planning', 'step-generating', 'step-reviewing'];
                stepLabels = [
                    'Preparing Terraform workspace',
                    'Running terraform init',
                    'Executing terraform apply',
                    'Validating deployment'
                ];
                timings = [1000, 2000, 3000, 1000];
            } else {
                // Template generation progress
                steps = ['step-analyzing', 'step-planning', 'step-generating', 'step-reviewing'];
                stepLabels = [
                    'Analyzing your request',
                    'Planning Azure resources',
                    'Generating Terraform template',
                    'Preparing for review'
                ];
                timings = [800, 1200, 1500, 600];
            }
            
            // Update step labels
            for (let i = 0; i < steps.length; i++) {
                const stepElement = document.getElementById(steps[i]);
                if (stepElement) {
                    stepElement.querySelector('.progress-step-text').textContent = stepLabels[i];
                }
            }
            
            for (let i = 0; i < steps.length; i++) {
                // Mark current step as active
                updateProgressStep(steps[i], 'active');
                
                // Wait for the specified time
                await new Promise(resolve => setTimeout(resolve, timings[i]));
                
                // Mark current step as completed
                updateProgressStep(steps[i], 'completed');
                
                // Small delay between steps
                if (i < steps.length - 1) {
                    await new Promise(resolve => setTimeout(resolve, 200));
                }
            }
        }
        
        function updateProgressStep(stepId, status) {
            const step = document.getElementById(stepId);
            if (step) {
                step.className = `progress-step ${status}`;
                const icon = step.querySelector('.progress-step-icon');
                if (status === 'completed') {
                    icon.textContent = '✓';
                } else if (status === 'error') {
                    icon.textContent = '✗';
                } else {
                    // Reset to number for active/pending
                    const stepNumber = stepId.split('-')[1] === 'analyzing' ? '1' :
                                     stepId.split('-')[1] === 'planning' ? '2' :
                                     stepId.split('-')[1] === 'generating' ? '3' : '4';
                    icon.textContent = stepNumber;
                }
            }
        }
        
        function resetProgressSteps() {
            const steps = ['step-analyzing', 'step-planning', 'step-generating', 'step-reviewing'];
            steps.forEach(stepId => {
                updateProgressStep(stepId, 'pending');
            });
        }
        
        // Real deployment progress tracking
        let currentDeploymentId = null;
        let deploymentPollingInterval = null;

        function showProgress(text = 'Processing...') {
            const progressContainer = document.getElementById('progressContainer');
            const progressStatus = document.getElementById('progressStatus');
            const progressText = document.getElementById('progressText');
            
            if (progressContainer && progressStatus && progressText) {
                progressText.textContent = text;
                progressContainer.style.display = 'block';
                progressStatus.style.display = 'block';
                updateProgress(0);
            }
        }

        async function trackDeploymentProgress(deploymentId) {
            currentDeploymentId = deploymentId;
            console.log('Starting deployment tracking for:', deploymentId);
            
            showDeploymentLoading('Deploying Azure resources');
            
            deploymentPollingInterval = setInterval(async () => {
                try {
                    const response = await fetch(`${API_BASE_URL}/api/azure/deployment-status/${deploymentId}`);
                    
                    if (response.ok) {
                        const status = await response.json();
                        console.log('Deployment status:', status);
                        
                        // Update the deployment text with current phase
                        const deploymentText = document.getElementById('deploymentText');
                        if (deploymentText) {
                            deploymentText.textContent = status.message || 'Deploying';
                        }
                        
                        if (status.status === 'Completed') {
                            clearInterval(deploymentPollingInterval);
                            deploymentPollingInterval = null;
                            hideDeploymentLoading();
                            
                            addMessage('assistant', `✅ Deployment completed successfully!\n\nResources created: ${status.resourcesCreated?.join(', ') || 'Azure resources'}`);
                            
                        } else if (status.status === 'Failed') {
                            clearInterval(deploymentPollingInterval);
                            deploymentPollingInterval = null;
                            hideDeploymentLoading();
                            
                            addMessage('assistant', `❌ Deployment failed: ${status.message}`);
                        }
                    } else {
                        console.error('Failed to fetch deployment status');
                        clearInterval(deploymentPollingInterval);
                        deploymentPollingInterval = null;
                        hideDeploymentLoading();
                    }
                } catch (error) {
                    console.error('Error polling deployment status:', error);
                    clearInterval(deploymentPollingInterval);
                    deploymentPollingInterval = null;
                    hideDeploymentLoading();
                }
            }, 2000); // Poll every 2 seconds
        }
        
        function updateProgress(percentage, text = null) {
            const progressBar = document.getElementById('progressBar');
            const progressText = document.getElementById('progressText');
            
            if (progressBar) {
                progressBar.style.width = `${Math.min(100, Math.max(0, percentage))}%`;
            }
            
            if (text && progressText) {
                progressText.textContent = text;
            }
        }
        
        function hideProgress() {
            const progressContainer = document.getElementById('progressContainer');
            const progressStatus = document.getElementById('progressStatus');
            
            if (progressContainer && progressStatus) {
                progressContainer.style.display = 'none';
                progressStatus.style.display = 'none';
            }
        }
        
        // Simulate deployment progress
        function simulateDeploymentProgress(deploymentText = 'Deploying Azure resources...') {
            showProgress(deploymentText);
            
            const steps = [
                { percent: 10, text: 'Validating Terraform template...' },
                { percent: 25, text: 'Initializing Terraform...' },
                { percent: 40, text: 'Planning deployment...' },
                { percent: 60, text: 'Creating resource group...' },
                { percent: 75, text: 'Provisioning resources...' },
                { percent: 90, text: 'Configuring security settings...' },
                { percent: 100, text: 'Deployment completed!' }
            ];
            
            let currentStep = 0;
            const interval = setInterval(() => {
                if (currentStep < steps.length) {
                    const step = steps[currentStep];
                    updateProgress(step.percent, step.text);
                    currentStep++;
                } else {
                    clearInterval(interval);
                    setTimeout(() => hideProgress(), 2000);
                }
            }, 1000);
        }
        
        async function sendMessage(message) {
            console.log('🚀 sendMessage called with:', message);
            console.log('📡 API_BASE_URL:', API_BASE_URL);
            console.log('🆔 SESSION_ID:', SESSION_ID);
            
            try {
                const response = await fetch(`${API_BASE_URL}/api/agent/chat`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        message: message,
                        sessionId: SESSION_ID
                    })
                });
                
                console.log('📥 Response received, status:', response.status, response.statusText);
                
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
                }
                
                const result = await response.json();
                console.log('📋 Parsed JSON result:', result);
                
                // Handle adaptive cards in the main chat function
                if (result.contentType === 'adaptive-card' && result.adaptiveCard) {
                    console.log('🃏 Handling adaptive card');
                    // Add the adaptive card message directly here instead of returning the response
                    addAdaptiveCardMessage(result.message, result.adaptiveCard);
                    return { success: true, message: result.message, handledAsAdaptiveCard: true };
                }
                
                console.log('✅ Returning normal result');
                return result;
            } catch (error) {
                console.error('❌ Error in sendMessage:', error);
                throw error;
            }
            
            return result;
        }
        
        function addMessage(role, content) {
            // Ensure content is defined
            if (content === undefined || content === null) {
                content = '[No content]';
            }
            
            const messageDiv = document.createElement('div');
            messageDiv.className = `message ${role}`;
            
            const bubbleDiv = document.createElement('div');
            bubbleDiv.className = 'message-bubble';
            
            // Process content for markdown-like formatting
            const formattedContent = formatMessageContent(content);
            bubbleDiv.innerHTML = formattedContent;
            
            messageDiv.appendChild(bubbleDiv);
            chatMessages.appendChild(messageDiv);
            
            // Scroll to bottom
            chatMessages.scrollTop = chatMessages.scrollHeight;
            
            // Update dashboard stats based on message content
            if (role === 'assistant') {
                updateDashboardFromMessage(content);
            }
            
            // Show contextual suggestions for assistant messages
            if (role === 'assistant') {
                setTimeout(() => {
                    // CHIPS COMPLETELY DISABLED FOR NOW - FOCUS ON DEPLOYMENT PROGRESS
                    // showContextualSuggestions_NEW(content);
                    
                    console.log('Processing assistant message:', content.substring(0, 150) + '...');
                    
                    // Debug: Log content for deployment detection
                    console.log('🔍 Checking for deployment patterns in content...');
                    console.log('📝 Full content preview (first 300 chars):', content.substring(0, 300));
                    
                    // Show deployment loading ONLY when deployment is actually starting (not just showing template)
                    const lowerContent = content.toLowerCase();
                    
                    // Check if this is just showing the template vs actually starting deployment
                    const isShowingTemplate = lowerContent.includes('would you like to proceed') ||
                                            lowerContent.includes('just say "yes"') ||
                                            lowerContent.includes('just say "deploy"') ||
                                            lowerContent.includes('to continue') ||
                                            lowerContent.includes('confirm deployment');
                    
                    // Only show progress if deployment is actually starting (not just showing template)
                    const isActuallyDeploying = (lowerContent.includes('great! the deployment is in progress') ||
                                               lowerContent.includes('deployment is in progress for your aks cluster') ||
                                               lowerContent.includes('applying template') ||
                                               lowerContent.includes('deploying infrastructure') ||
                                               lowerContent.includes('terraform init') ||
                                               lowerContent.includes('terraform plan') ||
                                               lowerContent.includes('terraform apply') ||
                                               lowerContent.includes('deployment started') ||
                                               lowerContent.includes('applying terraform template') ||
                                               lowerContent.includes('proceeding with deployment') ||
                                               lowerContent.includes('let\'s start your deployment')) && !isShowingTemplate;
                    
                    if (content && isActuallyDeploying) {
                        console.log('🚀 Deployment detected, showing loading indicator');
                        console.log('📊 Deployment detection details:');
                        console.log('   - Is showing template/asking confirmation:', isShowingTemplate);
                        console.log('   - Is actually deploying:', isActuallyDeploying);
                        console.log('   - Deployment progress pattern:', lowerContent.includes('deployment is in progress'));
                        console.log('   - Applying template pattern:', lowerContent.includes('applying template'));
                        console.log('Matched content:', content.substring(0, 200));
                        // Hide any existing chips during deployment
                        hideSuggestionChips();
                        showDeploymentLoading('Deploying Azure resources');
                    }
                    
                    // Hide deployment loading if deployment completed
                    if (content && (lowerContent.includes('deployment successful') ||
                                   lowerContent.includes('deployment completed') ||
                                   lowerContent.includes('resources created successfully') ||
                                   lowerContent.includes('terraform apply completed') ||
                                   lowerContent.includes('✅') ||
                                   lowerContent.includes('infrastructure deployed') ||
                                   lowerContent.includes('deployment finished'))) {
                        console.log('Deployment completion detected, hiding loading indicator');
                        hideDeploymentLoading();
                        // CHIPS STILL DISABLED - RE-ENABLE LATER
                        // showContextualSuggestions_NEW(content);
                    }
                }, 500);
            }
        }

        function addAdaptiveCardMessage(textContent, adaptiveCardData) {
            // Add the text message first if there is any
            if (textContent && textContent.trim()) {
                addMessage('assistant', textContent);
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
                // Create adaptive card instance
                const adaptiveCard = new AdaptiveCards.AdaptiveCard();
                
                // Set up action handling
                adaptiveCard.onExecuteAction = function(action) {
                    console.log('🎯 onExecuteAction triggered!');
                    console.log('🔍 Action type:', action.constructor.name);
                    console.log('🔍 Action instanceof SubmitAction:', action instanceof AdaptiveCards.SubmitAction);
                    console.log('🔍 Action title:', action.title);
                    console.log('🔍 Action id:', action.id);
                    console.log('🔍 Action data:', action.data);
                    
                    if (action instanceof AdaptiveCards.SubmitAction) {
                        console.log('Adaptive card action executed:', action.data);
                        console.log('Action title:', action.title);
                        console.log('Action id:', action.id);
                        
                        // Handle the action data
                        const actionData = action.data;
                        if (actionData) {
                            handleAdaptiveCardAction(actionData);
                        } else {
                            console.warn('No action data received from adaptive card');
                        }
                    } else {
                        console.log('🚨 Non-SubmitAction detected:', action);
                    }
                };
                
                // Parse and render the card
                console.log('🃏 About to parse adaptive card data:', JSON.stringify(adaptiveCardData, null, 2));
                adaptiveCard.parse(adaptiveCardData);
                const renderedCard = adaptiveCard.render();
                
                // Debug: Check what buttons were created
                if (renderedCard) {
                    const buttons = renderedCard.querySelectorAll('button');
                    console.log('🔘 Found buttons in rendered card:', buttons.length);
                    buttons.forEach((button, index) => {
                        console.log(`🔘 Button ${index}:`, {
                            text: button.textContent,
                            innerHTML: button.innerHTML,
                            onclick: button.onclick,
                            attributes: Array.from(button.attributes).map(attr => `${attr.name}="${attr.value}"`).join(', ')
                        });
                        
                        // Add direct event listener for Cancel button to bypass adaptive cards validation
                        if (button.textContent.trim().toLowerCase().includes('cancel')) {
                            console.log('🔧 Adding direct event listener to Cancel button');
                            button.addEventListener('click', function(event) {
                                console.log('🚨 Direct Cancel button click detected!');
                                event.preventDefault();
                                event.stopPropagation();
                                
                                // Handle cancel through the proper action handler
                                console.log('❌ Cancel action triggered directly - calling handleAdaptiveCardAction');
                                handleAdaptiveCardAction({ action: 'cancel' });
                                return false;
                            }, true); // Use capture phase to intercept before adaptive cards
                        }
                    });
                }
                
                if (renderedCard) {
                    cardContainer.appendChild(renderedCard);
                } else {
                    cardContainer.innerHTML = '<p>⚠️ Unable to render adaptive card</p>';
                }
            } catch (error) {
                console.error('Error rendering adaptive card:', error);
                cardContainer.innerHTML = '<p>❌ Error rendering interactive card. Falling back to text view.</p>';
                
                // Fallback: show as formatted JSON
                const pre = document.createElement('pre');
                pre.style.background = '#f5f5f5';
                pre.style.padding = '12px';
                pre.style.borderRadius = '8px';
                pre.style.fontSize = '12px';
                pre.style.overflow = 'auto';
                pre.textContent = JSON.stringify(adaptiveCardData, null, 2);
                cardContainer.appendChild(pre);
            }
            
            bubbleDiv.appendChild(cardContainer);
            messageDiv.appendChild(bubbleDiv);
            
            // Add to chat
            const chatMessages = document.getElementById('chatMessages');
            chatMessages.appendChild(messageDiv);
            chatMessages.scrollTop = chatMessages.scrollHeight;
        }

        function handleAdaptiveCardAction(actionData) {
            console.log('🎯 handleAdaptiveCardAction called');
            console.log('📋 Action data received:', actionData);
            console.log('🔍 Action data type:', typeof actionData);
            console.log('📝 Action data keys:', actionData ? Object.keys(actionData) : 'null/undefined');
            console.log('⚙️ Has action property:', actionData && actionData.hasOwnProperty('action'));
            console.log('🏷️ Action value:', actionData ? actionData.action : 'no action');
            console.log('📄 Full action data JSON:', JSON.stringify(actionData, null, 2));
            
            // Special handling for cancel actions - check multiple ways cancel might be sent
            if (actionData && (
                actionData.action === 'cancel' || 
                actionData.action === 'Cancel' ||
                actionData.action === 'cancel_deployment' ||
                (actionData.title && actionData.title.toLowerCase().includes('cancel')) ||
                (actionData.id && actionData.id.toLowerCase().includes('cancel')) ||
                // Additional check: if there's no action but this looks like a cancel (no form fields)
                (!actionData.action && !actionData.workload_name && !actionData.owner_team && !actionData.environment && 
                 Object.keys(actionData).length <= 1) // Only has action or is nearly empty
            )) {
                console.log('❌ Cancel action detected - stopping any ongoing processes');
                console.log('🛑 Cancel action data:', actionData);
                addMessage('user', 'Cancel');
                addMessage('assistant', '🛑 Operation cancelled by user.');
                return; // Exit early without sending to input box
            }
            
            // Check if this is a form submission (has form fields but no specific action)
            if (!actionData.action && (actionData.workload_name || actionData.owner_team || actionData.environment)) {
                console.log('✅ Processing AKS cluster creation form submission');
                
                // Build structured command from form data
                let command = `Create AKS cluster: workload_name="${actionData.workload_name || 'aks-cluster'}", project_name="${actionData.project_name || 'default'}", owner="${actionData.owner_team || 'devops'}", environment="${actionData.environment || 'dev'}", location="${actionData.azure_region || 'East US'}", node_count="${actionData.node_count || '3'}", vm_size="${actionData.vm_size || 'Standard_DS2_v2'}", enable_autoscaling=${actionData.enable_autoscaling || false}, enable_rbac=${actionData.enable_rbac || false}, network_policy="${actionData.network_policy || 'azure'}"`;
                
                // Send the structured command to the API for processing
                addMessage('user', 'Submit AKS Cluster Configuration');
                
                // Properly handle the response
                sendMessage(command).then(result => {
                    if (result && result.message && !result.handledAsAdaptiveCard) {
                        addMessage('assistant', result.message);
                    }
                }).catch(error => {
                    console.error('Error sending form submission:', error);
                    addMessage('assistant', 'Sorry, there was an error processing your request.');
                });
                
                return;
            }
            
            // Convert action to natural language command for other actions
            let command = '';
            
            switch(actionData.action) {
                case 'get_pod_logs':
                    command = `Get logs for pod ${actionData.podName} in deployment ${actionData.deploymentName}`;
                    if (actionData.namespaceName) {
                        command += ` namespace ${actionData.namespaceName}`;
                    }
                    break;
                case 'describe_pod':
                    command = `Describe pod ${actionData.podName} in deployment ${actionData.deploymentName}`;
                    break;
                case 'get_pod_metrics':
                    command = `Get metrics for pod ${actionData.podName} in deployment ${actionData.deploymentName}`;
                    break;
                case 'restart_pod':
                    command = `Restart pod ${actionData.podName} in deployment ${actionData.deploymentName}`;
                    break;
                case 'start_vm':
                    command = `Start virtual machine ${actionData.vmName} in resource group ${actionData.resourceGroup}`;
                    break;
                case 'stop_vm':
                    command = `Stop virtual machine ${actionData.vmName} in resource group ${actionData.resourceGroup}`;
                    break;
                case 'restart_vm':
                    command = `Restart virtual machine ${actionData.vmName} in resource group ${actionData.resourceGroup}`;
                    break;
                case 'get_vm_details':
                    command = `Get details for virtual machine ${actionData.vmName} in resource group ${actionData.resourceGroup}`;
                    break;
                case 'get_vm_metrics':
                    command = `Get metrics for virtual machine ${actionData.vmName} in resource group ${actionData.resourceGroup}`;
                    break;
                case 'browse_containers':
                    command = `Browse containers in storage account ${actionData.storageAccountName}`;
                    break;
                case 'manage_storage_keys':
                    command = `Manage keys for storage account ${actionData.storageAccountName}`;
                    break;
                case 'get_storage_metrics':
                    command = `Get metrics for storage account ${actionData.storageAccountName}`;
                    break;
                case 'refresh_pods':
                    command = `Refresh pods for deployment ${actionData.deploymentName}`;
                    break;
                case 'refresh_vms':
                    command = `Refresh virtual machines list`;
                    break;
                case 'refresh_storage':
                    command = `Refresh storage accounts list`;
                    break;
                case 'cancel':
                case 'Cancel':
                case 'cancel_deployment':
                    console.log('❌ Cancel action received from adaptive card');
                    command = 'Cancel this deployment';
                    break;
                default:
                    command = `Execute action: ${actionData.action}`;
                    break;
            }
            
            // Send the command as if the user typed it
            if (command) {
                document.getElementById('messageInput').value = command;
                sendUserMessage();
            }
        }

        function shouldShowDeploymentProgress(content) {
            const lowerContent = content.toLowerCase();
            return (lowerContent.includes("i'm deploying") || 
                   lowerContent.includes("deploying now") ||
                   (lowerContent.includes("deploying") && lowerContent.includes("cluster"))) &&
                   !lowerContent.includes("completed") &&
                   !lowerContent.includes("finished");
        }

        function showContextualSuggestions_NEW(content) {
            // Hide any existing chips first
            hideSuggestionChips();
            
            // Only show suggestions for certain types of responses
            const lowerContent = content.toLowerCase();
            console.log('Checking content for suggestions:', content.substring(0, 100) + '...');
            
            // Don't show suggestions if we're in a parameter collection phase
            if (isParameterCollectionPhase(content)) {
                console.log('Skipping suggestions - in parameter collection phase');
                return;
            }
            
            // Generate contextual suggestions based on content
            const suggestions = generateContextualChips(content, lowerContent);
            console.log('Generated suggestions:', suggestions);
            
            if (suggestions && suggestions.length > 0) {
                console.log('Showing contextual suggestions:', suggestions);
                showSuggestionChips(suggestions);
            } else {
                console.log('No suggestions generated for this content');
            }
        }

        function showContextualSuggestions(content) {
            // Hide any existing chips first
            hideSuggestionChips();
            
            // Only show suggestions for certain types of responses
            const lowerContent = content.toLowerCase();
            
            // Don't show suggestions if we're in a parameter collection phase
            if (isParameterCollectionPhase(content)) {
                console.log('Skipping suggestions - in parameter collection phase');
                return;
            }
            
            // Show suggestions after deployment starts or completes
            if ((lowerContent.includes('deploying') && lowerContent.includes('cluster')) || 
                (lowerContent.includes('deployment') && (lowerContent.includes('successful') || lowerContent.includes('completed'))) ||
                (lowerContent.includes("i'm deploying") || lowerContent.includes("deploying now"))) {
                showSuggestionChips([
                    { text: '📋 List deployed resources', action: 'list', type: 'primary' },
                    { text: '🔍 Check resource status', action: 'status', type: 'secondary' },
                    { text: '🔧 Create another resource', action: 'create', type: 'secondary' },
                    { text: '� View deployment details', action: 'details', type: 'secondary' }
                ]);
            }
            // Show suggestions after listing resources
            else if (lowerContent.includes('resource group') && (lowerContent.includes('found') || lowerContent.includes('list'))) {
                showSuggestionChips([
                    { text: '➕ Create new resource', action: 'create', type: 'primary' },
                    { text: '🔍 Get resource details', action: 'details', type: 'secondary' },
                    { text: '📊 Show resource costs', action: 'costs', type: 'secondary' }
                ]);
            }
            // Show suggestions only after terraform template is actually displayed (has terraform-block)
            else if (document.querySelector('.terraform-block') && lowerContent.includes('terraform')) {
                showSuggestionChips([
                    { text: '🚀 Deploy this template', action: 'deploy', type: 'primary' },
                    { text: '✏️ Modify template', action: 'edit', type: 'edit' },
                    { text: '💡 Explain template', action: 'explain', type: 'secondary' }
                ]);
            }
            // Show suggestions for error messages
            else if (lowerContent.includes('error') || lowerContent.includes('failed')) {
                showSuggestionChips([
                    { text: '🔄 Try again', action: 'retry', type: 'primary' },
                    { text: '❓ Get help', action: 'help', type: 'secondary' },
                    { text: '📋 Check prerequisites', action: 'prereq', type: 'secondary' }
                ]);
            }
        }

        function generateContextualChips(content, lowerContent) {
            console.log('generateContextualChips called with:', lowerContent.substring(0, 100));
            
            // Only show deployment chips for ACTUAL deployment scenarios (not planning)
            if ((lowerContent.includes('deploying your') && lowerContent.includes('started')) || 
                (lowerContent.includes('deployment') && lowerContent.includes('successful')) ||
                (lowerContent.includes('deployment') && lowerContent.includes('completed')) ||
                (lowerContent.includes("i'm deploying") && lowerContent.includes('now')) ||
                lowerContent.includes('deployment process now')) {
                
                console.log('Actual deployment scenario detected, returning deployment chips');
                return [
                    { text: 'Check the status of my deployed resources', action: 'status', type: 'primary' },
                    { text: 'Show me what resources were created', action: 'list', type: 'secondary' },
                    { text: 'Create another resource type', action: 'create', type: 'secondary' },
                    { text: 'Monitor the deployment progress', action: 'details', type: 'secondary' }
                ];
            }
            
            // Show deployment confirmation chips when asking for deploy confirmation
            if (lowerContent.includes('would you like me to deploy')) {
                console.log('Deploy confirmation detected, returning confirmation chips');
                return [
                    { text: 'Yes, deploy this configuration', action: 'deploy-yes', type: 'primary' },
                    { text: 'Let me modify it first', action: 'deploy-modify', type: 'secondary' },
                    { text: 'Explain what will be created', action: 'deploy-explain', type: 'secondary' },
                    { text: 'Cancel deployment', action: 'deploy-cancel', type: 'secondary' }
                ];
            }
            
            // Show suggestions after listing resources
            if (lowerContent.includes('resource group') && (lowerContent.includes('found') || lowerContent.includes('list'))) {
                return [
                    { text: 'Create a virtual machine', action: 'create', type: 'primary' },
                    { text: 'Show details for a specific resource', action: 'details', type: 'secondary' },
                    { text: 'Estimate costs for these resources', action: 'costs', type: 'secondary' }
                ];
            }
            
            // Show suggestions when terraform template is displayed (only after showing template)
            if (document.querySelector('.terraform-block') && lowerContent.includes('terraform')) {
                const suggestions = [
                    { text: 'Deploy this configuration', action: 'deploy', type: 'primary' }
                ];
                
                // Add context-specific modification suggestions
                if (lowerContent.includes('kubernetes') || lowerContent.includes('aks')) {
                    suggestions.push(
                        { text: 'Change the node count', action: 'modify-nodes', type: 'secondary' },
                        { text: 'Use a different instance size', action: 'modify-size', type: 'secondary' },
                        { text: 'Add auto-scaling', action: 'add-autoscaling', type: 'secondary' }
                    );
                } else if (lowerContent.includes('virtual machine') || lowerContent.includes('vm')) {
                    suggestions.push(
                        { text: 'Change the VM size', action: 'modify-vmsize', type: 'secondary' },
                        { text: 'Add data disks', action: 'add-disks', type: 'secondary' },
                        { text: 'Configure networking', action: 'modify-network', type: 'secondary' }
                    );
                } else if (lowerContent.includes('storage')) {
                    suggestions.push(
                        { text: 'Change storage tier', action: 'modify-tier', type: 'secondary' },
                        { text: 'Add backup policy', action: 'add-backup', type: 'secondary' },
                        { text: 'Configure access permissions', action: 'modify-access', type: 'secondary' }
                    );
                } else {
                    suggestions.push(
                        { text: 'Modify resource settings', action: 'edit', type: 'secondary' },
                        { text: 'Explain this configuration', action: 'explain', type: 'secondary' }
                    );
                }
                
                return suggestions;
            }
            
            // Show suggestions for follow-up questions or discussions (but NOT during planning)
            if ((lowerContent.includes('would you like') || lowerContent.includes('what would you prefer') || 
                lowerContent.includes('any other') || lowerContent.includes('anything else')) &&
                !lowerContent.includes('deploy')) {  // Don't show generic suggestions when asking about deployment
                
                // Context-aware follow-up suggestions
                if (lowerContent.includes('cluster') || lowerContent.includes('kubernetes')) {
                    return [
                        { text: 'Add monitoring and logging', action: 'add-monitoring', type: 'primary' },
                        { text: 'Configure ingress controller', action: 'add-ingress', type: 'secondary' },
                        { text: 'Set up CI/CD pipeline', action: 'setup-cicd', type: 'secondary' },
                        { text: 'Add security policies', action: 'add-security', type: 'secondary' }
                    ];
                } else if (lowerContent.includes('database') || lowerContent.includes('sql')) {
                    return [
                        { text: 'Configure backup retention', action: 'setup-backup', type: 'primary' },
                        { text: 'Set up high availability', action: 'setup-ha', type: 'secondary' },
                        { text: 'Configure firewall rules', action: 'setup-firewall', type: 'secondary' },
                        { text: 'Enable threat detection', action: 'enable-security', type: 'secondary' }
                    ];
                } else {
                    return [
                        { text: 'Add security configurations', action: 'add-security', type: 'primary' },
                        { text: 'Set up monitoring', action: 'add-monitoring', type: 'secondary' },
                        { text: 'Configure backup', action: 'setup-backup', type: 'secondary' },
                        { text: 'Review best practices', action: 'review-practices', type: 'secondary' }
                    ];
                }
            }
            
            // Show suggestions for error messages
            if (lowerContent.includes('error') || lowerContent.includes('failed')) {
                return [
                    { text: 'Try the deployment again', action: 'retry', type: 'primary' },
                    { text: 'Help me troubleshoot this issue', action: 'help', type: 'secondary' },
                    { text: 'Check the prerequisites', action: 'prereq', type: 'secondary' },
                    { text: 'Use a different region', action: 'change-region', type: 'secondary' }
                ];
            }
            
            // Default suggestions for general conversations (but only for very generic responses)
            if ((lowerContent.includes('great') || lowerContent.includes('perfect')) && 
                !lowerContent.includes('cluster') && !lowerContent.includes('resource') && !lowerContent.includes('deploy')) {
                return [
                    { text: 'Create an AKS cluster', action: 'create-aks', type: 'primary' },
                    { text: 'List my resource groups', action: 'list-rg', type: 'secondary' },
                    { text: 'Show Azure best practices', action: 'best-practices', type: 'secondary' }
                ];
            }
            
            // Don't show suggestions for planning conversations
            console.log('No contextual suggestions for this content type');
            return null;
        }

        function isParameterCollectionPhase(content) {
            const lowerContent = content.toLowerCase();
            
            // More specific parameter collection indicators
            const parameterIndicators = [
                'please provide the',
                'enter the value for',
                'what should i name',
                'which region would you like',
                'specify a name for',
                'choose a location for',
                'what would you like to name the',
                'please specify the',
                'i need to know the',
                'what value should i use for'
            ];
            
            // Check for question patterns that expect user input
            const questionPatterns = [
                'please provide',
                'what would you like',
                'do you want me to',
                'should i use',
                'would you prefer'
            ];
            
            // Only return true if it's clearly asking for parameter input
            const hasParameterIndicator = parameterIndicators.some(indicator => lowerContent.includes(indicator));
            const hasQuestionAndWaiting = questionPatterns.some(pattern => lowerContent.includes(pattern)) && 
                                        (lowerContent.includes('name') || lowerContent.includes('region') || lowerContent.includes('location'));
            
            return hasParameterIndicator || hasQuestionAndWaiting;
        }

        function formatMessageContent(content) {
            // Ensure content is defined and is a string
            if (!content || typeof content !== 'string') {
                return content || '';
            }
            
            // Check if content already contains HTML tags (from backend)
            const containsHtml = /<[^>]+>/.test(content);
            
            // Debug: Check if we have any code blocks at all
            if (content.includes('```')) {
                console.log('DEBUG: Found code blocks in content');
                console.log('DEBUG: Content preview:', content.substring(0, 300));
                console.log('DEBUG: Looking for hcl pattern...');
                
                // Test the regex pattern
                const hclMatch = content.match(/```(?:terraform|hcl)\n([\s\S]*?)\n```/g);
                if (hclMatch) {
                    console.log('DEBUG: HCL regex matched:', hclMatch.length, 'blocks');
                } else {
                    console.log('DEBUG: HCL regex did NOT match');
                    // Try to find what patterns exist
                    const allMatches = content.match(/```([^`]+)```/g);
                    if (allMatches) {
                        console.log('DEBUG: Found these code patterns:', allMatches);
                    }
                }
            }
            
            console.log('🔍 Content contains HTML:', containsHtml);
            console.log('🔍 Sample content:', content.substring(0, 200));
            
            // TERRAFORM/HCL PROCESSING FIRST (before general code blocks)
            
            // Try multiple HCL/Terraform patterns to catch different formats
            
            // Pattern 1: ```hcl\ncontent\n```
            content = content.replace(/```hcl\n([\s\S]*?)\n```/g, (match, code) => {
                console.log('DEBUG: Pattern 1 matched - ```hcl\\n...\\n```');
                const codeId = 'terraform-' + Math.random().toString(36).substr(2, 9);
                return `<div class="code-container">
                    <div class="code-header">
                        <span class="code-language">Terraform</span>
                        <div class="code-actions">
                            <button class="action-btn edit-btn" onclick="requestTerraformEdit('${codeId}')">
                                <span class="btn-icon">✏️</span>
                                <span class="btn-text">Edit</span>
                            </button>
                            <button class="action-btn deploy-btn" onclick="requestTerraformApply('${codeId}')">
                                <span class="btn-icon">🚀</span>
                                <span class="btn-text">Apply</span>
                            </button>
                            <button class="action-btn copy-btn" onclick="copyTerraformCode('${codeId}')">
                                <span class="btn-icon">📋</span>
                                <span class="btn-text">Copy</span>
                            </button>
                        </div>
                    </div>
                    <pre class="terraform-block" id="${codeId}" data-raw-code="${escapeHtml(code.trim())}">${formatTerraformCode(code.trim())}</pre>
                </div>`;
            });
            
            // Pattern 2: ```terraform\ncontent\n```
            content = content.replace(/```terraform\n([\s\S]*?)\n```/g, (match, code) => {
                console.log('DEBUG: Pattern 2 matched - ```terraform\\n...\\n```');
                const codeId = 'terraform-' + Math.random().toString(36).substr(2, 9);
                return `<div class="code-container">
                    <div class="code-header">
                        <span class="code-language">Terraform</span>
                        <div class="code-actions">
                            <button class="action-btn edit-btn" onclick="requestTerraformEdit('${codeId}')">
                                <span class="btn-icon">✏️</span>
                                <span class="btn-text">Edit</span>
                            </button>
                            <button class="action-btn deploy-btn" onclick="requestTerraformApply('${codeId}')">
                                <span class="btn-icon">🚀</span>
                                <span class="btn-text">Apply</span>
                            </button>
                            <button class="action-btn copy-btn" onclick="copyTerraformCode('${codeId}')">
                                <span class="btn-icon">📋</span>
                                <span class="btn-text">Copy</span>
                            </button>
                        </div>
                    </div>
                    <pre class="terraform-block" id="${codeId}" data-raw-code="${escapeHtml(code.trim())}">${formatTerraformCode(code.trim())}</pre>
                </div>`;
            });
            
            // Pattern 3: ```hcl without newline after
            content = content.replace(/```hcl([\s\S]*?)```/g, (match, code) => {
                console.log('DEBUG: Pattern 3 matched - ```hcl...```');
                const codeId = 'terraform-' + Math.random().toString(36).substr(2, 9);
                return `<div class="code-container">
                    <div class="code-header">
                        <span class="code-language">Terraform</span>
                        <div class="code-actions">
                            <button class="action-btn edit-btn" onclick="requestTerraformEdit('${codeId}')">
                                <span class="btn-icon">✏️</span>
                                <span class="btn-text">Edit</span>
                            </button>
                            <button class="action-btn deploy-btn" onclick="requestTerraformApply('${codeId}')">
                                <span class="btn-icon">🚀</span>
                                <span class="btn-text">Apply</span>
                            </button>
                            <button class="action-btn copy-btn" onclick="copyTerraformCode('${codeId}')">
                                <span class="btn-icon">📋</span>
                                <span class="btn-text">Copy</span>
                            </button>
                        </div>
                    </div>
                    <pre class="terraform-block" id="${codeId}" data-raw-code="${escapeHtml(code.trim())}">${formatTerraformCode(code.trim())}</pre>
                </div>`;
            });
            
            // Check for terraform blocks without explicit language tag but with terraform syntax
            content = content.replace(/```\n([\s\S]*?terraform\s*\{[\s\S]*?)\n```/g, (match, code) => {
                const codeId = 'terraform-' + Math.random().toString(36).substr(2, 9);
                return `<div class="code-container">
                    <div class="code-header">
                        <span class="code-language">Terraform</span>
                        <div class="code-actions">
                            <button class="action-btn edit-btn" onclick="requestTerraformEdit('${codeId}')">
                                <span class="btn-icon">✏️</span>
                                <span class="btn-text">Edit</span>
                            </button>
                            <button class="action-btn deploy-btn" onclick="requestTerraformApply('${codeId}')">
                                <span class="btn-icon">🚀</span>
                                <span class="btn-text">Apply</span>
                            </button>
                            <button class="action-btn copy-btn" onclick="copyTerraformCode('${codeId}')">
                                <span class="btn-icon">📋</span>
                                <span class="btn-text">Copy</span>
                            </button>
                        </div>
                    </div>
                    <pre class="terraform-block" id="${codeId}" data-raw-code="${escapeHtml(code.trim())}">${formatTerraformCode(code.trim())}</pre>
                </div>`;
            });
            
            // Check for terraform blocks that start with provider, resource, etc.
            content = content.replace(/```\n((?:provider|resource|data|variable|output|terraform|locals|module)\s+[\s\S]*?)\n```/g, (match, code) => {
                const codeId = 'terraform-' + Math.random().toString(36).substr(2, 9);
                return `<div class="code-container">
                    <div class="code-header">
                        <span class="code-language">Terraform</span>
                        <div class="code-actions">
                            <button class="action-btn edit-btn" onclick="requestTerraformEdit('${codeId}')">
                                <span class="btn-icon">✏️</span>
                                <span class="btn-text">Edit</span>
                            </button>
                            <button class="action-btn deploy-btn" onclick="requestTerraformApply('${codeId}')">
                                <span class="btn-icon">🚀</span>
                                <span class="btn-text">Apply</span>
                            </button>
                            <button class="action-btn copy-btn" onclick="copyTerraformCode('${codeId}')">
                                <span class="btn-icon">📋</span>
                                <span class="btn-text">Copy</span>
                            </button>
                        </div>
                    </div>
                    <pre class="terraform-block" id="${codeId}" data-raw-code="${escapeHtml(code.trim())}">${formatTerraformCode(code.trim())}</pre>
                </div>`;
            });
            
            // Bash/Shell code blocks (specific languages first)
            content = content.replace(/```(?:bash|shell|sh)\n([\s\S]*?)\n```/g, (match, code) => {
                return `<pre class="bash-block"><code>${escapeHtml(code.trim())}</code></pre>`;
            });
            
            // JSON code blocks
            content = content.replace(/```(?:json)\n([\s\S]*?)\n```/g, (match, code) => {
                try {
                    const parsed = JSON.parse(code.trim());
                    const formatted = JSON.stringify(parsed, null, 2);
                    return `<pre class="json-block"><code>${escapeHtml(formatted)}</code></pre>`;
                } catch {
                    return `<pre class="json-block"><code>${escapeHtml(code.trim())}</code></pre>`;
                }
            });
            
            // Regular code blocks (AFTER all specific language processing)
            content = content.replace(/```(\w+)?\n([\s\S]*?)\n```/g, (match, lang, code) => {
                // Check if this looks like logs content (contains timestamps, log levels, etc.)
                const isLogsContent = code.includes('time=') && code.includes('level=') && code.includes('msg=');
                if (isLogsContent) {
                    return `<pre class="logs-block"><code>${escapeHtml(code.trim())}</code></pre>`;
                }
                return `<pre><code>${escapeHtml(code.trim())}</code></pre>`;
            });
            
            // Convert inline code (only if not already HTML)
            if (!containsHtml) {
                content = content.replace(/`([^`]+)`/g, '<code>$1</code>');
            }
            
            // Convert **bold** to <strong> (only if not already HTML)
            if (!containsHtml) {
                content = content.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
            }
            
            // Convert line breaks (only if not already HTML)
            if (!containsHtml) {
                content = content.replace(/\n/g, '<br>');
            }
            
            // Debug: Check what content looks like before making clickable
            if (content.includes('NAME') || content.includes('STATUS') || content.includes('Pod') || content.includes('Service')) {
                console.log('🔍 Detected list-like content before makeClickable:', content.substring(0, 200));
            }
            
            // Make agent output clickable (lists, resources, etc.)
            content = makeAgentOutputClickable(content);
            
            return content;
        }

        function formatTerraformCode(code) {
            // Enhanced Terraform/HCL syntax highlighting
            let formatted = escapeHtml(code);
            
            // Keywords (primary) - make sure these match first
            formatted = formatted.replace(/\b(terraform|provider|resource|data|variable|output|locals|module|count|for_each|depends_on|random_id)\b/g, 
                '<span class="terraform-keyword">$1</span>');
            
            // Resource types (Azure resources and others)
            formatted = formatted.replace(/"(azurerm_[a-zA-Z_]+|random_[a-zA-Z_]+)"/g, 
                '"<span class="terraform-resource">$1</span>"');
            
            // Block identifiers FIRST (resource names, variable names, etc.) - most specific patterns first
            formatted = formatted.replace(/^(\s*)(resource|data|variable|output|locals|module)(\s+)"([^"]+)"(\s+)"([^"]+)"/gm, 
                '$1<span class="terraform-keyword">$2</span>$3<span class="terraform-string">"$4"</span>$5<span class="terraform-value">"$6"</span>');
            
            // Properties/attributes (before = sign) - be more specific to avoid conflicts
            formatted = formatted.replace(/^(\s*)([a-zA-Z_][a-zA-Z0-9_]*)\s*=/gm, 
                '$1<span class="terraform-property">$2</span> =');
            
            // Function calls and expressions
            formatted = formatted.replace(/\b([a-zA-Z_][a-zA-Z0-9_]*)\s*\(/g, 
                '<span class="terraform-value">$1</span>(');
            
            // Strings - be more careful about existing spans
            formatted = formatted.replace(/"([^"]*)"(?![^<]*<\/span>)/g, 
                '<span class="terraform-string">"$1"</span>');
            
            // Numbers
            formatted = formatted.replace(/\b(\d+)\b(?![^<]*<\/span>)/g, 
                '<span class="terraform-number">$1</span>');
            
            // Boolean values
            formatted = formatted.replace(/\b(true|false|null)\b(?![^<]*<\/span>)/g, 
                '<span class="terraform-boolean">$1</span>');
            
            // Comments
            formatted = formatted.replace(/(#.*$)/gm, 
                '<span class="terraform-comment">$1</span>');
            
            return formatted;
        }

        // Add click handlers for deployment options after content is loaded
        document.addEventListener('DOMContentLoaded', function() {
            // Use event delegation to handle clicks ONLY on specific deployment options
            document.body.addEventListener('click', function(e) {
                const clickedText = e.target.textContent || e.target.innerText;
                
                // Only handle clicks on SPECIFIC deployment action lines (more precise matching)
                if (e.target.closest('.message-content')) {
                    // Check for exact deployment option patterns
                    const deployPattern = /^•\s*✅\s*Deploy:\s*Execute terraform/i;
                    const editPattern = /^•\s*✏️\s*Edit:\s*Modify the template/i;
                    const cancelPattern = /^•\s*❌\s*Cancel:\s*Abort this deployment/i;
                    
                    if (deployPattern.test(clickedText.trim())) {
                        console.log('🚀 Deploy action clicked');
                        e.preventDefault();
                        e.stopPropagation();
                        const chatInput = document.getElementById('chatInput');
                        if (chatInput) {
                            chatInput.value = 'Deploy the terraform template';
                            chatInput.focus();
                            console.log('✅ Deploy text added to chat input');
                        }
                        return;
                    }
                    
                    if (editPattern.test(clickedText.trim())) {
                        console.log('✏️ Edit action clicked');
                        e.preventDefault();
                        e.stopPropagation();
                        const chatInput = document.getElementById('chatInput');
                        if (chatInput) {
                            chatInput.value = 'Edit the terraform template';
                            chatInput.focus();
                            console.log('✅ Edit text added to chat input');
                        }
                        return;
                    }
                    
                    if (cancelPattern.test(clickedText.trim())) {
                        console.log('❌ Cancel action clicked');
                        e.preventDefault();
                        e.stopPropagation();
                        const chatInput = document.getElementById('chatInput');
                        if (chatInput) {
                            chatInput.value = 'Cancel this deployment';
                            chatInput.focus();
                            console.log('✅ Cancel text added to chat input');
                        }
                        return;
                    }
                }
            });
            
            // Also add hover effects for deployment options
            document.body.addEventListener('mouseover', function(e) {
                const clickedText = e.target.textContent || e.target.innerText;
                if (clickedText.includes('Deploy:') || clickedText.includes('Edit:') || clickedText.includes('Cancel:')) {
                    e.target.style.cursor = 'pointer';
                    e.target.style.backgroundColor = '#f0f8ff';
                    e.target.style.textDecoration = 'underline';
                }
            });
            
            document.body.addEventListener('mouseout', function(e) {
                const clickedText = e.target.textContent || e.target.innerText;
                if (clickedText.includes('Deploy:') || clickedText.includes('Edit:') || clickedText.includes('Cancel:')) {
                    e.target.style.backgroundColor = '';
                    e.target.style.textDecoration = '';
                }
            });
        });

        function handleActionClick(action, element) {
            console.log('🎯 Action clicked:', action);
            
            // Visual feedback
            element.style.backgroundColor = '#e6f3ff';
            element.classList.add('clicked');
            
            // Create a command based on the action
            let command = '';
            if (action === 'Deploy') {
                command = 'Deploy the terraform template';
            } else if (action === 'Edit') {
                command = 'Edit the terraform template';
            } else if (action === 'Cancel') {
                command = 'Cancel this deployment';
            }
            
            // Send the command
            if (command) {
                setTimeout(() => {
                    sendMessage(command);
                }, 100);
            }
        }

        // Specific deployment action functions
        function setDeployCommand() {
            console.log('🚀 Deploy action clicked');
            const chatInput = document.getElementById('chatInput');
            if (chatInput) {
                chatInput.value = 'Deploy the terraform template';
                chatInput.focus();
                console.log('✅ Deploy command set in chat input');
            }
        }

        function setEditCommand() {
            console.log('✏️ Edit action clicked');
            
            // Try to find the most recent Terraform code block
            const terraformBlocks = document.querySelectorAll('.terraform-block');
            if (terraformBlocks.length > 0) {
                // Use the most recent (last) Terraform block
                const lastTerraformBlock = terraformBlocks[terraformBlocks.length - 1];
                const codeId = lastTerraformBlock.id;
                console.log('Found Terraform block with ID:', codeId);
                
                // Use the existing requestTerraformEdit function
                requestTerraformEdit(codeId);
            } else {
                // Fallback: Set a more specific message if no Terraform found
                const chatInput = document.getElementById('chatInput');
                if (chatInput) {
                    chatInput.value = 'I want to edit my Terraform configuration. Please show me the current template first.';
                    chatInput.focus();
                    console.log('✅ No Terraform found, set fallback message');
                }
            }
        }

        function setCancelCommand() {
            console.log('❌ Cancel action clicked');
            const chatInput = document.getElementById('chatInput');
            if (chatInput) {
                chatInput.value = 'Cancel this deployment';
                chatInput.focus();
                console.log('✅ Cancel command set in chat input');
            }
        }

        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        function makeAgentOutputClickable(content) {
            // Don't process if already processed (avoid double processing)
            if (content.includes('clickable-line') || content.includes('data-text=')) {
                return content;
            }
            
            // Check for logs output specifically
            if (content.includes('📋 **Logs for Pod:') || content.includes('Lines: 50') || content.includes('Pod Logs:')) {
                // This is a logs output, don't make it clickable
                return content;
            }
            
            // Targeted approach: Look for specific deployment action patterns and make them clickable
            if (content.includes('Ready for Deployment') || content.includes('Deploy:') || content.includes('Edit:') || content.includes('Cancel:')) {
                
                // Target specific deployment option patterns with rounded boxes
                content = content.replace(
                    /(•\s*✅\s*<strong>Deploy<\/strong>:\s*[^<\n]+)/g,
                    '<span class="deployment-action-clickable" onclick="setDeployCommand()" data-action="deploy">$1</span>'
                );
                
                content = content.replace(
                    /(•\s*✏️\s*<strong>Edit<\/strong>:\s*[^<\n]+)/g,
                    '<span class="deployment-action-clickable" onclick="setEditCommand()" data-action="edit">$1</span>'
                );
                
                content = content.replace(
                    /(•\s*❌\s*<strong>Cancel<\/strong>:\s*[^<\n]+)/g,
                    '<span class="deployment-action-clickable" onclick="setCancelCommand()" data-action="cancel">$1</span>'
                );
                
                return content;
            }
            
            // Check for Kubernetes resource listings - simplified detection
            const isKubernetesListing = 
                (content.includes('�') && content.includes('pods in cluster')) ||
                (content.includes('🎯') && content.includes('Deployments in cluster')) ||
                (content.includes('🎯') && content.includes('Services in cluster')) ||
                (content.includes('🎯') && content.includes('Namespaces in cluster')) ||
                (content.includes('Found') && content.includes('in cluster'));

            if (isKubernetesListing) {
                let processedContent = content;
                
                // Determine what type of resource listing this is and handle accordingly
                let resourceType = 'pod'; // default
                let resourceAction = 'Get logs from';
                
                if (content.includes('deployments in cluster') || content.includes('📦') && content.includes('deployments')) {
                    resourceType = 'deployment';
                    resourceAction = 'Describe deployment';
                } else if (content.includes('services in cluster') || content.includes('🌐')) {
                    resourceType = 'service';
                    resourceAction = 'Describe service';
                } else if (content.includes('namespaces in cluster') || content.includes('Namespaces in cluster') || 
                          (content.includes('�') && content.includes('Total namespaces:'))) {
                    resourceType = 'namespace';
                    resourceAction = 'List pods in namespace';
                } else if (content.includes('configmaps in cluster') || content.includes('📝') && content.includes('configmaps')) {
                    resourceType = 'configmap';
                    resourceAction = 'Describe configmap';
                } else if (content.includes('secrets in cluster') || content.includes('🔐') && content.includes('secrets')) {
                    resourceType = 'secret';
                    resourceAction = 'Describe secret';
                } else if (content.includes('ingress in cluster') || content.includes('🔗') && content.includes('ingress')) {
                    resourceType = 'ingress';
                    resourceAction = 'Describe ingress';
                } else if (content.includes('jobs in cluster') || content.includes('⚙️') && content.includes('jobs')) {
                    resourceType = 'job';
                    resourceAction = 'Describe job';
                } else if (content.includes('cronjobs in cluster') || content.includes('⏰') && content.includes('cronjobs')) {
                    resourceType = 'cronjob';
                    resourceAction = 'Describe cronjob';
                }
                
                // Handle resource names in <code> tags: <code>resource-name</code>
                const codeTagPattern = /<code>([a-z0-9][\w\-\.]*[a-z0-9])<\/code>/g;
                processedContent = processedContent.replace(codeTagPattern, (match, resourceName) => {
                    const uniqueId = 'clickable_' + Math.random().toString(36).substr(2, 9);
                    const smartCommand = `${resourceAction} ${resourceName}`;
                    return `<span class="clickable-line ${resourceType}-name" id="${uniqueId}" data-text="${escapeForAttribute(smartCommand)}" onclick="appendToChatById('${uniqueId}')" title="Click to ${resourceAction.toLowerCase()} ${resourceName}" style="color: #007acc; cursor: pointer; text-decoration: underline;"><code>${escapeHtml(resourceName)}</code></span>`;
                });
                
                // Handle emoji-based format like "📦 default" 
                const emojiResourcePattern = /(📦|🌐|📋)\s+([a-z0-9][\w\-\.]*[a-z0-9]|default|kube-system|kube-public|kube-node-lease)/g;
                processedContent = processedContent.replace(emojiResourcePattern, (match, emoji, resourceName) => {
                    const uniqueId = 'clickable_' + Math.random().toString(36).substr(2, 9);
                    const smartCommand = `${resourceAction} ${resourceName}`;
                    return `${emoji} <span class="clickable-line ${resourceType}-name" id="${uniqueId}" data-text="${escapeForAttribute(smartCommand)}" onclick="appendToChatById('${uniqueId}')" title="Click to ${resourceAction.toLowerCase()} ${resourceName}" style="color: #007acc; cursor: pointer; text-decoration: underline;">${escapeHtml(resourceName)}</span>`;
                });
                
                return `<div class="${resourceType}-listing">${processedContent}</div>`;
            }
            
            // For other content, return as-is
            return content;
        }

        function formatJsonCode(code) {
            try {
                const parsed = JSON.parse(code);
                return JSON.stringify(parsed, null, 2);
            } catch (e) {
                return code; // Return as-is if parsing fails
            }
        }

        function escapeForAttribute(text) {
            // More robust escaping for HTML attributes
            return text.replace(/&/g, '&amp;')
                      .replace(/"/g, '&quot;')
                      .replace(/'/g, '&#39;')
                      .replace(/</g, '&lt;')
                      .replace(/>/g, '&gt;')
                      .replace(/\n/g, ' ')
                      .replace(/\r/g, '')
                      .replace(/\t/g, ' ');
        }

        function appendToChatById(elementId) {
            console.log('�️ appendToChatById called with ID:', elementId);
            
            // Get the element and extract the text from data attribute
            const element = document.getElementById(elementId);
            if (element && element.dataset.text) {
                const textToAppend = element.dataset.text;
                console.log('📝 Appending text to chat:', textToAppend);
                
                // Set the input field value and submit
                const messageInput = document.getElementById('messageInput');
                if (messageInput) {
                    messageInput.value = textToAppend;
                    
                    // Trigger the submit button click or form submission
                    const submitButton = document.querySelector('button[type="submit"]');
                    if (submitButton) {
                        submitButton.click();
                    }
                }
            } else {
                console.error('Element not found or missing data-text attribute:', elementId);
            }
        }

        function appendToChat(text) {
                        console.log('🎯 Found pod name match:', match[1]);
                    }
                    console.log('🔍 Total matches found:', matches.length);
                    
                    // Reset regex for actual replacement
                    const podNameRegexForReplace = /([a-z0-9][\w\-]*[a-z0-9])<br>(?=<span class="pod-status-arrow"|└→)/g;
                    processedContent = processedContent.replace(podNameRegexForReplace, (match, podName) => {
                        // Only make it clickable if it looks like a valid pod name
                        if (podName.length > 3 && 
                            (podName.includes('-') || podName.includes('azure') || podName.includes('kube') || podName.includes('coredns') || podName.includes('metrics') || podName.includes('defender')) &&
                            !podName.includes('Namespace') && !podName.includes('Status') && !podName.includes('Pods')) {
                            
                            console.log('🎯 Making <br> pod name clickable:', podName);
                            
                            // Generate unique ID for this clickable element
                            const uniqueId = 'clickable_' + Math.random().toString(36).substr(2, 9);
                            const smartCommand = `Get logs from pod ${podName}`;
                            
                            return `<span class="clickable-line pod-name" id="${uniqueId}" data-text="${escapeForAttribute(smartCommand)}" onclick="appendToChatById('${uniqueId}')" title="Click to get logs for ${podName}">${podName}</span> `;
                        }
                        return match; // Return original if it doesn't look like a pod name
                    });
                    
                    // If no matches, try a simpler approach
                    if (matches.length === 0) {
                        console.log('🔄 No regex matches found, trying simpler approach');
                        
                        // Split by <br> and look for pod names
                        const parts = content.split('<br>');
                        for (let i = 0; i < parts.length; i++) {
                            const part = parts[i].trim();
                            console.log('🔍 Checking part:', part);
                            
                            // Check if this part looks like a pod name
                            if (part.length > 3 && part.match(/^[a-z0-9][\w\-]*[a-z0-9]$/) && 
                                (part.includes('-') || part.includes('azure') || part.includes('kube'))) {
                                // Add pod name handling here if needed
                            }
                        }
                    }
                }
            }
        }

        function formatJsonCode(code) {
            try {
                const parsed = JSON.parse(code);
                return JSON.stringify(parsed, null, 2);
            } catch (e) {
                return code; // Return as-is if parsing fails
            }
        }

        function escapeForAttribute(text) {
            // More robust escaping for HTML attributes
            return text.replace(/&/g, '&amp;')
                      .replace(/"/g, '&quot;')
                      .replace(/'/g, '&#39;')
                      .replace(/</g, '&lt;')
                      .replace(/>/g, '&gt;')
                      .replace(/\n/g, ' ')
                      .replace(/\r/g, '')
                      .replace(/\t/g, ' ');
        }

        function appendToChatById(elementId) {
            console.log('🖱️ appendToChatById called with ID:', elementId);
            
            // Get the element and extract the text from data attribute
            const element = document.getElementById(elementId);
            if (!element) {
                console.error('❌ Element not found:', elementId);
                return;
            }
            
            console.log('✅ Element found:', element);
            
            const text = element.getAttribute('data-text');
            if (!text) {
                console.error('❌ No data-text found for element:', elementId);
                return;
            }
            
            console.log('📝 Text to append:', text);
            
            // Get the chat input element
            const chatInput = document.getElementById('chatInput');
            if (chatInput) {
                console.log('✅ Chat input found');
                
                // If there's existing text, add a space before appending
                const currentText = chatInput.value.trim();
                const newText = currentText ? `${currentText} ${text}` : text;
                chatInput.value = newText;
                
                console.log('✅ Text set successfully:', newText);
                
                // Focus on the input
                chatInput.focus();
                
                // Position cursor at the end
                chatInput.setSelectionRange(newText.length, newText.length);
                
                // Optional: Auto-scroll the input into view
                chatInput.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                
                // Visual feedback - green flash
                chatInput.style.backgroundColor = '#e8f5e8';
                chatInput.style.borderColor = '#16c60c';
                setTimeout(() => {
                    chatInput.style.backgroundColor = '';
                    chatInput.style.borderColor = '';
                }, 500);

                // Add success animation to clicked element
                element.classList.add('clicked');
                setTimeout(() => {
                    element.classList.remove('clicked');
                }, 500);

                // Show a brief notification
                showNotification(`Added "${text}" to chat input`, 'success');
            } else {
                console.error('❌ Chat input element not found! Looking for ID: chatInput');
                console.log('Available elements:', document.querySelectorAll('input, textarea'));
            }
        }

        function appendToChat(text) {
            // Legacy function - kept for backward compatibility
            console.warn('appendToChat called directly - use appendToChatById instead');
            
            // Get the chat input element
            const chatInput = document.getElementById('chatInput');
            if (chatInput) {
                console.log('✅ Chat input found (legacy function)');
                
                // If there's existing text, add a space before appending
                const currentText = chatInput.value.trim();
                const newText = currentText ? `${currentText} ${text}` : text;
                chatInput.value = newText;
                
                // Focus on the input
                chatInput.focus();
                
                // Position cursor at the end
                chatInput.setSelectionRange(newText.length, newText.length);
                
                // Optional: Auto-scroll the input into view
                chatInput.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                
                // Visual feedback - green flash
                chatInput.style.backgroundColor = '#e8f5e8';
                chatInput.style.borderColor = '#16c60c';
                setTimeout(() => {
                    chatInput.style.backgroundColor = '';
                    chatInput.style.borderColor = '';
                }, 500);

                // Show a brief notification
                showNotification(`Added "${text}" to chat input`, 'success');
            }
        }

        function showNotification(message, type = 'info') {
            // Create notification element
            const notification = document.createElement('div');
            notification.className = `notification notification-${type}`;
            notification.innerHTML = `
                <div class="notification-content">
                    <span class="notification-icon">${type === 'success' ? '✅' : 'ℹ️'}</span>
                    <span class="notification-text">${message}</span>
                </div>
            `;
            
            // Add to body
            document.body.appendChild(notification);
            
            // Show notification
            setTimeout(() => {
                notification.classList.add('show');
            }, 10);
            
            // Remove notification after delay
            setTimeout(() => {
                notification.classList.remove('show');
                setTimeout(() => {
                    if (notification.parentNode) {
                        notification.parentNode.removeChild(notification);
                    }
                }, 300);
            }, 2000);
        }

        // Simple Edit and Apply functionality
        function requestTerraformEdit(codeId) {
            const codeBlock = document.getElementById(codeId);
            if (!codeBlock) {
                console.error('Terraform code block not found');
                return;
            }

            const rawCode = codeBlock.getAttribute('data-raw-code');
            if (!rawCode) {
                console.error('No raw code found');
                return;
            }

            // Store the current Terraform code for context
            window.currentTerraformContext = rawCode;
            window.currentTerraformCodeId = codeId;

            // Start a conversational edit session
            const editMessage = `🛠️ **Edit Terraform Configuration**\n\n` +
                `I'll help you modify this Terraform configuration. What changes would you like to make?\n\n` +
                `**Examples of changes you can request:**\n` +
                `• "Change the VM size to Standard_D4s_v3"\n` +
                `• "Add 2 more worker nodes to the cluster"\n` +
                `• "Change the region to West Europe"\n` +
                `• "Add tags for cost center and environment"\n` +
                `• "Increase storage size to 100GB"\n` +
                `• "Add a load balancer"\n\n` +
                `Just describe what you want to change in natural language.\n\n` +
                `💡 **Commands:** Type "cancel" to exit edit mode without changes.`;

            addMessage('assistant', editMessage, false);

            // Focus on input with helpful placeholder
            setTimeout(() => {
                const messageInput = document.getElementById('chatInput');
                if (messageInput) {
                    messageInput.focus();
                    messageInput.placeholder = "Describe what you want to modify (e.g., 'change VM size to Standard_D4s_v3')";
                }
            }, 100);

            // Set flag to indicate we're in edit mode
            window.waitingForTerraformEdit = true;
        }

        function cancelTerraformEdit() {
            // Clear edit mode state
            window.waitingForTerraformEdit = false;
            window.currentTerraformContext = null;
            window.currentTerraformCodeId = null;

            // Reset placeholder text
            const messageInput = document.getElementById('chatInput');
            if (messageInput) {
                messageInput.placeholder = "Ask me to create Azure infrastructure (e.g., 'Create a web app with database')";
            }

            // Show cancellation message
            addMessage('assistant', 
                '❌ **Edit Cancelled**\n\n' +
                'No changes were made to your Terraform configuration.\n\n' +
                'What else would you like to do?',
                false
            );
        }

        function requestTerraformApply(codeId) {
            const codeBlock = document.getElementById(codeId);
            if (!codeBlock) {
                console.error('Terraform code block not found');
                return;
            }

            const rawCode = codeBlock.getAttribute('data-raw-code');
            if (!rawCode) {
                console.error('No raw code found');
                return;
            }

            // Store the current Terraform code for context
            window.currentTerraformContext = rawCode;
            window.currentTerraformCodeId = codeId;

            // Check for mandatory parameters first
            const missingParams = checkMandatoryParameters(rawCode);
            
            if (missingParams.length > 0) {
                // Ask for mandatory parameters first
                askForMandatoryParameters(missingParams, rawCode);
            } else {
                // Deploy directly
                handleDirectDeployment(rawCode);
            }
        }

        function checkMandatoryParameters(terraformCode) {
            const missingParams = [];
            
            // Define mandatory parameters based on resource types
            const mandatoryChecks = [
                {
                    param: 'resource_group_name',
                    label: 'Resource Group Name',
                    pattern: /resource_group_name\s*=\s*"([^"]+)"/,
                    variablePattern: /\$\{var\.resource_group_name\}/,
                    required: true
                },
                {
                    param: 'location',
                    label: 'Azure Region',
                    pattern: /location\s*=\s*"([^"]+)"/,
                    variablePattern: /\$\{var\.location\}/,
                    required: true,
                    suggestions: ['eastus', 'westus2', 'northeurope', 'westeurope', 'eastasia']
                },
                {
                    param: 'environment',
                    label: 'Environment',
                    pattern: /environment\s*=\s*"([^"]+)"/,
                    variablePattern: /\$\{var\.environment\}/,
                    required: true,
                    suggestions: ['dev', 'staging', 'prod', 'test']
                }
            ];

            // Add resource-specific mandatory parameters
            if (terraformCode.includes('azurerm_kubernetes_cluster')) {
                mandatoryChecks.push({
                    param: 'kubernetes_version',
                    label: 'Kubernetes Version',
                    pattern: /kubernetes_version\s*=\s*"([^"]+)"/,
                    variablePattern: /\$\{var\.kubernetes_version\}/,
                    required: true,
                    suggestions: ['1.29', '1.28', '1.27']
                });
            }

            // Check each mandatory parameter
            mandatoryChecks.forEach(check => {
                if (check.required) {
                    const hasValue = check.pattern.test(terraformCode);
                    const hasVariable = check.variablePattern.test(terraformCode);
                    
                    if (hasVariable && !hasValue) {
                        // Parameter is referenced but not defined
                        missingParams.push(check);
                    }
                }
            });

            return missingParams;
        }

        function askForMandatoryParameters(missingParams, terraformCode) {
            let parameterQuestions = "**⚠️ Missing Required Parameters**\n\n";
            parameterQuestions += "Before I can help you with this Terraform configuration, I need some mandatory parameters:\n\n";
            
            missingParams.forEach((param, index) => {
                parameterQuestions += `**${index + 1}. ${param.label}**\n`;
                if (param.suggestions) {
                    parameterQuestions += `   Suggested values: ${param.suggestions.map(s => `\`${s}\``).join(', ')}\n`;
                }
                parameterQuestions += `   Example: "${param.param} = your-value-here"\n\n`;
            });

            parameterQuestions += "Please provide the missing parameters. You can say something like:\n";
            parameterQuestions += `• "Set resource group to 'my-rg', location to 'eastus', environment to 'dev'"\n`;
            parameterQuestions += `• "Use resource group 'prod-rg' in West Europe for production environment"\n\n`;
            parameterQuestions += `Once you provide these values, I'll ask if you want to modify the template or deploy it directly.`;

            // Add the message to chat
            addMessage('assistant', parameterQuestions, false);

            // Focus on input with helpful placeholder
            setTimeout(() => {
                const messageInput = document.getElementById('chatInput');
                if (messageInput) {
                    messageInput.focus();
                    messageInput.placeholder = "Provide the missing parameter values (e.g., 'resource group: my-rg, location: eastus, environment: dev')";
                }
            }, 100);

            // Set flag to indicate we're waiting for parameters
            window.waitingForMandatoryParams = true;
            window.missingParamsList = missingParams;
        }

        function askForNextAction(terraformCode) {
            const actionMessage = `**🎯 Terraform Configuration Ready**\n\n` +
                `I have your Terraform configuration with all required parameters. What would you like to do?\n\n` +
                `**✅ Deploy:** Execute terraform apply to create resources\n` +
                `**✏️ Edit:** Modify the template before deployment\n` +
                `**❌ Cancel:** Abort this deployment\n\n` +
                `Just say "deploy", "edit", or "cancel".`;
            
            addMessage('assistant', actionMessage, false);
            
            // Set flag to indicate we're waiting for deployment confirmation
            window.waitingForDeploymentConfirmation = true;
            
            // Focus on input with helpful placeholder
            setTimeout(() => {
                const messageInput = document.getElementById('chatInput');
                if (messageInput) {
                    messageInput.focus();
                    messageInput.placeholder = "Type 'deploy', 'edit', or 'cancel'";
                }
            }, 100);
        }

        function handleMandatoryParametersResponse(userMessage) {
            console.log('📝 Handling mandatory parameters response:', userMessage);
            
            // Parse the user's response to extract parameter values
            const extractedParams = {};
            const lowerMessage = userMessage.toLowerCase();
            
            // Try to extract common parameters from various phrasings
            const paramExtractionPatterns = [
                // Pattern: "resource group to 'value'" or "resource group: value"
                { param: 'resource_group_name', patterns: [/resource\s*group[:\s]+['"]*([^'",\s]+)/i] },
                { param: 'location', patterns: [/location[:\s]+['"]*([^'",\s]+)/i, /region[:\s]+['"]*([^'",\s]+)/i] },
                { param: 'environment', patterns: [/environment[:\s]+['"]*([^'",\s]+)/i, /env[:\s]+['"]*([^'",\s]+)/i] },
                { param: 'kubernetes_version', patterns: [/k8s\s*version[:\s]+['"]*([^'",\s]+)/i, /kubernetes\s*version[:\s]+['"]*([^'",\s]+)/i] }
            ];
            
            paramExtractionPatterns.forEach(paramDef => {
                paramDef.patterns.forEach(pattern => {
                    const match = userMessage.match(pattern);
                    if (match && match[1]) {
                        extractedParams[paramDef.param] = match[1].replace(/['"]/g, '');
                    }
                });
            });
            
            console.log('🔍 Extracted parameters:', extractedParams);
            
            // Check if we got some parameters
            if (Object.keys(extractedParams).length > 0) {
                // Update the Terraform code with the provided parameters
                let updatedCode = window.currentTerraformContext;
                
                Object.entries(extractedParams).forEach(([param, value]) => {
                    // Replace variable references with actual values
                    const variablePattern = new RegExp(`\\$\\{var\\.${param}\\}`, 'g');
                    updatedCode = updatedCode.replace(variablePattern, `"${value}"`);
                });
                
                // Store updated code
                window.currentTerraformContext = updatedCode;
                
                // Check if we still have missing parameters
                const stillMissing = checkMandatoryParameters(updatedCode);
                
                if (stillMissing.length > 0) {
                    // Still missing some parameters
                    const confirmMessage = `**✅ Parameters Updated**\n\n` +
                        `I've updated these parameters:\n` +
                        Object.entries(extractedParams).map(([k, v]) => `• ${k}: ${v}`).join('\n') + '\n\n' +
                        `However, I still need:\n` +
                        stillMissing.map(p => `• ${p.label}`).join('\n') + '\n\n' +
                        `Please provide the remaining values.`;
                    
                    addMessage('assistant', confirmMessage, false);
                    // Keep waiting for more parameters
                } else {
                    // All parameters are now provided
                    window.waitingForMandatoryParams = false;
                    window.missingParamsList = null;
                    
                    const confirmMessage = `**✅ All Parameters Set**\n\n` +
                        `I've updated your Terraform configuration with:\n` +
                        Object.entries(extractedParams).map(([k, v]) => `• ${k}: ${v}`).join('\n') + '\n\n';
                    
                    addMessage('assistant', confirmMessage, false);
                    
                    // Now ask what they want to do next
                    askForNextAction(updatedCode);
                }
            } else {
                // Couldn't extract parameters, ask for clarification
                addMessage('assistant', 
                    '❓ **Clarification Needed**\n\n' +
                    'I couldn\'t extract the parameter values from your message. Please try a format like:\n\n' +
                    '• "resource group: my-rg, location: eastus, environment: dev"\n' +
                    '• "Set resource group to \'prod-rg\' and location to \'westus2\'"\n\n' +
                    'What values would you like to use for the missing parameters?',
                    false
                );
            }
        }

        function handleTerraformEditRequest(userMessage) {
            console.log('✏️ Handling Terraform edit request:', userMessage);
            
            const lowerMessage = userMessage.toLowerCase();
            
            // Check for cancel request
            if (lowerMessage.includes('cancel') || lowerMessage.includes('exit') || lowerMessage.includes('abort')) {
                cancelTerraformEdit();
                return;
            }
            
            // Send the edit request with the current Terraform context
            sendMessageWithContext(userMessage, window.currentTerraformContext).then(result => {
                // Exit edit mode after processing
                window.waitingForTerraformEdit = false;
                window.currentTerraformContext = null;
                window.currentTerraformCodeId = null;
                
                // Reset placeholder
                const messageInput = document.getElementById('chatInput');
                if (messageInput) {
                    messageInput.placeholder = "Ask me to create Azure resources (e.g., 'Create an AKS cluster')";
                }
                
                if (result && result.message && !result.handledAsAdaptiveCard) {
                    addMessage('assistant', result.message);
                }
            }).catch(error => {
                console.error('Error processing edit request:', error);
                addMessage('assistant', 'Sorry, there was an error processing your edit request. Please try again.');
                
                // Don't exit edit mode on error, let user try again
            });
        }

        function handleDeploymentConfirmation(userMessage) {
            console.log('🚀 Handling deployment confirmation:', userMessage);
            
            const lowerMessage = userMessage.toLowerCase();
            
            // Exit confirmation mode
            window.waitingForDeploymentConfirmation = false;
            
            // Reset placeholder
            const messageInput = document.getElementById('chatInput');
            if (messageInput) {
                messageInput.placeholder = "Ask me to create Azure resources (e.g., 'Create an AKS cluster')";
            }
            
            if (lowerMessage.includes('deploy') || lowerMessage.includes('apply') || lowerMessage.includes('yes')) {
                // User wants to deploy
                console.log('✅ User confirmed deployment');
                
                // Send deployment request with Terraform context
                sendMessageWithContext('Deploy this Terraform configuration', window.currentTerraformContext).then(result => {
                    if (result && result.message && !result.handledAsAdaptiveCard) {
                        addMessage('assistant', result.message);
                    }
                    
                    // Clear context after deployment
                    window.currentTerraformContext = null;
                    window.currentTerraformCodeId = null;
                }).catch(error => {
                    console.error('Error during deployment:', error);
                    addMessage('assistant', 'Sorry, there was an error starting the deployment. Please try again.');
                });
                
            } else if (lowerMessage.includes('edit') || lowerMessage.includes('modify')) {
                // User wants to edit first
                console.log('✏️ User wants to edit first');
                
                // Enter edit mode
                const codeId = window.currentTerraformCodeId;
                if (codeId) {
                    requestTerraformEdit(codeId);
                } else {
                    addMessage('assistant', 'I can help you edit the configuration. What changes would you like to make?');
                    window.waitingForTerraformEdit = true;
                }
                
            } else if (lowerMessage.includes('cancel') || lowerMessage.includes('abort') || lowerMessage.includes('no')) {
                // User wants to cancel
                console.log('❌ User cancelled deployment');
                
                addMessage('assistant', 
                    '❌ **Deployment Cancelled**\n\n' +
                    'No resources were created. The Terraform configuration is still available if you change your mind.\n\n' +
                    'What else would you like to do?'
                );
                
                // Clear context
                window.currentTerraformContext = null;
                window.currentTerraformCodeId = null;
                
            } else {
                // Unclear response, ask for clarification
                addMessage('assistant', 
                    '❓ **Please Clarify**\n\n' +
                    'I didn\'t understand your response. Please say:\n\n' +
                    '• **"deploy"** - to create the resources now\n' +
                    '• **"edit"** - to modify the configuration first\n' +
                    '• **"cancel"** - to abort this deployment\n\n' +
                    'What would you like to do?'
                );
                
                // Re-enter confirmation mode
                window.waitingForDeploymentConfirmation = true;
                
                setTimeout(() => {
                    const messageInput = document.getElementById('chatInput');
                    if (messageInput) {
                        messageInput.placeholder = "Type 'deploy', 'edit', or 'cancel'";
                    }
                }, 100);
            }
        }

        function handleDirectDeployment(terraformCode) {
            // Ask for final confirmation before deployment
            const confirmMessage = `**🚀 Ready to Deploy**\n\n` +
                `Your Terraform configuration is ready. This will create Azure resources in your subscription.\n\n` +
                `**Are you sure you want to proceed?**\n\n` +
                `• **Yes** - Deploy the infrastructure now\n` +
                `• **Edit** - Make changes to the configuration first\n` +
                `• **Cancel** - Abort this deployment`;
            
            addMessage('assistant', confirmMessage, false);
            
            // Set flag to indicate we're waiting for deployment confirmation
            window.waitingForDeploymentConfirmation = true;
            
            // Focus on input with helpful placeholder
            setTimeout(() => {
                const messageInput = document.getElementById('chatInput');
                if (messageInput) {
                    messageInput.focus();
                    messageInput.placeholder = "Type 'yes', 'edit', or 'cancel'";
                }
            }, 100);
        }

        async function sendMessageWithContext(message, terraformContext) {
            console.log('📡 Sending message with Terraform context');
            
            try {
                const response = await fetch(`${API_BASE_URL}/api/agent/chat`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        message: message,
                        sessionId: SESSION_ID,
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
                    addAdaptiveCardMessage(result.message, result.adaptiveCard);
                    return { success: true, message: result.message, handledAsAdaptiveCard: true };
                }
                
                return result;
            } catch (error) {
                console.error('❌ Error in sendMessageWithContext:', error);
                throw error;
            }
        }

        function copyTerraformCode(codeId) {
            const codeBlock = document.getElementById(codeId);
            if (!codeBlock) {
                console.error('Code block not found');
                return;
            }

            const rawCode = codeBlock.getAttribute('data-raw-code');
            if (!rawCode) {
                console.error('No raw code found');
                return;
            }

            // Copy to clipboard
            navigator.clipboard.writeText(rawCode).then(() => {
                // Visual feedback
                const copyBtn = codeBlock.parentElement.querySelector('.copy-btn');
                if (copyBtn) {
                    const originalText = copyBtn.innerHTML;
                    copyBtn.innerHTML = '<span class="btn-icon">✅</span><span class="btn-text">Copied!</span>';
                    copyBtn.classList.add('copied');
                    
                    setTimeout(() => {
                        copyBtn.innerHTML = originalText;
                        copyBtn.classList.remove('copied');
                    }, 2000);
                }
                
                showNotification('Terraform code copied to clipboard!', 'success');
            }).catch(err => {
                console.error('Failed to copy: ', err);
                showNotification('Failed to copy code to clipboard', 'error');
            });
        }

        function setFormDisabled(disabled) {
            if (chatInput) chatInput.disabled = disabled;
            if (sendButton) sendButton.disabled = disabled;
        }

        function showTyping() {
            if (typingIndicator) {
                typingIndicator.style.display = 'block';
            }
        }

        function hideTyping() {
            if (typingIndicator) {
                typingIndicator.style.display = 'none';
            }
        }

        function showDeploymentProgress() {
            console.log('🚀 Showing deployment progress UI');
            showProgressIndicator();
            startProgressSimulation('Processing your deployment request...');
        }

        function hideDeploymentProgress() {
            console.log('✅ Hiding deployment progress UI');
            hideProgressIndicator();
        }

        function showProgressIndicator() {
            if (progressIndicator) {
                // Reset all steps to pending state
                resetProgressSteps();
                progressIndicator.style.display = 'block';
            }
        }

        function hideProgressIndicator() {
            if (progressIndicator) {
                progressIndicator.style.display = 'none';
            }
        }

        async function startProgressSimulation(initialMessage) {
            console.log('🔄 Starting progress simulation:', initialMessage);
            
            if (!progressIndicator) {
                console.warn('Progress indicator element not found');
                return;
            }
            
            // Simulate progress through the steps
            await simulateProgress(initialMessage);
        }

        function showDeploymentLoading(message = 'Deploying Azure resources') {
            console.log('📊 Showing deployment loading indicator:', message);
            const deploymentIndicator = document.getElementById('deploymentIndicator');
            const deploymentText = document.getElementById('deploymentText');
            
            if (deploymentIndicator && deploymentText) {
                deploymentText.textContent = message;
                deploymentIndicator.style.display = 'block';
                
                // Animate the dots
                startDotAnimation();
            }
        }

        function hideDeploymentLoading() {
            console.log('📊 Hiding deployment loading indicator');
            const deploymentIndicator = document.getElementById('deploymentIndicator');
            
            if (deploymentIndicator) {
                deploymentIndicator.style.display = 'none';
                stopDotAnimation();
            }
        }

        let dotAnimationInterval;

        function startDotAnimation() {
            const dotsElement = document.getElementById('deploymentDots');
            if (!dotsElement) return;
            
            let dotCount = 0;
            dotAnimationInterval = setInterval(() => {
                dotCount = (dotCount + 1) % 4;
                dotsElement.textContent = '.'.repeat(dotCount) + '\u00A0'.repeat(3 - dotCount);
            }, 500);
        }

        function stopDotAnimation() {
            if (dotAnimationInterval) {
                clearInterval(dotAnimationInterval);
                dotAnimationInterval = null;
            }
        }

        function updateStatus(status) {
            if (statusIndicator) {
                statusIndicator.className = 'status-indicator';
                if (status === 'ready') {
                    statusIndicator.style.backgroundColor = '#28a745';
                } else if (status === 'busy') {
                    statusIndicator.style.backgroundColor = '#ffc107';
                } else if (status === 'error') {
                    statusIndicator.style.backgroundColor = '#dc3545';
                }
            }
        }

        function updateSessionDisplay() {
            if (sessionIdElement) {
                sessionIdElement.textContent = SESSION_ID;
            }
        }

        // Quick send functions
        function sendExample(message) {
            chatInput.value = message;
            handleSubmit(new Event('submit'));
        }

        function showTemplateGallery() {
            if (window.templateManager) {
                window.templateManager.showGallery();
            } else {
                sendExample('Show me available Azure templates');
            }
        }

        async function checkApiStatus() {
            try {
                const response = await fetch(`${API_BASE_URL}/api/agent/status`);
                const status = await response.json();
                
                if (status.isHealthy) {
                    updateStatus('ready');
                    console.log('✅ API is healthy');
                } else {
                    updateStatus('error');
                    console.warn('⚠️ API is not healthy');
                }
            } catch (error) {
                updateStatus('error');
                console.error('❌ Failed to check API status:', error);
            }
        }

        // Suggestion chips functionality (CURRENTLY DISABLED)
        function showSuggestionChips(chips) {
            // COMPLETELY DISABLED FOR NOW
            console.log('Suggestion chips disabled - would have shown:', chips);
            return;
            
            /*
            const container = document.getElementById('suggestionChips');
            const chipsList = document.getElementById('chipsList');
            
            if (!container || !chipsList || !chips || chips.length === 0) {
                return;
            }
            
            // Clear existing chips
            chipsList.innerHTML = '';
            
            // Add new chips
            chips.forEach(chip => {
                const chipElement = document.createElement('span');
                chipElement.className = `suggestion-chip ${chip.type || 'secondary'}`;
                chipElement.textContent = chip.text;
                chipElement.onclick = () => handleChipClick(chip.action, chip.text);
                chipsList.appendChild(chipElement);
            });
            
            // Show container with animation
            container.classList.add('show');
            */
        }

        function hideSuggestionChips() {
            // DISABLED BUT SAFE TO CALL
            console.log('hideSuggestionChips called (chips disabled)');
            return;
            
            /*
            const container = document.getElementById('suggestionChips');
            if (container) {
                container.classList.remove('show');
            }
            */
        }

        function handleChipClick(action, text) {
            // DISABLED
            console.log('Chip click disabled - would have handled:', action, text);
            return;
            
            /*
            console.log('Chip clicked:', action, text);
            
            // Hide chips after click
            hideSuggestionChips();
            
            // Generate appropriate command based on action
            let command = '';
            
            switch(action) {
                case 'deploy':
                case 'deploy-yes':
                    command = 'Deploy the terraform template';
                    break;
                case 'edit':
                case 'deploy-modify':
                    command = 'Edit the terraform template';
                    break;
                case 'deploy-explain':
                    command = 'Explain what resources will be created';
                    break;
                case 'deploy-cancel':
                    command = 'Cancel this deployment';
                    break;
                case 'status':
                    command = 'Check the status of my deployed resources';
                    break;
                case 'list':
                case 'list-rg':
                    command = 'List my Azure resource groups';
                    break;
                case 'create':
                case 'create-aks':
                    command = 'Create an AKS cluster';
                    break;
                case 'details':
                    command = 'Show me deployment details';
                    break;
                case 'costs':
                    command = 'Show me the estimated costs';
                    break;
                case 'retry':
                    command = 'Try the deployment again';
                    break;
                case 'help':
                    command = 'Help me troubleshoot this issue';
                    break;
                case 'best-practices':
                    command = 'Show me Azure best practices';
                    break;
                default:
                    command = text; // Use the chip text as-is
                    break;
            }
            
            // Set the input value and submit
            if (command) {
                chatInput.value = command;
                handleSubmit(new Event('submit'));
            }
            */
        }

        // Terraform display functionality
        function showTerraformSuggestions(codeId) {
            // DISABLED FOR NOW
            console.log('Terraform suggestions disabled for codeId:', codeId);
            return;
            
            /*
            console.log('Terraform block clicked:', codeId);
            
            // Show specific suggestions for Terraform templates
            showSuggestionChips([
                { text: '🚀 Deploy this template', action: 'deploy', type: 'primary' },
                { text: '✏️ Edit the configuration', action: 'edit', type: 'edit' },
                { text: '💡 Explain this template', action: 'explain', type: 'secondary' },
                { text: '❌ Cancel deployment', action: 'cancel', type: 'cancel' }
            ]);
            */
        }
