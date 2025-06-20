using Microsoft.AspNetCore.SignalR;
using CommonServices.Agents;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Agents.Orchestration.Handoff;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;

namespace CommonServices.Services
{
    public enum AgentMode
    {
        AzureOnly,
        TutorOnly, 
        QuizOnly,
        HandoffOrchestration
    }

    public class ChatHub : Hub
    {
        private readonly Func<string, string, string, string, string, string, ChatAgent> _chatAgentFactory;
        private readonly Func<string, string, AzureChatAgent> _azureChatAgentFactory;
        private readonly IConfiguration _configuration;
        private static readonly Dictionary<string, bool> _initializedSessions = new();
        private static readonly Dictionary<string, AzureChatAgent> _azureAgents = new();
        private static readonly Dictionary<string, ChatAgent> _tutorAgents = new();
        private static readonly Dictionary<string, ChatAgent> _quizAgents = new();

        public ChatHub(
            Func<string, string, string, string, string, string, ChatAgent> chatAgentFactory,
            Func<string, string, AzureChatAgent> azureChatAgentFactory,
            IConfiguration configuration)
        {
            _azureChatAgentFactory = azureChatAgentFactory;
            _chatAgentFactory = chatAgentFactory;
            _configuration = configuration;
        }

        public override Task OnConnectedAsync()
        {
            Console.WriteLine($"Client connected: {Context.ConnectionId}");
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
            return base.OnDisconnectedAsync(exception);
        }

        public async Task ProcessMessage(string message, string sessionId, string agentMode = "AzureOnly")
        {
            try
            {
                // Parse string to enum
                if (!Enum.TryParse<AgentMode>(agentMode, true, out var parsedMode))
                {
                    parsedMode = AgentMode.AzureOnly; // Default fallback
                }

                // Ensure agents are initialized for this session
                if (!_initializedSessions.ContainsKey(sessionId))
                {
                    await InitializeAgentsForSession(sessionId);
                }

                var azureAgent = _azureAgents[sessionId];
                var tutorAgent = _tutorAgents[sessionId];
                var quizAgent = _quizAgents[sessionId];

                await Clients.Group(sessionId).SendAsync("StreamingStarted");

                switch (parsedMode)
                {
                    case AgentMode.AzureOnly:
                        await ProcessWithAIStreaming(sessionId, azureAgent.GetTokenStreamAsync(message));
                        break;
                    case AgentMode.TutorOnly:
                        await ProcessWithAIStreaming(sessionId, tutorAgent.GetTokenStreamAsync(message));
                        break;
                    case AgentMode.QuizOnly:
                        await ProcessWithAIStreaming(sessionId, quizAgent.GetTokenStreamAsync(message));
                        break;
                    case AgentMode.HandoffOrchestration:
                        await ProcessMessageWithHandoff(message, sessionId);
                        return; // Don't send StreamingCompleted twice
                    default:
                        throw new ArgumentException("Invalid AgentMode", nameof(agentMode));
                }

                await Clients.Group(sessionId).SendAsync("StreamingCompleted");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                await Clients.Group(sessionId).SendAsync("onError", new { error = ex.Message });
                await Clients.Group(sessionId).SendAsync("StreamingCompleted");
            }
        }

        private async Task ProcessMessageWithHandoff(string message, string sessionId)
        {
            try
            {
                var tutorAgent = _tutorAgents[sessionId].GetAgent();
                var quizAgent = _quizAgents[sessionId].GetAgent();

                if (tutorAgent != null && quizAgent != null)
                {
                    // Set up handoff relationships
                    var handoffs = OrchestrationHandoffs
                        .StartWith(tutorAgent)
                        .Add(tutorAgent, quizAgent, "Transfer to quiz agent when creating assessments or user requests quizzes")
                        .Add(quizAgent, tutorAgent, "Transfer back to tutor for explanations after quiz or for educational content");

                    // Create handoff orchestration without InteractiveCallback to prevent message loops
                    var orchestration = new HandoffOrchestration(
                        handoffs,
                        tutorAgent,
                        quizAgent);

                    // Start the runtime
                    var runtime = new InProcessRuntime();
                    await runtime.StartAsync();

                    try
                    {
                        await Clients.Group(sessionId).SendAsync("StreamingStarted");

                        // Invoke the orchestration and wait for completion
                        var orchestrationResult = await orchestration.InvokeAsync(message, runtime);
                        
                        // Get the final result from the orchestration
                        var result = await orchestrationResult.GetValueAsync();
                        
                        // Send only the final orchestration result as streaming chunks
                        if (!string.IsNullOrEmpty(result))
                        {
                            // Split the final result into words for a streaming effect
                            var words = result.Split(' ');
                            foreach (var word in words)
                            {
                                await Clients.Group(sessionId).SendAsync("ReceiveStreamingChunk", word + " ");
                                await Task.Delay(50); // Small delay for streaming effect
                            }
                            
                            // Also send as final response
                            await Clients.Group(sessionId).SendAsync("onFinalResponse", result);
                        }

                        await Clients.Group(sessionId).SendAsync("StreamingCompleted");
                    }
                    finally
                    {
                        // Clean up runtime
                        await runtime.DisposeAsync();
                    }
                }
                else
                {
                    throw new InvalidOperationException("One or more agents are not properly initialized");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message with handoff: {ex.Message}");
                await Clients.Group(sessionId).SendAsync("onError", new { error = ex.Message });
                await Clients.Group(sessionId).SendAsync("StreamingCompleted");
            }
        }

        private async Task InitializeAgentsForSession(string sessionId)
        {
            var modelId = _configuration["OpenAI:ModelId"] ?? "gpt-4o-mini";
            var openAiApiKey = _configuration["OpenAI:ApiKey"] ?? string.Empty;
            var braveApiKey = _configuration["Brave:ApiKey"] ?? string.Empty;
            var mem0ApiKey = _configuration["Mem0:ApiKey"] ?? string.Empty;
            var name = _configuration["ChatAgent:Name"] ?? "DefaultAgent";
            var instructions = _configuration["ChatAgent:Instructions"] ?? "Default instructions.";
            var foundryEndpoint = _configuration["AIFoundry:Endpoint"] ?? string.Empty;

            // Initialize Azure agent
            var azureAgent = _azureChatAgentFactory(modelId, foundryEndpoint);
            await azureAgent.InitializeAsync(agentId:"asst_mX8QoTHi8pBJelU0jMP0n17x");
            _azureAgents[sessionId] = azureAgent;

            // Initialize tutor agent
            var tutorAgent = _chatAgentFactory(modelId, openAiApiKey, braveApiKey, mem0ApiKey, name, instructions);
            await tutorAgent.InitializeAsync(
                name: "TutorAgent",
                template: """
                    You are a helpful tutor that will explain difficult concepts in {{$subject}}.

                    Here is the learning profile of the user you will be interacting with: {{$profile}}.

                    Be sure to use tools at your disposal to fetch latest information on this specific topic.
                """,
                args: new Dictionary<string, object>()
                {
                    {"subject","Information Theory"},
                    {"profile","The user has a comp sci background and prefers examples."}
                },
                description: "Educational tutor that explains complex concepts and provides learning assistance"
            );
            _tutorAgents[sessionId] = tutorAgent;

            // Initialize quiz agent
            var quizAgent = _chatAgentFactory(modelId, openAiApiKey, braveApiKey, mem0ApiKey, name, instructions);
            await quizAgent.InitializeAsync(
                name: "QuizAgent",
                template: """
                    You are an assistant that will create quizes to help test a users understadning on: {{$subject}}.

                    Here is the learning profile of the user you will be interacting with: {{$profile}}.

                    Be sure to use tools at your disposal to fetch lastest information on this specific topic for the quiz.
                """,
                args: new Dictionary<string, object>()
                {
                    {"subject","Information Theory"},
                    {"profile","The user has a comp sci background and prefers examples."}
                },
                description: "Quiz creation assistant that generates assessments and test questions"
            );
            _quizAgents[sessionId] = quizAgent;

            _initializedSessions[sessionId] = true;
        }

        private async Task ProcessWithAIStreaming(string sessionId, IAsyncEnumerable<string> tokenStream)
        {
            var fullResponse = new System.Text.StringBuilder();

            await foreach (var token in tokenStream)
            {
                // Send each token as it arrives for real-time effect
                await Clients.Group(sessionId).SendAsync("ReceiveStreamingChunk", token);
                fullResponse.Append(token);

                // Optional: Add small delay to see streaming effect (remove in production)
                await Task.Delay(10);
            }

            // Send final complete response
            var finalResponseText = fullResponse.ToString();
            if (!string.IsNullOrEmpty(finalResponseText))
            {
                await Clients.Group(sessionId).SendAsync("onFinalResponse", finalResponseText);
            }
        }

        public async Task SendToolCall(string tool, string input, string output)
        {
            await Clients.Caller.SendAsync("onToolCall", new { tool, input, output });
        }

        public async Task JoinSession(string sessionId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
            Console.WriteLine($"Client {Context.ConnectionId} joined session {sessionId}");
        }

        public async Task LeaveSession(string sessionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
            Console.WriteLine($"Client {Context.ConnectionId} left session {sessionId}");
        }
    }
}