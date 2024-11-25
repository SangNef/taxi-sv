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
        private readonly IConfiguration configuation;
        public AdminNotificationController(TaxiContext context, IConfiguration configuation)
        {
            _context = context;
            this.configuation = configuation;
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

            // Trả về kết quả
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


    }
}
