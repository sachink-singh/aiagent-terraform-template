# Clickable Agent Output Demo

This demonstrates the new clickable functionality for agent outputs. Try asking the agent to list Kubernetes pods, Azure resources, or other CLI outputs.

## Example Outputs That Should Be Clickable:

### Kubernetes Pods
```
NAME                     READY   STATUS    RESTARTS   AGE
nginx-deployment-1       1/1     Running   0          2d
redis-cache-2           1/1     Running   1          1d
web-frontend-3          0/1     Pending   0          5m
api-backend-4           1/1     Failed    2          3h
```

### Azure Resources
```
Name                Location    ResourceGroup    Status
myWebApp           East US     rg-production    Running
myDatabase         West US     rg-data         Succeeded
myStorage          Central US  rg-storage      Creating
```

### Docker Containers
```
CONTAINER ID   IMAGE                COMMAND                  CREATED       STATUS
12345abcde     nginx:latest         "/docker-entrypoint.…"   2 hours ago   Up 2 hours
67890fghij     redis:6.2-alpine     "docker-entrypoint.s…"   1 day ago     Up 1 day
```

## How It Works:

1. **Individual Line Clicking**: Each line that matches patterns like resource lists, pod statuses, etc., becomes individually clickable
2. **Smart Pattern Detection**: The system recognizes common CLI output formats from kubectl, az cli, docker, etc.
3. **Visual Feedback**: Clickable lines have subtle styling and hover effects
4. **Chat Integration**: Clicking a line appends it to the chat input
5. **Header Recognition**: Table headers are styled differently and also clickable

## Test Instructions:

1. Open the web interface at http://localhost:5050
2. Ask for outputs like:
   - "List all pods in the default namespace"
   - "Show me Azure resources in my subscription"
   - "Get docker containers"
   - Any CLI command that produces tabular output

3. The agent's response should have clickable lines that you can click to add to your chat input.
