using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using CommonServices.Agents;
using CommonServices.Services;
using System.Runtime.CompilerServices;

namespace AgentAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TutorController : ControllerBase
    {
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly IConfiguration _configuration;
        private static readonly Dictionary<string, TutorAgent> _agents = new();

        public TutorController(IHubContext<ChatHub> hubContext, IConfiguration configuration)
        {
            _hubContext = hubContext;
            _configuration = configuration;
        }

        [HttpPost("api/ask")]
        public async Task<IActionResult> AskAgent([FromBody] string message)
        {
            // Simulate tool call
            await _hubContext.Clients.All.SendAsync("onToolCall", new {
                tool = "WebSearch",
                input = message,
                output = "Simulated result"
            });

            await Task.Delay(1000); // Simulate agent thinking

            await _hubContext.Clients.All.SendAsync("onFinalResponse", "Here’s your answer");

            return Ok();
        }
    }
}
