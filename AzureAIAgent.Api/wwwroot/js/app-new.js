/**
 * Main Application Initialization
 */

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.app = {

    // Initialize the application
    initialize() {
        console.log('🚀 Initializing Azure AI Agent Application...');
        
        try {
            // Load configuration first
            this.loadConfiguration();
            
            // Initialize DOM cache now that config is loaded
            window.AzureAIAgent.config.initializeDOMCache();
            
            // Load Terraform context
            window.AzureAIAgent.terraform.loadContext();
            
            // Initialize modules
            this.initializeModules();
            
            // Setup event handlers
            window.AzureAIAgent.events.initialize();
            
            // Initialize UI state
            this.initializeUIState();
            
            // Show welcome message
            this.showWelcomeMessage();
            
            console.log('✅ Azure AI Agent Application initialized successfully');
            
        } catch (error) {
            console.error('❌ Error initializing application:', error);
            this.handleInitializationError(error);
        }
    },

    // Load configuration
    loadConfiguration() {
        // Initialize session ID if not exists
        if (!window.AzureAIAgent.config.SESSION_ID) {
            window.AzureAIAgent.config.SESSION_ID = this.generateSessionId();
        }
        
        // Load saved settings from localStorage
        try {
            const savedSettings = localStorage.getItem('azureAIAgentSettings');
            if (savedSettings) {
                const settings = JSON.parse(savedSettings);
                Object.assign(window.AzureAIAgent.config, settings);
            }
        } catch (error) {
            console.warn('⚠️ Could not load saved settings:', error);
        }
        
        console.log('⚙️ Configuration loaded');
    },

    // DOM elements are cached by config.js initializeDOMCache() function

    // Initialize all modules
    initializeModules() {
        // Setup scroll enhancements
        window.AzureAIAgent.events.addScrollToBottomButton();
        
        // Setup tooltips
        window.AzureAIAgent.events.setupTooltips();
        
        console.log('📦 Modules initialized');
    },

    // Initialize UI state
    initializeUIState() {
        // Set initial status
        window.AzureAIAgent.ui.updateStatus('ready');
        
        // Focus chat input
        if (window.AzureAIAgent.config.chatInput) {
            window.AzureAIAgent.config.chatInput.focus();
        }
        
        // Debug: Check if dashboard module exists
        console.log('🔍 Checking dashboard module:', window.AzureAIAgent.dashboard);
        console.log('🔍 Dashboard methods:', Object.keys(window.AzureAIAgent.dashboard || {}));
        
        // Initialize dashboard using available methods
        if (window.AzureAIAgent.dashboard && window.AzureAIAgent.dashboard.initialize) {
            window.AzureAIAgent.dashboard.initialize();
        } else {
            console.error('❌ Dashboard module not found or initialize method missing');
        }
        
        console.log('🎨 UI state initialized');
    },

    // Show welcome message
    showWelcomeMessage() {
        const welcomeMessage = `👋 Welcome to Azure AI Agent!

I'm your intelligent assistant for Azure resource management and deployment. Here's what I can help you with:

🏗️ **Infrastructure Management**
• Create and deploy Azure resources using natural language
• Generate Terraform templates automatically
• Monitor deployment progress in real-time

🎯 **What you can ask me:**
• "Deploy an AKS cluster in East US"
• "Create a storage account with blob containers"
• "Show me my Azure resource groups"
• "Generate a Terraform template for a web app"

💡 **Tips:**
• Use specific requirements for better results
• I'll show you previews before deploying
• All operations are tracked in the dashboard

Type your request below to get started! 🚀`;

        window.AzureAIAgent.ui.addMessage('assistant', welcomeMessage);
    },

    // Generate unique session ID
    generateSessionId() {
        return 'session_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    },

    // Handle initialization errors
    handleInitializationError(error) {
        console.error('💥 Initialization failed:', error);
        
        // Show error message to user
        const errorMessage = `❌ Application initialization failed: ${error.message}
        
Please refresh the page and try again. If the problem persists, check the browser console for more details.`;
        
        window.AzureAIAgent.ui.showNotification(errorMessage, 'error');
        
        // Update status indicator
        window.AzureAIAgent.ui.updateStatus('error');
    },

    // Save settings
    saveSettings(settings) {
        try {
            Object.assign(window.AzureAIAgent.config, settings);
            localStorage.setItem('azureAIAgentSettings', JSON.stringify(settings));
            console.log('💾 Settings saved');
        } catch (error) {
            console.warn('⚠️ Could not save settings:', error);
        }
    },

    // Get application info
    getApplicationInfo() {
        return {
            sessionId: window.AzureAIAgent.config.SESSION_ID,
            startTime: window.AzureAIAgent.config.sessionStartTime,
            version: '1.0.0',
            modules: [
                'config',
                'dashboard', 
                'chat',
                'ui',
                'formatting',
                'cards',
                'terraform',
                'progress',
                'events',
                'app'
            ],
            terraformContext: window.AzureAIAgent.terraform.getTemplateStatus(),
            deploymentHistory: window.AzureAIAgent.terraform.getDeploymentHistory()
        };
    },

    // Cleanup on page unload
    cleanup() {
        console.log('🧹 Cleaning up application...');
        
        // Save current state
        const currentState = {
            sessionDuration: Date.now() - window.AzureAIAgent.config.sessionStartTime,
            terraformContext: window.AzureAIAgent.config.terraformContext
        };
        
        try {
            sessionStorage.setItem('lastSessionState', JSON.stringify(currentState));
        } catch (error) {
            console.warn('⚠️ Could not save session state:', error);
        }
        
        console.log('✅ Cleanup completed');
    }
};

// Auto-initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.AzureAIAgent.app.initialize();
});

// Cleanup on page unload
window.addEventListener('beforeunload', () => {
    window.AzureAIAgent.app.cleanup();
});

// Global error handler
window.addEventListener('error', (e) => {
    console.error('🚨 Global error:', e.error);
    window.AzureAIAgent.ui.showNotification('An unexpected error occurred. Check console for details.', 'error');
});

// Global unhandled promise rejection handler
window.addEventListener('unhandledrejection', (e) => {
    console.error('🚨 Unhandled promise rejection:', e.reason);
    window.AzureAIAgent.ui.showNotification('An async operation failed. Check console for details.', 'error');
});
