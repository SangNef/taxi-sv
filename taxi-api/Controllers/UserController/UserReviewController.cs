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
                return Ok(new { message = "Review data cannot be null" });
            }

            if (string.IsNullOrWhiteSpace(reviewDto.Review1) || reviewDto.Rate == null || reviewDto.Rate < 1 || reviewDto.Rate > 5)
            {
                return Ok(new { message = "Invalid review or rating" });
            }

            var bookingDetail = await _context.BookingDetails
                .Include(bd => bd.Booking) 
                .Where(bd => bd.Id == reviewDto.BookingDetailId)
                .FirstOrDefaultAsync();

            if (bookingDetail == null)
            {
                return Ok(new { message = "Booking detail not found" });
            }

            if (bookingDetail.Booking == null ||  bookingDetail.Status != "4")
            {
                return Ok(new { message = "Review can only be created for booking status 4" });
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

       }
}
