using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Brave;
using CommonServices.Services;

namespace CommonServices.Agents
{
    public class ChatAgent
    {
        private readonly Kernel _kernel;
        private ChatCompletionAgent? _chatAgent;
        private readonly AgentThread _thread;
        private readonly MemoryProvider _memoryProvider;
        private readonly PromptRenderer _promptRenderer;

        public ChatAgent(
            string modelId,
            string openaiApiKey,
            string braveApiKey,
            string mem0ApiKey
        )
        {
            // Create kernel with OpenAI chat completion
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(modelId, openaiApiKey);
            builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));
            
            _kernel = builder.Build();

            // Add search plugin
            var bingConnector = new BraveTextSearch(braveApiKey);
            var searchPlugin = bingConnector.CreateWithSearch("SearchPlugin");
            _kernel.Plugins.Add(searchPlugin);

            // Initialize services
            _memoryProvider = new MemoryProvider(mem0ApiKey);
            _promptRenderer = new PromptRenderer(_kernel);

            // Initialize the agent (async initialization will be handled separately)
            _thread = new ChatHistoryAgentThread();
        }

         public ChatCompletionAgent? GetAgent()
        {
            return _chatAgent ?? null;
        }


        public async Task<ChatCompletionAgent> InitializeAsync(string name, string template, Dictionary<string, object> args, string description = "")
        {
            // Clear previous memories
            await _memoryProvider.ClearProviderMemories();

            // Get memory provider and add to thread
            var mem0Provider = _memoryProvider.GetMem0Provider();
            _thread.AIContextProviders.Add(mem0Provider);

            var prompt = await _promptRenderer.RenderPrompt(template, args);

            // Enable planning
            OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            // Create the chat completion agent
            var chatAgent = new ChatCompletionAgent()
            {
                Name = name,
                Description = description,
                Instructions = prompt,
                Kernel = _kernel,
                Arguments = new KernelArguments(openAIPromptExecutionSettings)
            };

            // Store reference to the agent
            _chatAgent = chatAgent;

            return chatAgent;
        }

        public async IAsyncEnumerable<ChatMessageContent> GetResponseAsync(string userInput)
        {
            if (_chatAgent == null)
            {
                throw new InvalidOperationException("Agent not initialized. Call InitializeAsync() first.");
            }

            var message = new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, userInput);

            DateTime now = DateTime.Now;
            KernelArguments arguments = new()
            {
                { "now", $"{now.ToShortDateString()} {now.ToShortTimeString()}" }
            };

            // Use the non-streaming method for better tool call support
            // This gives us complete ChatMessageContent objects with proper metadata
            await foreach (var response in _chatAgent.InvokeAsync(message, _thread, options: new() { KernelArguments = arguments }))
            {
                yield return response;
            }
        }

        /// <summary>
        /// Token-level streaming for real-time text generation (without tool call detection)
        /// </summary>
        public async IAsyncEnumerable<string> GetTokenStreamAsync(string userInput)
        {
            if (_chatAgent == null)
            {
                throw new InvalidOperationException("Agent not initialized. Call InitializeAsync() first.");
            }

            var message = new ChatMessageContent(AuthorRole.User, userInput);

            DateTime now = DateTime.Now;
            KernelArguments arguments = new()
            {
                { "now", $"{now.ToShortDateString()} {now.ToShortTimeString()}" }
            };

            await foreach (StreamingChatMessageContent response in _chatAgent.InvokeStreamingAsync(message, _thread, options: new() { KernelArguments = arguments }))
            {
                if (!string.IsNullOrEmpty(response.Content))
                {
                    yield return response.Content;
                }
            }
        }
        public async Task ClearMemoryAsync()
        {
            await _memoryProvider.ClearProviderMemories();
        }
    }
}