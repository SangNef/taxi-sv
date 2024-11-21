using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using taxi_api.Models;
using taxi_api.DTO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace taxi_api.Controllers.DriverController
{
    [Route("api/driver")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly TaxiContext _context;

        public PaymentController(TaxiContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [Authorize]
        [HttpPost("increase-price")]
        public async Task<IActionResult> IncreasePrice([FromBody] IncreasePriceDto increasePriceDto)
        {
            if (increasePriceDto == null)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid price data."
                });
            }

            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId");
            if (driverIdClaim == null || !int.TryParse(driverIdClaim.Value, out int driverId))
            {
                return Unauthorized(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Invalid token. Driver ID is missing."
                });
            }

            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Driver not found."
                });
            }

            // Kiểm tra nếu Price có giá trị hợp lệ
            if (increasePriceDto.IncreaseAmount <= 0)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Increase amount must be greater than zero."
                });
            }

            driver.Price = (driver.Price ?? 0) + increasePriceDto.IncreaseAmount;
            driver.UpdatedAt = DateTime.Now;

            _context.Drivers.Update(driver);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Price increased successfully.",
                data = new
                {
                    driver.Id,
                    driver.Fullname,
                    driver.Price,
                    driver.UpdatedAt
                }
            });
        }
    }
}
