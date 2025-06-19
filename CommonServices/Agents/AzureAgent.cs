using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Brave;
using CommonServices.Services;
using Azure.AI.Agents.Persistent;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Azure.Identity;

namespace CommonServices.Agents
{
    public class AzureAgent
    {
        private AzureAIAgent? _azureAIAgent;
        private AzureAIAgentThread? _thread;

        private readonly string _modelId;

        private readonly PersistentAgentsClient _client;

        public AzureAgent(string modelId, string foundryEndpoint)
        {
            _client = AzureAIAgent.CreateAgentsClient(foundryEndpoint, new AzureCliCredential());
            _modelId = modelId;
        }

        public async Task InitializeAsync()
        {
            PersistentAgent definition = await _client.Administration.CreateAgentAsync(
                model: _modelId,
                name: "NBA Agent",
                description: "An agent that talks to you about basketball",
                instructions: "You are a friendly agent that talks basketball with the user"
            );

            _azureAIAgent = new(definition, _client);
            _thread = new(_azureAIAgent.Client);

        }

        public async IAsyncEnumerable<ChatMessageContent> GetResponseAsync(string userInput)
        {
            if (_azureAIAgent != null)
            {
                try
                {
                    ChatMessageContent message = new(AuthorRole.User, userInput);
                    await foreach (ChatMessageContent response in _azureAIAgent.InvokeAsync(message, _thread))
                    {
                        if (response != null)
                        {
                            yield return response;
                        }
                    }
                }
                finally
                {
                    if (_thread != null)
                    {
                        await _thread.DeleteAsync();
                    }
                }
            }
        }

        public async IAsyncEnumerable<string> GetTokenStreamAsync(string userInput)
        {
            if (_azureAIAgent == null)
            {
                throw new InvalidOperationException("Agent not initialized. Call InitializeAsync() first.");
            }

            var message = new ChatMessageContent(AuthorRole.User, userInput);

            DateTime now = DateTime.Now;
            KernelArguments arguments = new()
            {
                { "now", $"{now.ToShortDateString()} {now.ToShortTimeString()}" }
            };

            await foreach (StreamingChatMessageContent response in _azureAIAgent.InvokeStreamingAsync(message, _thread, options: new() {KernelArguments = arguments}))
            {
                if (response.Content != null)
                {
                    yield return response.Content;
                }
            }

        }
    }
}