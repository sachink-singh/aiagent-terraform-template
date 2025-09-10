// Debug script to test clickable functionality
console.log('=== DEBUGGING CLICKABLE OUTPUT ===');

// Wait for the page to load
setTimeout(() => {
    console.log('1. Testing if addMessage function exists...');
    if (typeof addMessage === 'function') {
        console.log('✅ addMessage function found');
        
        // Test with simple pod data
        console.log('2. Adding test message...');
        const testMessage = `Here are your pods:

NAME                     READY   STATUS    RESTARTS   AGE
nginx-deployment-12345   1/1     Running   0          2d5h
redis-cache-67890       1/1     Running   1          1d`;

        addMessage('assistant', testMessage);
        
        // Check if clickable elements were created
        setTimeout(() => {
            console.log('3. Checking for clickable elements...');
            const clickableElements = document.querySelectorAll('.clickable-line, .clickable-header');
            console.log(`Found ${clickableElements.length} clickable elements`);
            
            if (clickableElements.length > 0) {
                console.log('4. Testing first clickable element...');
                const firstElement = clickableElements[0];
                console.log('Element:', firstElement);
                console.log('Data-text:', firstElement.getAttribute('data-text'));
                console.log('Onclick:', firstElement.getAttribute('onclick'));
                
                // Test the appendToChatById function directly
                console.log('5. Testing appendToChatById function...');
                if (typeof appendToChatById === 'function') {
                    console.log('✅ appendToChatById function exists');
                    const elementId = firstElement.id;
                    console.log('Testing with element ID:', elementId);
                    
                    // Check if messageInput exists
                    const messageInput = document.getElementById('messageInput');
                    if (messageInput) {
                        console.log('✅ messageInput found');
                        console.log('Current value:', messageInput.value);
                        
                        // Test the function
                        try {
                            appendToChatById(elementId);
                            console.log('Function called successfully');
                            console.log('New value:', messageInput.value);
                        } catch (error) {
                            console.error('❌ Error calling appendToChatById:', error);
                        }
                    } else {
                        console.error('❌ messageInput element not found');
                    }
                } else {
                    console.error('❌ appendToChatById function not found');
                }
            } else {
                console.log('❌ No clickable elements found');
                
                // Debug the makeAgentOutputClickable function
                console.log('6. Testing makeAgentOutputClickable directly...');
                if (typeof makeAgentOutputClickable === 'function') {
                    console.log('✅ makeAgentOutputClickable function exists');
                    const testLine = 'nginx-deployment-12345   1/1     Running   0          2d5h';
                    const result = makeAgentOutputClickable(testLine);
                    console.log('Input:', testLine);
                    console.log('Output:', result);
                } else {
                    console.error('❌ makeAgentOutputClickable function not found');
                }
            }
        }, 1000);
        
    } else {
        console.error('❌ addMessage function not found');
    }
}, 3000);

// Also test if the functions are in the global scope
setTimeout(() => {
    console.log('=== CHECKING GLOBAL FUNCTIONS ===');
    console.log('addMessage:', typeof window.addMessage);
    console.log('makeAgentOutputClickable:', typeof window.makeAgentOutputClickable);
    console.log('appendToChatById:', typeof window.appendToChatById);
    console.log('appendToChat:', typeof window.appendToChat);
    console.log('escapeForAttribute:', typeof window.escapeForAttribute);
}, 1000);
