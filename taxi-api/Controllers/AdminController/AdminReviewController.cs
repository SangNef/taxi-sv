using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminReviewController : ControllerBase
    {
        private readonly TaxiContext _context;

        public AdminReviewController(TaxiContext context)
        {
            _context = context;
        }

        [HttpGet("list-feedback")]
        public async Task<IActionResult> ListAllFeedback([FromQuery] string code = null, [FromQuery] string nameOrPhone = null, [FromQuery] int? rate = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var feedbackQuery = _context.Reviews
                .Include(r => r.BookingDetail)
                .ThenInclude(bd => bd.Booking)
                .ThenInclude(b => b.Customer)
                .AsQueryable();

            // Tìm kiếm theo booking code (nếu có)
            if (!string.IsNullOrEmpty(code))
            {
                feedbackQuery = feedbackQuery.Where(r => r.BookingDetail.Booking.Code.Contains(code));
            }

            // Tìm kiếm theo customer name hoặc phone (nếu có)
            if (!string.IsNullOrEmpty(nameOrPhone))
            {
                feedbackQuery = feedbackQuery.Where(r =>
                    r.BookingDetail.Booking.Customer.Name.Contains(nameOrPhone) ||
                    r.BookingDetail.Booking.Customer.Phone.Contains(nameOrPhone));
            }

            // Tìm kiếm theo rate (nếu có)
            if (rate.HasValue)
            {
                feedbackQuery = feedbackQuery.Where(r => r.Rate == rate);
            }

            var totalRecords = await feedbackQuery.CountAsync();
            var feedbackList = await feedbackQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Review1,
                    r.Rate,
                    BookingCode = r.BookingDetail.Booking.Code,
                    CustomerName = r.BookingDetail.Booking.Customer.Name,
                    CustomerPhone = r.BookingDetail.Booking.Customer.Phone,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .ToListAsync();

            // Trả về kết quả với thông tin phân trang
            return Ok(new
            {
                code = 200,
                message = "Feedback list retrieved successfully",
                data = feedbackList,
                pagination = new
                {
                    currentPage = page,
                    pageSize,
                    totalRecords,
                    totalPages = (int)System.Math.Ceiling((double)totalRecords / pageSize)
                }
            });
        }

    }
}
