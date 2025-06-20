using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Azure.AI.Agents.Persistent;
using Microsoft.SemanticKernel.Agents.AzureAI;
using Azure.Identity;

namespace CommonServices.Agents
{
    public class AzureChatAgent
    {
        private AzureAIAgent? _azureAIAgent;
        private AzureAIAgentThread? _thread;
        private readonly string _modelId;
        private readonly PersistentAgentsClient _client;
        public AzureChatAgent(string modelId, string foundryEndpoint)
        {
            _client = AzureAIAgent.CreateAgentsClient(foundryEndpoint, new AzureCliCredential());
            _modelId = modelId;
        }

        public AzureAIAgent? GetAgent()
        {
            return _azureAIAgent ?? null;
        }

        //TODO: Accept agent configured in Azure, default to NBA agent if none provided
        public async Task<AzureAIAgent> InitializeAsync(string agentId)
        {
            PersistentAgent? agentDefinition = await _client.Administration.GetAgentAsync(agentId);

            _azureAIAgent = new(agentDefinition, _client);
            _thread = new(_azureAIAgent.Client);

            return _azureAIAgent;
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