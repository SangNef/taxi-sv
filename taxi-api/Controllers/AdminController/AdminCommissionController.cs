using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using taxi_api.Models;
using taxi_api.DTO;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/admin/commission")]
    [ApiController]
    public class AdminCommissionController : ControllerBase
    {
        private readonly TaxiContext _context;

        public AdminCommissionController(TaxiContext context)
        {
            _context = context;
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
    }
}
