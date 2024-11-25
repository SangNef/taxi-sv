using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Controllers.DriverController
{
    [Authorize]
    [Route("api/driver/notification")]
    [ApiController]
    public class DriverNotificationController : ControllerBase
    {
        private readonly TaxiContext _context;

        public DriverNotificationController(TaxiContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetDriverNotifications()
        {
            // Lấy DriverId từ token đã được xác thực
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId");
            if (driverIdClaim == null)
            {
                return Unauthorized(new
                {
                    code = "Unauthorized",
                    message = "Invalid token. Driver ID is missing."
                });
            }

            if (!int.TryParse(driverIdClaim.Value, out int driverId))
            {
                return BadRequest(new
                {
                    code = "InvalidData",
                    message = "Invalid driver ID."
                });
            }

            // Truy vấn thông báo từ cơ sở dữ liệu
            var notifications = await _context.Notifications
                .Where(n => n.DriverId == driverId && n.DeletedAt == null)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    n.Title,
                    n.Content,
                    n.IsRead,
                    n.CreatedAt,
                    n.Navigate
                })
                .ToListAsync();

            return Ok(new
            {
                code = "Success",
                message = "Notifications retrieved successfully.",
                data = notifications
            });
        }

        [HttpPut("read/{id}")]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId");
            if (driverIdClaim == null || !int.TryParse(driverIdClaim.Value, out int driverId))
            {
                return Unauthorized(new
                {
                    code = "Unauthorized",
                    message = "Invalid token or driver ID is missing."
                });
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.DriverId == driverId);

            if (notification == null)
            {
                return NotFound(new
                {
                    code = "NotFound",
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
                code = "Success",
                message = "Notification marked as read successfully."
            });
        }

        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId");
            if (driverIdClaim == null || !int.TryParse(driverIdClaim.Value, out int driverId))
            {
                return Unauthorized(new
                {
                    code = "Unauthorized",
                    message = "Invalid token or driver ID is missing."
                });
            }

            var unreadNotifications = await _context.Notifications
                .Where(n => n.DriverId == driverId && n.IsRead == false)
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
                code = "Success",
                message = $"{unreadNotifications.Count} notifications marked as read successfully."
            });
        }



    }
}
