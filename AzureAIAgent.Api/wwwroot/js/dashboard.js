/**
 * Dashboard Statistics Management
 */

// Global variables for dashboard state
let deploymentCount = 0;
let resourceCount = 0;
let sessionStartTime = Date.now();

window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.dashboard = {

    // Initialize dashboard stats
    initialize() {
        console.log('📊 Dashboard module initializing...');
        this.updateStat('deploymentCount', 0);
        this.updateStat('resourceCount', 0);
        this.updateStat('sessionTime', '00:00');
        this.startSessionTimer();
        console.log('📊 Dashboard module initialized successfully');
    },

    // Update a specific statistic
    updateStat(statId, value) {
        const element = document.getElementById(statId);
        if (element) {
            if (typeof value === 'number' && statId !== 'sessionTime') {
                const currentValue = parseInt(element.textContent) || 0;
                this.animateCounter(element, currentValue, value);
            } else {
                element.textContent = value;
            }
        }
    },

    // Animate counter changes
    animateCounter(element, from, to) {
        const duration = 1000;
        const start = Date.now();
        
        function update() {
            const now = Date.now();
            const progress = Math.min((now - start) / duration, 1);
            const eased = window.AzureAIAgent.dashboard.easeOutCubic(progress);
            const current = Math.round(from + (to - from) * eased);
            
            element.textContent = current;
            
            if (progress < 1) {
                requestAnimationFrame(update);
            }
        }
        
        update();
    },

    // Easing function for smooth animations
    easeOutCubic(t) {
        return 1 - Math.pow(1 - t, 3);
    },

    // Start session timer
    startSessionTimer() {
        setInterval(() => {
            const elapsed = Date.now() - sessionStartTime;
            const minutes = Math.floor(elapsed / 60000);
            const seconds = Math.floor((elapsed % 60000) / 1000);
            this.updateStat('sessionTime', `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`);
        }, 1000);
    },

    // Increment deployment count
    incrementDeploymentCount() {
        deploymentCount++;
        this.updateStat('deploymentCount', deploymentCount);
    },

    // Update resource count
    updateResourceCount(count) {
        resourceCount = count;
        this.updateStat('resourceCount', resourceCount);
    },

    // Update dashboard from message content
    updateFromMessage(content) {
        const lowerContent = content.toLowerCase();
        
        if (lowerContent.includes('deployment') && lowerContent.includes('successful')) {
            this.incrementDeploymentCount();
        }
        
        // Count resources mentioned
        const resourceTypes = ['virtual machine', 'storage account', 'network', 'kubernetes', 'database', 'app service'];
        let resourceMentions = 0;
        resourceTypes.forEach(type => {
            if (lowerContent.includes(type)) {
                resourceMentions++;
            }
        });
        
        if (resourceMentions > 0) {
            this.updateResourceCount(Math.max(resourceCount, resourceMentions));
        }
    },

    // Update Terraform status (for terraform.js compatibility)
    updateTerraformStatus(key, value) {
        // Map to existing updateStat method
        this.updateStat(key, value);
    },

    // Increment counter (for terraform.js compatibility)
    incrementCounter(counterType) {
        const element = document.getElementById(counterType);
        if (element) {
            const currentValue = parseInt(element.textContent) || 0;
            this.updateStat(counterType, currentValue + 1);
        }
    },

    // Update last operation (for progress.js compatibility)
    updateLastOperation(status, message) {
        // Update status display if elements exist
        const statusElement = document.getElementById('lastOperationStatus');
        const messageElement = document.getElementById('lastOperationMessage');
        
        if (statusElement) {
            statusElement.textContent = status;
        }
        if (messageElement) {
            messageElement.textContent = message;
        }
    }
};

// Confirm dashboard module loaded
console.log('📊 Dashboard module loaded successfully');
console.log('📊 Available methods:', Object.keys(window.AzureAIAgent.dashboard));
