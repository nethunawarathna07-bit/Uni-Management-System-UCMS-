using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UniManage.Data;
using UniManage.Models;

namespace UniManage.Controllers
{
    [Authorize]
    public class MessageController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessageController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Inbox - view all received messages
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var messages = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m => m.ReceiverId == user.Id || m.SenderId == user.Id)
                .OrderByDescending(m => m.SentDate)
                .ToListAsync();

            ViewBag.CurrentUserId = user.Id;
            ViewBag.UnreadCount = messages.Count(m => m.ReceiverId == user.Id && !m.IsRead);
            return View(messages);
        }

        // Compose new message - GET
        public async Task<IActionResult> Compose(string receiverId = "")
        {
            var user = await _userManager.GetUserAsync(User);

            // Get all users except current user to message
            var allUsers = _userManager.Users
                .Where(u => u.Id != user.Id)
                .ToList();

            ViewBag.Users = allUsers;
            ViewBag.PreselectedReceiverId = receiverId;
            return View();
        }

        // Compose new message - POST
        [HttpPost]
        public async Task<IActionResult> Compose(string receiverId, string content)
        {
            var user = await _userManager.GetUserAsync(User);

            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(receiverId))
            {
                TempData["Error"] = "Please select a recipient and write a message.";
                return RedirectToAction("Compose");
            }

            var message = new Message
            {
                SenderId = user.Id,
                ReceiverId = receiverId,
                Content = content,
                SentDate = DateTime.Now,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Message sent successfully!";
            return RedirectToAction("Index");
        }

        // View a single message thread
        public async Task<IActionResult> View(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            var message = await _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .FirstOrDefaultAsync(m => m.MessageId == id);

            if (message == null) return NotFound();

            // Mark as read if current user is receiver
            if (message.ReceiverId == user.Id && !message.IsRead)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }

            ViewBag.CurrentUserId = user.Id;
            return View(message);
        }

        // Delete a message
        public async Task<IActionResult> Delete(int id)
        {
            var message = await _context.Messages.FindAsync(id);
            if (message != null)
            {
                _context.Messages.Remove(message);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Message deleted.";
            }
            return RedirectToAction("Index");
        }

        // Reply to a message - GET
        public async Task<IActionResult> Reply(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var original = await _context.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.MessageId == id);

            if (original == null) return NotFound();

            var allUsers = _userManager.Users
                .Where(u => u.Id != user.Id)
                .ToList();

            ViewBag.Users = allUsers;
            ViewBag.PreselectedReceiverId = original.SenderId;
            ViewBag.OriginalMessage = original;
            return View("Compose");
        }
    }
}