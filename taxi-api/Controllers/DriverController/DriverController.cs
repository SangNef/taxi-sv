﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using taxi_api.Models;
using taxi_api.Helpers;
using taxi_api.DTO;
using Microsoft.AspNetCore.Authorization;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using Twilio.Types;
using Twilio.Exceptions;


namespace taxi_api.Controllers.DriverController
{
    [Route("api/driver")]
    [ApiController]
    public class DriverController : ControllerBase
    {
        private readonly TaxiContext _context;
        private readonly IPasswordHasher<Driver> _passwordHasher;
        private readonly IConfiguration configuation;
        private readonly IMemoryCache _cache;

        public DriverController(TaxiContext context, IPasswordHasher<Driver> passwordHasher, IConfiguration configuation, IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            this.configuation = configuation;
            _cache = cache;
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] DriverRegisterDto driverDto)
        {
            if (driverDto == null)
            {
                return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Invalid Data." });
            }

            var existingDriver = _context.Drivers.FirstOrDefault(d => d.Phone == driverDto.Phone);
            if (existingDriver != null)
            {
                return Conflict(new { code = CommonErrorCodes.InvalidData, message = "The driver with this phone number already exists." });
            }

            // Lấy giá trị commission mặc định từ bảng Configs
            var commissionDefault = _context.Configs.FirstOrDefault(c => c.ConfigKey == "default_comission");
            if (commissionDefault == null)
            {
                return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Default commission configuration not found." });
            }

            // Chuyển đổi giá trị commission từ chuỗi sang kiểu số (int hoặc decimal)
            int commission = 0;
            if (!int.TryParse(commissionDefault.Value, out commission))  // Hoặc sử dụng decimal nếu commission là số thập phân
            {
                return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Invalid commission value in configuration." });
            }

            var newDriver = new Driver
            {
                Fullname = driverDto.Name,
                Phone = driverDto.Phone,
                Password = _passwordHasher.HashPassword(null, driverDto.Password),
                IsActive = false,
                DeletedAt = null,
                Price = 0,
                Commission = commission,  // Gán commission vào tài xế
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // Thêm tài xế vào cơ sở dữ liệu
            _context.Drivers.Add(newDriver);
            _context.SaveChanges();

            // Trả về thông báo thành công
            return Ok(new { code = CommonErrorCodes.Success, message = "Register Driver Successfully, please wait for customer support to activate the account!" });
        }


        [HttpPost("login")]
        public IActionResult Login([FromBody] DriverLoginDto loginDto)
        {
            if (loginDto == null)
                return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Invalid login data." });

            var driver = _context.Drivers
                .Include(d => d.Taxies)
                .FirstOrDefault(x => x.Phone == loginDto.Phone);

            if (driver == null)
                return Ok(new { code = CommonErrorCodes.NotFound, message = "Invalid phone or password ." });

            var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(driver, driver.Password, loginDto.Password);
            if (passwordVerificationResult == PasswordVerificationResult.Failed)
                return Ok(new { code = CommonErrorCodes.Unauthorized, message = "Invalid phone or password ." });

            // Kiểm tra trạng thái tài khoản
            if (driver.IsActive == false)
                return Ok(new { code = CommonErrorCodes.Unauthorized, message = "Driver account is not activated ." });

            if (driver.DeletedAt != null)
                return Ok(new { code = CommonErrorCodes.Unauthorized, message = "Your account is locked. Please contact customer support ." });

            // Định nghĩa responseData để trả về dữ liệu tài xế và token
            var responseData = new
            {
                driver = new
                {
                    driver.Id,
                    driver.Fullname,
                    driver.Phone,
                    driver.IsActive,
                    driver.Price,
                    driver.Commission,
                    driver.CreatedAt,
                    driver.UpdatedAt,
                    Taxies = driver.Taxies.Select(t => new
                    {
                        t.DriverId,
                        t.Name,
                        t.LicensePlate,
                        t.Seat,
                        t.InUse,
                        t.CreatedAt,
                        t.UpdatedAt
                    }).ToList()
                }
            };

            var responseDataJson = JsonSerializer.Serialize(responseData);

            var claims = new[]
            {
        new Claim(JwtRegisteredClaimNames.Sub, driver.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim("DriverId", driver.Id.ToString()),
        new Claim("Phone", driver.Phone ?? ""),
        new Claim("ResponseData", responseDataJson)
    };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuation["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: configuation["Jwt:Issuer"],
                audience: configuation["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(1),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            return Ok(new { code = CommonErrorCodes.Success, message = "Driver logged in successfully.", token = tokenString});
        }
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetDriverProfile()
        {
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId");
            if (driverIdClaim == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Invalid token. Driver ID is missing."
                });
            }
            if (!int.TryParse(driverIdClaim.Value, out int driverId))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid driver ID."
                });
            }

            var driverProfile = await _context.Drivers
                .Where(d => d.Id == driverId)
                .GroupJoin(
                    _context.Taxies.Where(t => t.InUse == true),
                    driver => driver.Id,
                    taxi => taxi.DriverId,
                    (driver, taxies) => new
                    {
                        driver.Id,
                        driver.Fullname,
                        driver.Phone,
                        driver.Price,
                        driver.Commission,
                        driver.CreatedAt,
                        driver.UpdatedAt,
                        TaxiInfo = taxies.Select(taxi => new
                        {
                            taxi.Name,
                            taxi.LicensePlate,
                            taxi.Seat,
                            taxi.InUse,
                            taxi.CreatedAt,
                            taxi.UpdatedAt
                        }).ToList(),
                        Message = taxies.Any() ? null : "There are currently no vehicles available"
                    }
                )
                .FirstOrDefaultAsync();

            if (driverProfile == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Driver not found."
                });
            }

            // Thống kê booking
            var completedBookingsCount = await _context.BookingDetails
                .Where(bd => bd.TaxiId != null && bd.Taxi.DriverId == driverId && bd.Status == "4")
                .CountAsync();

            var canceledBookingsCount = await _context.BookingDetails
                .Where(bd => bd.TaxiId != null && bd.Taxi.DriverId == driverId && bd.Status == "5")
                .CountAsync();

            var totalBookingsCount = await _context.BookingDetails
                .Where(bd => bd.TaxiId != null && bd.Taxi.DriverId == driverId)
                .CountAsync();

            var reviews = await _context.Reviews
                .Where(r => r.BookingDetail != null &&
                            r.BookingDetail.Taxi != null &&
                            r.BookingDetail.Taxi.DriverId == driverId)
                .ToListAsync();

            int reviewCount = reviews.Count;
            decimal averageRating = reviewCount > 0
                ? (decimal)reviews.Average(r => r.Rate ?? 0)
                : 0;

            var rateCounts = reviews
                .GroupBy(r => r.Rate)
                .Select(g => new
                {
                    Percentage = reviewCount > 0 ? (decimal)g.Count() / reviewCount * 100 : 0
                })
                .ToList();

            decimal totalPercentage = rateCounts.Sum(r => r.Percentage);

            // Trả về kết quả
            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Driver profile and booking statistics retrieved successfully.",
                data = new
                {
                    DriverInfo = driverProfile,
                    BookingStatistics = new
                    {
                        completedBookingsCount,
                        canceledBookingsCount,
                        totalBookingsCount,
                        reviewCount,
                        averageRating,
                        rateDistribution = totalPercentage
                    }
                }
            });
        }

        [Authorize]
        [HttpPut("edit-profile")]
        public async Task<IActionResult> EditProfile([FromBody] EditDriverProfileDto editProfileDto)
        {
            if (editProfileDto == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid profile data."
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
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Driver not found."
                });
            }

            driver.Fullname = editProfileDto.Fullname ?? driver.Fullname;
            driver.Phone = editProfileDto.Phone ?? driver.Phone;
            driver.UpdatedAt = DateTime.Now;

            _context.Drivers.Update(driver);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Profile updated successfully.",
                data = new
                {
                    driver.Id,
                    driver.Fullname,
                    driver.Phone,
                    driver.UpdatedAt
                }
            });
        }

        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            if (changePasswordDto == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid password data."
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
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Driver not found."
                });
            }

            var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(driver, driver.Password, changePasswordDto.OldPassword);
            if (passwordVerificationResult == PasswordVerificationResult.Failed)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Old password is incorrect."
                });
            }

            // Cập nhật mật khẩu mới
            driver.Password = _passwordHasher.HashPassword(driver, changePasswordDto.NewPassword);
            driver.UpdatedAt = DateTime.Now;

            _context.Drivers.Update(driver);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Password changed successfully."
            });
        }

    }
}
