using Microsoft.AspNetCore.SignalR;
using CommonServices.Agents;
using Microsoft.SemanticKernel;

namespace CommonServices.Services
{
    public class ChatHub : Hub
    {
        private readonly TutorAgent _tutorAgent;
        private static readonly Dictionary<string, bool> _initializedSessions = new();

        public ChatHub(TutorAgent tutorAgent)
        {
            _tutorAgent = tutorAgent;
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

        public async Task ProcessMessage(string message, string sessionId)
        {
            Console.WriteLine("MESSAGE");
            try
            {
                // Ensure agent is initialized for this session
                if (!_initializedSessions.ContainsKey(sessionId))
                {
                    await _tutorAgent.InitializeAsync();
                    _initializedSessions[sessionId] = true;
                }

                // Step 1: Signal streaming is starting
                await Clients.Group(sessionId).SendAsync("StreamingStarted");
                
                // Step 2: Process with your AI (Semantic Kernel)
                await ProcessWithAIStreaming(message, sessionId);
                
                // Step 3: Signal streaming is complete
                await Clients.Group(sessionId).SendAsync("StreamingCompleted");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing message: {ex.Message}");
                await Clients.Group(sessionId).SendAsync("onError", new { error = ex.Message });
                await Clients.Group(sessionId).SendAsync("StreamingCompleted");
            }
        }

        // private async Task ProcessWithAI(string message, string sessionId)
        // {
        //     var responses = new List<string>();

        //     await foreach (var response in _tutorAgent.GetResponseAsync(message))
        //     {
        //         // Debug: Log response details
        //         Console.WriteLine($"Response Content: {response.Content}");
        //         Console.WriteLine($"Response Role: {response.Role}");
        //         Console.WriteLine($"Response ModelId: {response.ModelId}");
        //         Console.WriteLine($"Response Metadata Count: {response.Metadata?.Count}");
                
        //         if (response.Metadata != null)
        //         {
        //             foreach (var item in response.Metadata)
        //             {
        //                 Console.WriteLine($"Metadata Key: {item.Key}, Value: {item.Value}");
        //             }
        //         }

        //         // Check if this is a tool call response
        //         bool isToolCall = false;
        //         string functionName = "Unknown";
        //         string functionArgs = "";
        //         string functionResult = "";

        //         // Method 1: Check if role is "tool" (for function results)
        //         if (string.Equals(response.Role.ToString(), "tool", StringComparison.OrdinalIgnoreCase))
        //         {
        //             isToolCall = true;
        //             functionResult = response.Content ?? "";
                    
        //             // Try to extract function name from metadata
        //             if (response.Metadata?.ContainsKey("Function") == true)
        //             {
        //                 functionName = response.Metadata["Function"]?.ToString() ?? "Unknown";
        //             }
        //             if (response.Metadata?.ContainsKey("Arguments") == true)
        //             {
        //                 functionArgs = response.Metadata["Arguments"]?.ToString() ?? "";
        //             }
        //         }
                
        //         // Method 2: Check for function call information in metadata
        //         else if (response.Metadata?.ContainsKey("Function") == true)
        //         {
        //             isToolCall = true;
        //             functionName = response.Metadata["Function"]?.ToString() ?? "Unknown";
        //             functionArgs = response.Metadata.ContainsKey("Arguments") ? response.Metadata["Arguments"]?.ToString() : "";
        //             functionResult = response.Content ?? "";
        //         }

        //         if (isToolCall)
        //         {
        //             // Send tool call update to the session group
        //             await Clients.Group(sessionId).SendAsync("onToolCall", new
        //             {
        //                 tool = functionName,
        //                 input = functionArgs,
        //                 output = functionResult
        //             });
                    
        //             Console.WriteLine($"Tool call sent: {functionName}");
        //         }

        //         // Stream response chunks as they come
        //         if (!string.IsNullOrEmpty(response.Content))
        //         {
        //             responses.Add(response.Content);
                    
        //             // Send streaming chunk to frontend
        //             await Clients.Group(sessionId).SendAsync("ReceiveStreamingChunk", response.Content);
        //         }
        //     }

        //     // Send final complete response
        //     var finalResponse = string.Join(" ", responses);
        //     if (!string.IsNullOrEmpty(finalResponse))
        //     {
        //         await Clients.Group(sessionId).SendAsync("onFinalResponse", finalResponse);
        //     }
        // }

        /// <summary>
        /// Real-time token streaming version - faster but no tool call detection
        /// </summary>
        private async Task ProcessWithAIStreaming(string message, string sessionId)
        {
            var fullResponse = new System.Text.StringBuilder();
            
            await foreach (var token in _tutorAgent.GetTokenStreamAsync(message))
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