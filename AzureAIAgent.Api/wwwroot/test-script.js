console.log('Testing clickable output functionality...');

// Wait for page to load, then test
setTimeout(() => {
    // Find the addMessage function and simulate an agent response
    if (typeof addMessage === 'function') {
        console.log('addMessage function found, testing...');
        
        const testResponse = `Here are your Kubernetes pods:

NAME                     READY   STATUS    RESTARTS   AGE
nginx-deployment-12345   1/1     Running   0          2d5h
redis-cache-67890       1/1     Running   1          1d  
web-frontend-abc123     0/1     Pending   0          5m
api-backend-def456      1/1     Failed    2          3h

Azure resources in your subscription:

Name                Location    ResourceGroup    Status
myWebApp           East US     rg-production    Running
myDatabase         West US     rg-data         Succeeded
myStorage          Central US  rg-storage      Creating`;

        addMessage('assistant', testResponse);
        console.log('Test message added! Check the chat for clickable lines.');
        
        // Test clicking after a delay
        setTimeout(() => {
            const clickableElements = document.querySelectorAll('.clickable-line, .clickable-header');
            console.log(`Found ${clickableElements.length} clickable elements`);
            
            if (clickableElements.length > 0) {
                console.log('First clickable element:', clickableElements[0]);
                console.log('Data text:', clickableElements[0].getAttribute('data-text'));
            }
        }, 1000);
        
    } else {
        console.log('addMessage function not found. Make sure the page is fully loaded.');
    }
}, 2000);
