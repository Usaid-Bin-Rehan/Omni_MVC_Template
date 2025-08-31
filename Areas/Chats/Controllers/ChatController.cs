using Microsoft.AspNetCore.Mvc;
using Omni_MVC_2.Areas.Chats.Models;
using Omni_MVC_2.Utilities.SemanticKernelUtilities.AIChatService;
using System.Text;

namespace Omni_MVC_2.Areas.Chats.Controllers
{
    [Area("Chats")]
    public class ChatController : Controller
    {
        private readonly IAIChatService _chat;

        public ChatController(IAIChatService chat)
        {
            _chat = chat;
        }

        // GET /Chats/Chat
        [HttpGet]
        public IActionResult Index()
        {
            var history = _chat.GetHistory();
            List<ChatMessageVM> model = history.Select(m => new ChatMessageVM { Role = m.Role.ToString(), Content = m.Content ?? string.Empty }).ToList();
            return View(model);
        }

        // Optional: return history as JSON for live refresh
        [HttpGet]
        public IActionResult GetHistory()
        {
            var history = _chat.GetHistory();
            List<ChatMessageVM> dto = history.Select(m => new ChatMessageVM { Role = m.Role.ToString(), Content = m.Content ?? string.Empty}).ToList();
            return Json(dto);
        }

        // POST /Chats/Chat/Ask
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ask(string message, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(message)) return BadRequest(new { ok = false, error = "Message required." });
            try
            {
                var answer = await _chat.AskAsync(message, ct);
                return Json(new { ok = true, answer });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new { ok = false, error = "LLM unavailable", detail = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult Export() => File(Encoding.UTF8.GetBytes(_chat.ExportToMarkdown()), "text/markdown", "conversation.md");
    }
}