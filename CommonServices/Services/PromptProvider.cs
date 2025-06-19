using Microsoft.SemanticKernel;

namespace CommonServices.Services
{
    public class PromptRenderer
    {
        private readonly Kernel _kernel;

        public PromptRenderer(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<string> RenderPrompt(string template, Dictionary<string, object> arguments, string? templateName = null)
        {
            var templateFactory = new KernelPromptTemplateFactory();
            var promptTemplateConfig = new PromptTemplateConfig()
            {
                Template = template,
                Name = templateName ?? "GenericPrompt"
            };

            // Convert Dictionary to KernelArguments
            var promptVars = new KernelArguments();
            foreach (var kvp in arguments)
            {
                promptVars[kvp.Key] = kvp.Value;
            }

            var promptTemplate = templateFactory.Create(promptTemplateConfig);
            var renderedPrompt = await promptTemplate.RenderAsync(this._kernel, promptVars);
            Console.WriteLine($"Rendered Prompt:\n{renderedPrompt}\n");

            return renderedPrompt;
        }
    }
}
