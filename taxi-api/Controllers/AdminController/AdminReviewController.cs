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
        public async Task<IActionResult> ListAllFeedback([FromQuery] string Code = null, [FromQuery] string Name = null, [FromQuery] int? rate = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // Truy vấn tất cả các phản hồi (review)
            var feedbackQuery = _context.Reviews
                .Include(r => r.BookingDetail)
                .ThenInclude(bd => bd.Booking) // Lấy thông tin Booking
                .ThenInclude(b => b.Customer)  // Lấy thông tin Customer từ Booking
                .AsQueryable();

            // Tìm kiếm theo booking code (nếu có)
            if (!string.IsNullOrEmpty(Code))
            {
                feedbackQuery = feedbackQuery.Where(r => r.BookingDetail.Booking.Code.Contains(Code));
            }

            // Tìm kiếm theo customer name (nếu có)
            if (!string.IsNullOrEmpty(Name))
            {
                feedbackQuery = feedbackQuery.Where(r => r.BookingDetail.Booking.Customer.Name.Contains(Name));
            }

            // Tìm kiếm theo rate (nếu có)
            if (rate.HasValue)
            {
                feedbackQuery = feedbackQuery.Where(r => r.Rate == rate);
            }

            // Phân trang dữ liệu
            var totalRecords = await feedbackQuery.CountAsync();
            var feedbackList = await feedbackQuery
                .Skip((page - 1) * pageSize) // Bỏ qua các phần tử đã hiển thị
                .Take(pageSize) // Lấy các phần tử cần thiết
                .Select(r => new
                {
                    r.Id,
                    r.Review1,
                    r.Rate,
                    BookingCode = r.BookingDetail.Booking.Code,
                    CustomerName = r.BookingDetail.Booking.Customer.Name,
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
                    page,
                    pageSize,
                    totalRecords,
                    totalPages = (int)System.Math.Ceiling((double)totalRecords / pageSize)
                }
            });
        }

    }
}
