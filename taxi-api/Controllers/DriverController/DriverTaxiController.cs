using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using taxi_api.Models;
using taxi_api.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace taxi_api.Controllers.DriverController
{
    [Route("api/driver")]
    [ApiController]
    public class DriverTaxiController : ControllerBase
    {
        private readonly TaxiContext _context;

        public DriverTaxiController(TaxiContext context)
        {
            _context = context;
        }

        // POST: api/DriverTaxi/add-taxi
        [Authorize]
        [HttpPost("add-taxi")]
        public async Task<IActionResult> AddTaxi([FromBody] TaxiRequestDto request)
        {
            if (request == null || string.IsNullOrEmpty(request.Name) || string.IsNullOrEmpty(request.LicensePlate) || request.Seat <= 0)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid taxi information."
                });
            }

            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;
            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    data = (object)null,
                    message = "Unauthorized: Driver ID not found."
                });
            }

            // Kiểm tra xem tài xế có tồn tại không
            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    data = (object)null,
                    message = "Driver not found."
                });
            }

            var taxi = new Taxy
            {
                DriverId = driverId, 
                Name = request.Name,
                LicensePlate = request.LicensePlate,
                Seat = request.Seat,
                InUse = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.Taxies.AddAsync(taxi);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = new { taxiId = taxi.Id },
                message = "Taxi successfully created."
            });
        }

    }
}
