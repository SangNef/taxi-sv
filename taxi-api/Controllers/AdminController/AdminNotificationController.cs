using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/notification")]
    [ApiController]
    public class AdminNotificationController : ControllerBase
    {
        private readonly TaxiContext _context;
        private readonly IConfiguration _configuration;

        public AdminNotificationController(TaxiContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet("get-notices")]
        public async Task<IActionResult> GetNotices(bool? isRead = null, int page = 1, int pageSize = 10)
        {
            var query = _context.AdminNotifications.AsQueryable();

            if (isRead.HasValue)
            {
                query = query.Where(n => n.IsRead == isRead.Value);
            }

            var totalNotices = await query.CountAsync();

            var notices = await query
                .Where(n => n.DeletedAt == null)
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Content,
                    n.Navigate,
                    n.IsRead,
                    n.CreatedAt,
                    n.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Notifications retrieved successfully.",
                data = notices,
                pagination = new
                {
                    totalNotices,
                    currentPage = page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)totalNotices / pageSize)
                }
            });
        }

        [HttpPut("read/{id}")]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            var notification = await _context.AdminNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.DeletedAt == null);

            if (notification == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Notification not found."
                });
            }

            if (notification.IsRead == true)
            {
                return Ok(new
                {
                    code = "AlreadyRead",
                    message = "Notification is already marked as read."
                });
            }

            notification.IsRead = true;
            notification.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Notification marked as read successfully."
            });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var unreadNotifications = await _context.AdminNotifications
                .Where(n => n.IsRead == false && n.DeletedAt == null)
                .ToListAsync();

            if (!unreadNotifications.Any())
            {
                return Ok(new
                {
                    code = "NoUnreadNotifications",
                    message = "No unread notifications found."
                });
            }

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
                notification.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = $"{unreadNotifications.Count} notifications marked as read successfully."
            });
        }
    }
}
