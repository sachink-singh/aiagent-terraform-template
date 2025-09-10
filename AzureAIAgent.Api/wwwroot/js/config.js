/**
 * Configuration and Constants for Azure AI Agent
 */

// Configuration with Terraform state management
const currentProtocol = window.location.protocol;
const currentHost = window.location.host;
const API_BASE_URL = `${currentProtocol}//${currentHost}`;
const SESSION_ID = 'chat-session-' + Date.now();

console.log('API_BASE_URL:', API_BASE_URL);

// Global state variables
let sessionStartTime = Date.now();
let deploymentCount = 0;
let resourceCount = 0;
let currentDeploymentId = null;
let deploymentPollingInterval = null;
let dotAnimationInterval;

// Terraform context tracking
window.currentTerraformContext = null;
window.currentTerraformCodeId = null;
window.waitingForTerraformEdit = false;
window.waitingForMandatoryParams = false;
window.waitingForDeploymentConfirmation = false;
window.missingParamsList = null;

// DOM element cache
let chatMessages, chatInput, sendButton, chatForm, typingIndicator, progressIndicator, statusIndicator, sessionIdElement;

// Initialize DOM elements cache
function initializeDOMCache() {
    chatMessages = document.getElementById('chatMessages');
    chatInput = document.getElementById('chatInput');
    sendButton = document.getElementById('sendButton');
    chatForm = document.getElementById('chatForm');
    typingIndicator = document.getElementById('typingIndicator');
    progressIndicator = document.getElementById('progressIndicator');
    statusIndicator = document.getElementById('statusIndicator');
    sessionIdElement = document.getElementById('sessionId');
    
    // Set session ID
    if (sessionIdElement) {
        sessionIdElement.textContent = SESSION_ID;
    }
    
    // Log cache status
    console.log('🎯 DOM Cache Status:', {
        chatMessages: !!chatMessages,
        chatInput: !!chatInput,
        sendButton: !!sendButton,
        chatForm: !!chatForm,
        typingIndicator: !!typingIndicator,
        progressIndicator: !!progressIndicator,
        statusIndicator: !!statusIndicator,
        sessionIdElement: !!sessionIdElement
    });
}

// Export for use in other modules
window.AzureAIAgent = window.AzureAIAgent || {};
window.AzureAIAgent.config = {
    API_BASE_URL,
    SESSION_ID,
    initializeDOMCache,
    // DOM elements getters
    get chatMessages() { return chatMessages; },
    get chatInput() { return chatInput; },
    get sendButton() { return sendButton; },
    get chatForm() { return chatForm; },
    get typingIndicator() { return typingIndicator; },
    get progressIndicator() { return progressIndicator; },
    get statusIndicator() { return statusIndicator; },
    get sessionIdElement() { return sessionIdElement; }
};
