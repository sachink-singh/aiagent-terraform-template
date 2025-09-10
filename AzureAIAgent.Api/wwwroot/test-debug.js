// Test script to be run in browser console
console.log('=== TESTING CLICKABLE OUTPUT WITH DEBUGGING ===');

// Test data
const testMessage = `Here are your pods:

NAME                     READY   STATUS    RESTARTS   AGE
nginx-deployment-12345   1/1     Running   0          2d5h
redis-cache-67890       1/1     Running   1          1d`;

// Wait for page to load
setTimeout(() => {
    console.log('Testing addMessage function...');
    if (typeof addMessage === 'function') {
        console.log('✅ addMessage function found');
        addMessage('assistant', testMessage);
        console.log('✅ Test message added - check console for debugging output');
    } else {
        console.log('❌ addMessage function not found');
    }
}, 2000);
