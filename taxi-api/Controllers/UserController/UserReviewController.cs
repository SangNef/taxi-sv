using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using taxi_api.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using taxi_api.DTO;
using Microsoft.EntityFrameworkCore;

namespace taxi_api.Controllers.UserController
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserReviewController : ControllerBase
    {
        private readonly TaxiContext _context;

        public UserReviewController(TaxiContext context)
        {
            _context = context;
        }

        // Hàm tạo review mới
        [HttpPost("create")]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto reviewDto)
        {
            if (reviewDto == null)
            {
                return BadRequest(new { message = "Review data cannot be null" });
            }

            // Kiểm tra các dữ liệu cần thiết có hợp lệ không
            if (string.IsNullOrWhiteSpace(reviewDto.Review1) || reviewDto.Rate == null || reviewDto.Rate < 1 || reviewDto.Rate > 5)
            {
                return BadRequest(new { message = "Invalid review or rating" });
            }

            // Kiểm tra trạng thái của booking
            var bookingDetail = await _context.BookingDetails
                .Include(bd => bd.Booking) 
                .Where(bd => bd.Id == reviewDto.BookingDetailId)
                .FirstOrDefaultAsync();

            if (bookingDetail == null)
            {
                return NotFound(new { message = "Booking detail not found" });
            }

            if (bookingDetail.Booking == null || bookingDetail.Booking.Status != "4" && bookingDetail.Status != "2")
            {
                return BadRequest(new { message = "Review can only be created for booking status 4" });
            }

            // Tạo đối tượng Review từ DTO
            var review = new Review
            {
                BookingDetailId = reviewDto.BookingDetailId,
                Review1 = reviewDto.Review1,
                Rate = reviewDto.Rate,
                CreatedAt = DateTime.UtcNow, 
                UpdatedAt = DateTime.UtcNow, 
            };

            // Lưu vào cơ sở dữ liệu
            await _context.Reviews.AddAsync(review);
            await _context.SaveChangesAsync();

            // Trả về thông báo thành công
            return Ok(new
            {
                code = 200,
                message = "Review created successfully",
                data = new { review.Id }  // Trả về ID của review đã được tạo
            });
        }

        [HttpGet("list-feedback")]
        public async Task<IActionResult> ListAllFeedback([FromQuery] string bookingCode = null, [FromQuery] string customerName = null, [FromQuery] int? rate = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            // Truy vấn tất cả các phản hồi (review)
            var feedbackQuery = _context.Reviews
                .Include(r => r.BookingDetail)
                .ThenInclude(bd => bd.Booking) // Lấy thông tin Booking
                .ThenInclude(b => b.Customer)  // Lấy thông tin Customer từ Booking
                .AsQueryable();

            // Tìm kiếm theo booking code (nếu có)
            if (!string.IsNullOrEmpty(bookingCode))
            {
                feedbackQuery = feedbackQuery.Where(r => r.BookingDetail.Booking.Code.Contains(bookingCode));
            }

            // Tìm kiếm theo customer name (nếu có)
            if (!string.IsNullOrEmpty(customerName))
            {
                feedbackQuery = feedbackQuery.Where(r => r.BookingDetail.Booking.Customer.Name.Contains(customerName));
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
