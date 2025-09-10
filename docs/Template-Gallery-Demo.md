# Azure AI Agent - Template Gallery & Adaptive Cards Demo

## Overview

The Azure AI Agent now includes a comprehensive template management system that integrates **Adaptive Cards** with **GitHub template repositories** directly into the **Web API UI**. This provides a rich, interactive experience for deploying Azure infrastructure.

## How It Works in the Web UI

### 1. Template Gallery Access

Users can access the template gallery in several ways:

**From Welcome Screen:**
- Click the "📦 Browse Templates" quick action chip
- The system loads a curated gallery of Azure infrastructure templates

**Via Natural Language:**
- Type: "Show me available templates"
- Type: "I want to browse infrastructure templates"
- Type: "What templates do you have for web applications?"

### 2. Interactive Template Gallery

The template gallery displays as an **Adaptive Card** with:

```json
{
  "type": "AdaptiveCard",
  "body": [
    {
      "type": "TextBlock",
      "text": "Azure Infrastructure Templates",
      "weight": "Bolder",
      "size": "Large"
    },
    {
      "type": "Container",
      "items": [
        {
          "type": "ColumnSet",
          "columns": [
            {
              "type": "Column",
              "items": [
                {
                  "type": "TextBlock",
                  "text": "🚀 AKS Cluster",
                  "weight": "Bolder"
                },
                {
                  "type": "TextBlock",
                  "text": "Production-ready Kubernetes cluster with monitoring"
                }
              ]
            }
          ]
        }
      ]
    }
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "Deploy AKS Template",
      "data": { "templateId": "aks-cluster" }
    }
  ]
}
```

### 3. Template Categories Available

- **🚀 Kubernetes & Containers**
  - AKS cluster with monitoring
  - Container Apps environment
  - Azure Container Registry

- **🌐 Web Applications**
  - App Service with database
  - Static Web Apps
  - Function Apps with storage

- **💾 Data & Storage**
  - SQL Database with backup
  - Storage Account with lifecycle policies
  - CosmosDB with global distribution

- **🔧 Infrastructure**
  - Virtual Machine scale sets
  - Virtual Networks with subnets
  - Load Balancers with health probes

### 4. Parameter Configuration Forms

When a user selects a template, they get an interactive **parameter form** as an Adaptive Card:

```json
{
  "type": "AdaptiveCard",
  "body": [
    {
      "type": "TextBlock",
      "text": "Configure AKS Cluster",
      "weight": "Bolder"
    },
    {
      "type": "Input.Text",
      "id": "clusterName",
      "label": "Cluster Name",
      "placeholder": "my-aks-cluster",
      "isRequired": true
    },
    {
      "type": "Input.ChoiceSet",
      "id": "region",
      "label": "Azure Region",
      "choices": [
        { "title": "East US", "value": "eastus" },
        { "title": "West Europe", "value": "westeurope" }
      ]
    },
    {
      "type": "Input.Number",
      "id": "nodeCount",
      "label": "Node Count",
      "value": 3,
      "min": 1,
      "max": 10
    }
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "Deploy Now",
      "data": { "action": "deploy" }
    }
  ]
}
```

### 5. Live Deployment Tracking

Once deployment starts, users see:

**Real-time Progress:**
- Live progress bar showing deployment stages
- Status updates: "Creating resource group...", "Configuring networking..."
- Estimated time remaining

**Deployment Status Card:**
```json
{
  "type": "AdaptiveCard",
  "body": [
    {
      "type": "TextBlock",
      "text": "✅ Deployment Complete!",
      "weight": "Bolder",
      "color": "Good"
    },
    {
      "type": "FactSet",
      "facts": [
        { "title": "Resource Group:", "value": "my-aks-rg" },
        { "title": "Cluster Name:", "value": "my-aks-cluster" },
        { "title": "Nodes:", "value": "3" },
        { "title": "Status:", "value": "✅ Running" }
      ]
    }
  ],
  "actions": [
    {
      "type": "Action.OpenUrl",
      "title": "View in Azure Portal",
      "url": "https://portal.azure.com/#resource/..."
    }
  ]
}
```

## Integration Architecture

### API Endpoints for Template Management

The Web API exposes these endpoints:

```csharp
// GET /api/templates/gallery
// Returns adaptive card JSON for template gallery

// GET /api/templates/{id}/form  
// Returns adaptive card JSON for parameter form

// POST /api/templates/deploy
// Deploys template with parameters

// GET /api/templates/deployments/{id}/status
// Returns deployment status
```

### JavaScript Integration

The `template-manager.js` provides:

```javascript
class TemplateManager {
  constructor(apiBaseUrl) {
    this.apiBaseUrl = apiBaseUrl;
    this.adaptiveCards = new AdaptiveCards.AdaptiveCard();
  }

  async loadTemplateGallery() {
    const response = await fetch(`${this.apiBaseUrl}/api/templates/gallery`);
    return await response.json();
  }

  async loadTemplateForm(templateId) {
    const response = await fetch(`${this.apiBaseUrl}/api/templates/${templateId}/form`);
    return await response.json();
  }

  async deployTemplate(templateId, parameters) {
    const response = await fetch(`${this.apiBaseUrl}/api/templates/deploy`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ templateId, parameters })
    });
    return await response.json();
  }

  renderAdaptiveCard(cardJson, container) {
    const card = new AdaptiveCards.AdaptiveCard();
    card.parse(cardJson);
    const renderedCard = card.render();
    container.appendChild(renderedCard);
  }
}
```

## User Experience Flow

### Complete Workflow Example

1. **User arrives at Web UI**
   - Sees welcome message with template gallery option
   - Clicks "📦 Browse Templates"

2. **Template Gallery Loads**
   - Interactive card shows available templates
   - User clicks "Deploy AKS Template"

3. **Parameter Form Appears**
   - Adaptive card with input fields
   - User fills: cluster name, region, node count
   - Clicks "Deploy Now"

4. **Deployment Starts**
   - Real-time progress bar appears
   - Status updates every few seconds
   - User sees: "Creating resource group..." → "Configuring networking..."

5. **Deployment Completes**
   - Success card with resource details
   - Links to Azure Portal
   - Option to deploy another template

### Integration with Chat System

The template system integrates seamlessly with the existing chat:

**Natural Language Integration:**
- User: "I need a Kubernetes cluster for my web app"
- AI: Shows template gallery filtered for Kubernetes
- User: Selects template, fills parameters, deploys

**Context Awareness:**
- AI remembers previous deployments
- Suggests related templates
- Maintains deployment history

## Benefits of This Approach

### Rich Interactive Experience
- ✅ Visual template gallery instead of text lists
- ✅ Interactive forms with validation
- ✅ Real-time deployment tracking
- ✅ Professional, modern UI components

### Seamless Integration
- ✅ Works within existing chat interface
- ✅ No popup windows or external tools
- ✅ Maintains conversation context
- ✅ Mobile-responsive design

### Developer-Friendly
- ✅ Adaptive Cards are industry standard
- ✅ Easy to extend with new templates
- ✅ RESTful API design
- ✅ TypeScript/JavaScript integration

### Production Ready
- ✅ Error handling and validation
- ✅ Progress tracking and status updates
- ✅ Session management
- ✅ Scalable architecture

## Example Usage Scenarios

### Scenario 1: Quick Web App Deployment
```
User: "I need to deploy a web app quickly"
AI: Shows web app templates with database options
User: Selects "App Service + SQL Database" template
AI: Shows parameter form (app name, database size, region)
User: Fills form and clicks deploy
AI: Shows real-time deployment progress
Result: Running web app with database in 5 minutes
```

### Scenario 2: Complex Infrastructure
```
User: "Set up a production environment for microservices"
AI: Shows enterprise templates gallery
User: Selects "AKS + Service Mesh + Monitoring" template
AI: Shows advanced parameter form with networking options
User: Configures cluster size, networking, monitoring
AI: Deploys complete infrastructure with status tracking
Result: Production-ready Kubernetes environment
```

This integration provides the best of both worlds: the flexibility of natural language interaction with the precision and visual appeal of structured template deployment.
