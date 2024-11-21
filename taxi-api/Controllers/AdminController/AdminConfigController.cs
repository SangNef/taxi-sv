using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using taxi_api.DTO;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/admin/config")]
    [ApiController]
    public class AdminConfigController : ControllerBase
    {
        private readonly TaxiContext _context;

        // Constructor để khởi tạo context
        public AdminConfigController(TaxiContext context)
        {
            _context = context;
        }

        [HttpGet("get-airport-price")]
        public IActionResult GetAirportPrice()
        {
            var airportPrice = _context.Configs
                .Where(c => c.ConfigKey == "airport_price")
                .Select(c => new
                {
                    c.Name,
                    c.Value,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.DeletedAt
                })
                .FirstOrDefault();

            if (airportPrice == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Airport price not found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Airport price retrieved successfully.",
                data = airportPrice
            });
        }

        [HttpGet("get-pickup-id-and-default")]
        public IActionResult GetPickupIdAndDefault()
        {
            var configValues = _context.Configs
                .Where(c => c.ConfigKey == "pickup_id" || c.ConfigKey == "default_arival_pickup")
                .Select(c => new
                {
                    c.ConfigKey,
                    c.Value,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.DeletedAt
                })
                .ToList();

            if (configValues.Count == 0)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "No matching pickup configuration values found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Pickup configuration values retrieved successfully.",
                data = configValues
            });
        }

        [HttpGet("get-dropoff-id-and-default")]
        public IActionResult GetDropoffIdAndDefault()
        {
            var configValues = _context.Configs
                .Where(c => c.ConfigKey == "dropoff_id" || c.ConfigKey == "default_arival_dropoff")
                .Select(c => new
                {
                    c.ConfigKey,
                    c.Value,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.DeletedAt
                })
                .ToList();

            if (configValues.Count == 0)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "No matching dropoff configuration values found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Dropoff configuration values retrieved successfully.",
                data = configValues
            });
        }

        [HttpPut("edit-airport-price")]
        public IActionResult EditAirportPrice([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Airport price value must be provided."
                });
            }

            var airportPriceConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "airport_price");

            if (airportPriceConfig == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Airport price configuration not found."
                });
            }

            // Cập nhật giá sân bay
            airportPriceConfig.Value = configDto.Value;
            airportPriceConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Airport price updated successfully.",
                    data = new { airportPrice = airportPriceConfig.Value }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating the airport price: {ex.Message}"
                });
            }
        }

        [HttpPut("edit-pickup-id-and-default")]
        public IActionResult EditPickupIdAndDefault([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Pickup ID and default arrival pickup value must be provided."
                });
            }

            var pickupIdConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "default_arival_pickup");

            var defaultArrivalPickupConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "default_arival_pickup");

            if (pickupIdConfig == null || defaultArrivalPickupConfig == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Pickup configuration not found."
                });
            }

            // Cập nhật Pickup ID và Default Arrival Pickup
            pickupIdConfig.Value = configDto.Value;
            defaultArrivalPickupConfig.Value = configDto.Value;
            pickupIdConfig.UpdatedAt = DateTime.UtcNow;
            defaultArrivalPickupConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Pickup configuration values updated successfully.",
                    data = new
                    {
                        pickupId = pickupIdConfig.Value,
                        defaultArrivalPickup = defaultArrivalPickupConfig.Value
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating the pickup configuration: {ex.Message}"
                });
            }
        }

        [HttpPut("edit-dropoff-id-and-default")]
        public IActionResult EditDropoffIdAndDefault([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Dropoff ID and default arrival dropoff value must be provided."
                });
            }

            var dropoffIdConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "default_arival_dropoff");

            var defaultArrivalDropoffConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "default_arival_dropoff");

            if (dropoffIdConfig == null || defaultArrivalDropoffConfig == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Dropoff configuration not found."
                });
            }

            // Cập nhật Dropoff ID và Default Arrival Dropoff
            dropoffIdConfig.Value = configDto.Value;
            defaultArrivalDropoffConfig.Value = configDto.Value;
            dropoffIdConfig.UpdatedAt = DateTime.UtcNow;
            defaultArrivalDropoffConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Dropoff configuration values updated successfully.",
                    data = new
                    {
                        dropoffId = dropoffIdConfig.Value,
                        defaultArrivalDropoff = defaultArrivalDropoffConfig.Value
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating the dropoff configuration: {ex.Message}"
                });
            }
        }
    }
}
