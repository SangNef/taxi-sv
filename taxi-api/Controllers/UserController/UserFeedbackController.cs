using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;
using taxi_api.DTO;

[Route("api/[controller]")]
[ApiController]
public class UserFeedbackController : ControllerBase
{
    private readonly TaxiContext _context;

    public UserFeedbackController(TaxiContext context)
    {
        _context = context;
    }

    // POST api/user/feedback
    [HttpPost("feedback")]
    public async Task<IActionResult> CreateFeedback([FromBody] FeedbackDto feedbackDto)
    {
        if (feedbackDto == null)
        {
            return BadRequest(new
            {
                code = CommonErrorCodes.InvalidData,
                message = "Invalid data."
            });
        }

        // Check if the booking exists and its status is 4
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == feedbackDto.BookingId);

        if (booking == null)
        {
            return NotFound(new
            {
                code = CommonErrorCodes.NotFound,
                message = "Booking not found."
            });
        }

        if (booking.Status != "4") 
        {
            return BadRequest(new
            {
                code = CommonErrorCodes.InvalidData,
                message = "Feedback can only be submitted for bookings with status 4."
            });
        }

        // Create and store the feedback
        var feedback = new Feedback
        {
            BookingId = feedbackDto.BookingId,
            Rating = feedbackDto.Rating,
            Comment = feedbackDto.Comment,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Feedbacks.AddAsync(feedback);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            code = CommonErrorCodes.Success,
            message = "Feedback created successfully."
        });

        
    }
    [HttpGet]
    public async Task<IActionResult> GetAllFeedback([FromQuery] string bookingCode = null , [FromQuery] string customerName = null)
    {
        // Lọc các feedback dựa theo mã đặt xe hoặc tên khách hàng
        var query = _context.Feedbacks
            .Include(f => f.Booking)
            .ThenInclude(b => b.Customer)
            .AsQueryable();

        if (!string.IsNullOrEmpty(bookingCode))
        {
            query = query.Where(f => f.Booking.Code.Contains(bookingCode));
        }

        if (!string.IsNullOrEmpty(customerName))
        {
            query = query.Where(f => f.Booking.Customer.Name.Contains(customerName));
        }

        // Lấy kết quả và trả về
        var feedbacks = await query.Select(f => new
        {
            f.Id,
            f.Rating,
            f.Comment,
            f.CreatedAt,
            CustomerName = f.Booking.Customer.Name,
            BookingCode = f.Booking.Code
        }).ToListAsync();

        if (!feedbacks.Any())
        {
            return NotFound(new
            {
                code = "NOT_FOUND",
                message = "No feedback found for the given criteria."
            });
        }

        return Ok(new
        {
            code = "SUCCESS",
            data = feedbacks,
            message = "Feedbacks retrieved successfully."
        });
    }
}
