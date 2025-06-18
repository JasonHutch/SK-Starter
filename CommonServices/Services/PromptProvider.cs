using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.PromptTemplates;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using System.Threading.Tasks;

namespace CommonServices.Services
{
    public class PromptRenderer
    {
        private readonly Kernel _kernel;

        public PromptRenderer(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<string> RenderTutorPrompt()
        {
            string test = """
            You are an assistant that helps users with learning {{$subject}} be sure to:
            - Ask users questions to test and solidify understanding
            - Break concepts down according to memories and perfernces from student profile

            Here is the students learning profile:
            {{$learningProfile}}
            """;
            var templateFactory = new KernelPromptTemplateFactory();
            var promptTemplateConfig = new PromptTemplateConfig()
            {
                Template = test,
                Name = "TutorChatPrompt"
            };

            // Input data for the prompt rendering and execution
            var promptVars = new KernelArguments()
            {
                { "subject", "CFA Exam"},
                { "learningProfile","User is a hands on learner, likes realworld examples, has software development background. Likes to understand and build on foundational concepts, likes resources to reference."}
            };

            // Render the prompt
            var promptTemplate = templateFactory.Create(promptTemplateConfig);
            var renderedPrompt = await promptTemplate.RenderAsync(this._kernel, promptVars);
            Console.WriteLine($"Rendered Prompt:\n{renderedPrompt}\n");

            return renderedPrompt;
        }
    }
}
