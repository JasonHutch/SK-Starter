using System.ComponentModel;
using System.Net.Http.Headers;
using Microsoft.SemanticKernel.Memory;

namespace CommonServices.Services
{
    public class MemoryProvider
    {
        private readonly Mem0Provider mem0Provider;

        public MemoryProvider(string mem0Key)
        {
            var httpClient = new HttpClient()
            {
                BaseAddress = new Uri("https://api.mem0.ai")
            };

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", mem0Key);

            this.mem0Provider = new Mem0Provider(httpClient, options: new()
            {
                UserId = "U1"
            });
        }

        public async Task ClearProviderMemories()
        {
            await this.mem0Provider.ClearStoredMemoriesAsync();
        }

        public Mem0Provider GetMem0Provider()
        {
            return this.mem0Provider;
        }
    }
}