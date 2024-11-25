using Microsoft.AspNetCore.Mvc;
using taxi_api.Models;
using taxi_api.DTO;

namespace taxi_api.Controllers.DriverController
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly TaxiContext _context; 

        public TicketsController(TaxiContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTicket([FromBody] CreateTicketDto request)
        {
            // Kiểm tra BookingId có tồn tại không
            var booking = await _context.Bookings.FindAsync(request.BookingId);
            if (booking == null)
            {
                return Ok(new { message = "Booking not found." });
            }
          
            // Tạo đối tượng Ticket mới
            var ticket = new Ticket
            {
                BookingId = request.BookingId,
                Content = request.Content
            };

            // Lưu vào cơ sở dữ liệu
            await _context.Tickets.AddAsync(ticket);
            await _context.SaveChangesAsync();

            // Trả về kết quả
            return Ok(new
            {
                message = "Ticket created successfully.",
                ticket = new
                {
                    ticket.Id,
                    ticket.BookingId,
                    ticket.Content
                }
            });
        }
    }
}
