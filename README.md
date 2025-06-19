# Semantic Kernel C# Starter

A comprehensive C# starter template demonstrating Azure AI Foundry agent integration with real-time SignalR communication and multi-agent orchestration capabilities.

## 🚀 Features

- **Multi-Agent Support**: Azure AI Foundry agents, OpenAI-powered tutor and quiz agents
- **Agent Orchestration**: Intelligent handoff between agents using Semantic Kernel
- **Real-time Communication**: SignalR hub for streaming responses
- **Multiple Agent Modes**: 
  - `AzureOnly` - Azure AI Foundry agent only
  - `TutorOnly` - Educational tutor agent
  - `QuizOnly` - Quiz generation agent  
  - `HandoffOrchestration` - Intelligent agent switching
- **Azure Integration**: Built-in Application Insights telemetry and monitoring
- **Modern .NET**: Built on .NET 9 with nullable reference types
- **CORS Ready**: Pre-configured for frontend integration

## 📋 Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Azure AI Foundry](https://ai.azure.com/) project and endpoint
- [Azure Application Insights](https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview) (optional but recommended)
- API keys for external services (OpenAI, Brave Search, Mem0 - optional)

## ⚙️ Setup Instructions

### 1. Clone the Repository

```bash
git clone <repository-url>
cd MultiAgentFun-Csharp
```

### 2. Azure AI Foundry Setup

1. Create an [Azure AI Foundry](https://ai.azure.com/) project
2. Deploy a chat completion model (e.g., GPT-4o, GPT-3.5-turbo)
3. Note your project endpoint URL (format: `https://your-project.services.ai.azure.com/api/projects/YourProject`)

### 3. Configure Application Settings

Update `AgentAPI/appsettings.json` with your configuration:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "your-application-insights-connection-string"
  },
  "OpenAI": {
    "ModelId": "gpt-4o",
    "ApiKey": "your-openai-api-key"
  },
  "Brave": {
    "ApiKey": "your-brave-search-api-key"
  },
  "Mem0": {
    "ApiKey": "your-mem0-api-key"
  },
  "AIFoundry": {
    "Endpoint": "https://your-project.services.ai.azure.com/api/projects/YourProject"
  }
}
```

### 4. Azure Authentication

The application uses Azure Default Credential for authentication. Set up authentication using one of these methods:

**Option A: Azure CLI (Recommended for development)**
```bash
az login
```

**Option B: Environment Variables**
```bash
export AZURE_CLIENT_ID="your-client-id"
export AZURE_CLIENT_SECRET="your-client-secret"
export AZURE_TENANT_ID="your-tenant-id"
```

**Option C: Managed Identity** (when deployed to Azure)

### 5. Install Dependencies

```bash
dotnet restore
```

## 🏃‍♂️ Running the Application

### Development Mode

```bash
cd AgentAPI
dotnet run
```

The API will be available at:
- HTTP: `http://localhost:5038`
- HTTPS: `https://localhost:7095`

### SignalR Hub Endpoint

The SignalR hub is available at: `/chathub`

## 📡 API Usage

### SignalR Connection

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5038/chathub")
    .build();

await connection.start();
```

### Joining a Session

```javascript
await connection.invoke("JoinSession", "your-session-id");
```

### Sending Messages

**Basic (Azure only):**
```javascript
await connection.invoke("ProcessMessage", "Hello!", "session-123");
```

**With specific agent mode:**
```javascript
await connection.invoke("ProcessMessage", "Create a quiz about C#", "session-123", "QuizOnly");
```

**With intelligent orchestration:**
```javascript
await connection.invoke("ProcessMessage", "I need help with programming", "session-123", "HandoffOrchestration");
```

### Receiving Responses

```javascript
// Real-time streaming chunks
connection.on("ReceiveStreamingChunk", (chunk) => {
    console.log("Streaming:", chunk);
});

// Final complete response
connection.on("onFinalResponse", (response) => {
    console.log("Complete response:", response);
});

// Stream status
connection.on("StreamingStarted", () => console.log("Started"));
connection.on("StreamingCompleted", () => console.log("Completed"));

// Error handling
connection.on("onError", (error) => console.error("Error:", error));
```

## 🏗️ Project Structure

```
MultiAgentFun-Csharp/
├── AgentAPI/                           # Main Web API project
│   ├── Controllers/                    # API controllers
│   ├── Program.cs                      # Application startup
│   ├── appsettings.json               # Configuration
│   └── AgentAPI.csproj                # Project file
├── CommonServices/                     # Shared services library
│   ├── Agents/                        # Agent implementations
│   │   ├── AzureChatAgent.cs         # Azure AI Foundry agent
│   │   └── ChatAgent.cs              # OpenAI-based agents
│   ├── Services/                      # SignalR and other services
│   │   └── ChatHub.cs                # SignalR hub implementation
│   └── CommonServices.csproj         # Shared library project
└── MultiAgentFun-Csharp.sln          # Solution file
```

## 🔧 Agent Modes Explained

### AzureOnly Mode
- Uses Azure AI Foundry deployed models
- Best for general conversational AI
- Leverages Azure's managed infrastructure

### TutorOnly Mode  
- Educational tutor specializing in complex concepts
- Uses web search for latest information
- Customizable for different subjects

### QuizOnly Mode
- Generates quizzes and assessments
- Creates test questions based on topics
- Integrates with web search for current content

### HandoffOrchestration Mode
- Intelligently routes messages between agents
- Azure agent handles general queries
- Seamless handoff to tutor/quiz agents as needed
- Bidirectional communication between specialized agents

## 🔌 Frontend Integration

The API includes CORS configuration for frontend frameworks. Common origins are pre-configured:

```csharp
// Supported frontend origins
"http://localhost:3000"   // React default
"http://localhost:8080"   // Vue default
// Additional ports as needed
```

## 📊 Monitoring & Telemetry

The application includes comprehensive telemetry via Azure Application Insights:

- **Traces**: Semantic Kernel operations
- **Metrics**: Agent performance and usage
- **Logs**: Detailed application logging
- **Dependencies**: External API calls

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Commit your changes: `git commit -m 'Add amazing feature'`
4. Push to the branch: `git push origin feature/amazing-feature`
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Resources

- [Azure AI Foundry Documentation](https://docs.microsoft.com/en-us/azure/ai-foundry/)
- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [SignalR Documentation](https://docs.microsoft.com/en-us/aspnet/core/signalr/)
- [.NET 9 Documentation](https://docs.microsoft.com/en-us/dotnet/)

---

**Happy coding! 🎉**
