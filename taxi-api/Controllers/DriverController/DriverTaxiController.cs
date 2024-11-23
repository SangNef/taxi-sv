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
                InUse = false,
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

        [Authorize]
        [HttpGet("list-taxis")]
        public async Task<IActionResult> ListAllTaxi()
        {
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

            var taxis = await _context.Taxies
                .Where(t => t.DriverId == driverId)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.LicensePlate,
                    t.Seat,
                    t.InUse,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = taxis,
                message = "Taxi list retrieved successfully."
            });
        }

        [Authorize]
        [HttpPut("update-inuse/{taxiId}")]
        public async Task<IActionResult> UpdateTaxiInUse(int taxiId)
        {
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

            // Kiểm tra xem taxi có thuộc về tài xế hay không
            var taxiToUpdate = await _context.Taxies
                .FirstOrDefaultAsync(t => t.Id == taxiId && t.DriverId == driverId);

            if (taxiToUpdate == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    data = (object)null,
                    message = "Taxi not found or does not belong to the driver."
                });
            }

            // Đặt tất cả các xe khác của tài xế thành InUse = false
            var driverTaxis = await _context.Taxies
                .Where(t => t.DriverId == driverId)
                .ToListAsync();

            foreach (var taxi in driverTaxis)
            {
                taxi.InUse = taxi.Id == taxiId;
                taxi.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = new { updatedTaxiId = taxiId },
                message = "Taxi in-use status updated successfully."
            });
        }

    }
}
