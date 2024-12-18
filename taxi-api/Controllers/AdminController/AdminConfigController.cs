﻿using Microsoft.AspNetCore.Mvc;
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
                return Ok(new
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
                return Ok(new
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
                return Ok(new
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
        [HttpGet("get-main-color")]
        public IActionResult GetMainColor()
        {
            var mainColor = _context.Configs
                .Where(c => c.ConfigKey == "main_color")
                .Select(c => new
                {
                    c.Name,
                    c.Value,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.DeletedAt
                })
                .FirstOrDefault();

            if (mainColor == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Main color not found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Main color retrieved successfully.",
                data = mainColor
            });
        }

        [HttpGet("get-homescreen-banner")]
        public IActionResult GetHomescreenBanner()
        {
            var homescreenBanner = _context.Configs
                .Where(c => c.ConfigKey == "homescreen_banner")
                .Select(c => new
                {
                    c.Name,
                    c.Value,
                    c.CreatedAt,
                    c.UpdatedAt,
                    c.DeletedAt
                })
                .FirstOrDefault();

            if (homescreenBanner == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Homescreen banner not found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Homescreen banner retrieved successfully.",
                data = homescreenBanner
            });
        }

        [HttpGet("get-commission")]
        public IActionResult GetCommission()
        {
            var commissionConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "default_comission");

            if (commissionConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Commission configuration not found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Commission configuration retrieved successfully.",
                data = new
                {
                    commissionConfig.ConfigKey,
                    commissionConfig.Value,
                    commissionConfig.CreatedAt,
                    commissionConfig.UpdatedAt,
                    commissionConfig.DeletedAt
                }
            });
        }

        [HttpGet("get-royalty")]
        public IActionResult GetRoyalty()
        {
            var royaltyConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "default_royalty");

            if (royaltyConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Royalty configuration not found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Royalty configuration retrieved successfully.",
                data = new
                {
                    royaltyConfig.ConfigKey,
                    royaltyConfig.Value,
                    royaltyConfig.CreatedAt,
                    royaltyConfig.UpdatedAt,
                    royaltyConfig.DeletedAt
                }
            });
        }

        [HttpGet("get-refund-3-day")]
        public IActionResult GetRefund3Day()
        {
            var refundConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "refund_3_day");

            if (refundConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Refund 3-day configuration not found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Refund 3-day configuration retrieved successfully.",
                data = new
                {
                    refundConfig.ConfigKey,
                    refundConfig.Value,
                    refundConfig.CreatedAt,
                    refundConfig.UpdatedAt,
                    refundConfig.DeletedAt
                }
            });
        }

        [HttpGet("get-refund-1-day")]
        public IActionResult GetRefund1Day()
        {
            var refundConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "refund_1_day");

            if (refundConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Refund 1-day configuration not found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Refund 1-day configuration retrieved successfully.",
                data = new
                {
                    refundConfig.ConfigKey,
                    refundConfig.Value,
                    refundConfig.CreatedAt,
                    refundConfig.UpdatedAt,
                    refundConfig.DeletedAt
                }
            });
        }

        [HttpGet("get-refund-overdue")]
        public IActionResult GetRefundOverdue()
        {
            var refundConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "refund_overdue");

            if (refundConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Refund overdue configuration not found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Refund overdue configuration retrieved successfully.",
                data = new
                {
                    refundConfig.ConfigKey,
                    refundConfig.Value,
                    refundConfig.CreatedAt,
                    refundConfig.UpdatedAt,
                    refundConfig.DeletedAt
                }
            });
        }

        [HttpPut("edit-airport-price")]
        public IActionResult EditAirportPrice([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Airport price value must be provided."
                });
            }

            var airportPriceConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "airport_price");

            if (airportPriceConfig == null)
            {
                return Ok(new
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
                return Ok(new
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
                return Ok(new
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
                return Ok(new
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
                return Ok(new
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
        [HttpPut("edit-main-color")]
        public IActionResult EditMainColor([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Main color value must be provided."
                });
            }

            var mainColorConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "main_color");

            if (mainColorConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Main color configuration not found."
                });
            }

            // Cập nhật main color
            mainColorConfig.Value = configDto.Value;
            mainColorConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Main color updated successfully.",
                    data = new { mainColor = mainColorConfig.Value }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating the main color: {ex.Message}"
                });
            }
        }
        [HttpPut("edit-homescreen-banner")]
        public IActionResult EditHomescreenBanner([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Homescreen banner URL or file must be provided."
                });
            }

            var homescreenBannerConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "homescreen_banner");

            if (homescreenBannerConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Homescreen banner configuration not found."
                });
            }

            // Cập nhật homescreen banner
            homescreenBannerConfig.Value = configDto.Value;
            homescreenBannerConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Homescreen banner updated successfully.",
                    data = new { homescreenBanner = homescreenBannerConfig.Value }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating the homescreen banner: {ex.Message}"
                });
            }
        }

        [HttpPut("edit-commission")]
        public IActionResult EditCommission([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Commission value must be provided."
                });
            }

            var commissionConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "default_comission");

            if (commissionConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Commission configuration not found."
                });
            }

            commissionConfig.Value = configDto.Value;
            commissionConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Commission updated successfully.",
                    data = new
                    {
                        commission = commissionConfig.Value
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating commission: {ex.Message}"
                });
            }
        }

        [HttpPut("edit-royalty")]
        public IActionResult EditRoyalty([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Royalty value must be provided."
                });
            }

            var royaltyConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "default_royalty");

            if (royaltyConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Royalty configuration not found."
                });
            }

            royaltyConfig.Value = configDto.Value;
            royaltyConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Royalty updated successfully.",
                    data = new
                    {
                        royalty = royaltyConfig.Value
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating royalty: {ex.Message}"
                });
            }
        }
        [HttpPut("edit-refund-3-day")]
        public IActionResult EditRefund3Day([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Refund 3-day value must be provided."
                });
            }

            var refund3DayConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "refund_3_day");

            if (refund3DayConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Refund 3-day configuration not found."
                });
            }

            // Cập nhật refund 3-day
            refund3DayConfig.Value = configDto.Value;
            refund3DayConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Refund 3-day configuration updated successfully.",
                    data = new { refund3Day = refund3DayConfig.Value }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating the refund 3-day configuration: {ex.Message}"
                });
            }
        }

        [HttpPut("edit-refund-1-day")]
        public IActionResult EditRefund1Day([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Refund 1-day value must be provided."
                });
            }

            var refund1DayConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "refund_1_day");

            if (refund1DayConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Refund 1-day configuration not found."
                });
            }

            // Cập nhật refund 1-day
            refund1DayConfig.Value = configDto.Value;
            refund1DayConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Refund 1-day configuration updated successfully.",
                    data = new { refund1Day = refund1DayConfig.Value }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating the refund 1-day configuration: {ex.Message}"
                });
            }
        }

        [HttpPut("edit-refund-overdue")]
        public IActionResult EditRefundOverdue([FromBody] ConfigDto configDto)
        {
            if (string.IsNullOrEmpty(configDto.Value))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Refund overdue value must be provided."
                });
            }

            var refundOverdueConfig = _context.Configs
                .FirstOrDefault(c => c.ConfigKey == "refund_overdue");

            if (refundOverdueConfig == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Refund overdue configuration not found."
                });
            }

            // Cập nhật refund overdue
            refundOverdueConfig.Value = configDto.Value;
            refundOverdueConfig.UpdatedAt = DateTime.UtcNow;

            try
            {
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Refund overdue configuration updated successfully.",
                    data = new { refundOverdue = refundOverdueConfig.Value }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating the refund overdue configuration: {ex.Message}"
                });
            }
        }

    }
}
