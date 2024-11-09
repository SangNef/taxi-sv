using Microsoft.AspNetCore.Http;
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

            var newDriver = new Driver
            {
                Fullname = driverDto.Name,
                Phone = driverDto.Phone,
                Password = _passwordHasher.HashPassword(null, driverDto.Password),
                IsActive = true,
                DeletedAt = null,
                Point = 0,
                Commission = 0,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _context.Drivers.Add(newDriver);
            _context.SaveChanges();

            return Ok(new { code = CommonErrorCodes.Success, message = "Register Driver Successfully , please waiting for custommer support active account for moment !" });
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] DriverLoginDto loginDto)
        {
            if (loginDto == null)
                return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Invalid login data." });

            // Tìm tài xế theo số điện thoại
            var driver = _context.Drivers
                .Include(d => d.Taxies) // Bao gồm dữ liệu Taxies khi truy vấn tài xế
                .FirstOrDefault(x => x.Phone == loginDto.Phone);

            if (driver == null)
                return NotFound(new { code = CommonErrorCodes.NotFound, message = "Driver does not exist." });

            // Kiểm tra mật khẩu đã được băm
            var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(driver, driver.Password, loginDto.Password);
            if (passwordVerificationResult == PasswordVerificationResult.Failed)
                return Unauthorized(new { code = CommonErrorCodes.Unauthorized, message = "Invalid account or password" });

            // Kiểm tra trạng thái tài khoản
            if (driver.IsActive == false)
                return Unauthorized(new { code = CommonErrorCodes.Unauthorized, message = "Driver account is not activated." });

            if (driver.DeletedAt != null)
                return Unauthorized(new { code = CommonErrorCodes.Unauthorized, message = "Your account is locked. Please contact customer support." });

            // Định nghĩa responseData để trả về dữ liệu tài xế và token
            var responseData = new
            {
                driver = new
                {
                    driver.Id,
                    driver.Fullname,
                    driver.Phone,
                    driver.IsActive,
                    driver.Point,
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
            var phoneNumber = driver.Phone;
            if (phoneNumber.StartsWith("0"))
            {
                phoneNumber = "+84" + phoneNumber.Substring(1); 
            }
            else
            {
                phoneNumber = "+" + phoneNumber;
            }
            try
            {
                TwilioClient.Init(configuation["Twilio:AccountSid"], configuation["Twilio:AuthToken"]);

                var message = MessageResource.Create(
                    body: "Login successful. Welcome to the Taxi service hê loooooooooooooooooooooooooooooooooo.",
                    from: new PhoneNumber(configuation["Twilio:PhoneNumber"]),
                    to: new PhoneNumber(phoneNumber)  
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = "Failed to send SMS.",
                    error = ex.Message,  // Lấy chi tiết lỗi
                    stackTrace = ex.StackTrace  // In ra Stack Trace nếu cần
                });
            }


            return Ok(new { code = CommonErrorCodes.Success, message = "Driver logged in successfully.", token = tokenString , phone = phoneNumber });
        }
        [HttpPost("create-booking")]
        public async Task<IActionResult> CreateBooking([FromBody] BookingRequestDto request)
        {
            if (request == null)
                return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Invalid data." });

            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;
            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new { code = CommonErrorCodes.Unauthorized, message = "Invalid driver." });
            }

            // Validate the request
            if (request == null)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid data."
                });
            }

            Customer customer;

            if (!string.IsNullOrEmpty(request.Name) && !string.IsNullOrEmpty(request.Phone))
            {
                customer = new Customer
                {
                    Name = request.Name,
                    Phone = request.Phone
                };
                await _context.Customers.AddAsync(customer);
            }
            else
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Please select or create a new customer!"
                });
            }

            // Kiểm tra PickUpId và DropOffId, nếu không có thì lấy từ Config
            if (request.PickUpId == null)
            {
                var pickupConfig = await _context.Configs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "default_arival_pickup");
                if (pickupConfig != null)
                {
                    request.PickUpId = int.Parse(pickupConfig.Value);
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Pick-up point configuration not found!"
                    });
                }
            }

            if (request.DropOffId == null)
            {
                var dropoffConfig = await _context.Configs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "default_arival_dropoff");
                if (dropoffConfig != null)
                {
                    request.DropOffId = int.Parse(dropoffConfig.Value);
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Drop-off point configuration not found!"
                    });
                }
            }

            // Kiểm tra PickUpId và DropOffId có hợp lệ trong cơ sở dữ liệu
            if (!await _context.Wards.AnyAsync(w => w.Id == request.PickUpId))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid pick-up point!"
                });
            }

            if (!await _context.Wards.AnyAsync(w => w.Id == request.DropOffId))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid drop-off point!"
                });
            }

            // Tạo Arival và xử lý giá
            var arival = new Arival
            {
                Type = request.Types,
                PickUpId = request.PickUpId,
                PickUpAddress = request.PickUpAddress,
                DropOffId = request.DropOffId,
                DropOffAddress = request.DropOffAddress
            };

            decimal price = 0;

            if (request.Types == "province")
            {
                // Find the Ward based on DropOffId
                var ward = await _context.Wards
                .FirstOrDefaultAsync(w => w.Id == request.DropOffId);

                if (ward != null)
                {
                    // Retrieve District based on ward's district_id
                    var district = await _context.Districts
                    .FirstOrDefaultAsync(d => d.Id == ward.DistrictId);

                    if (district != null)
                    {
                        // Retrieve Province based on district's ProvinceId
                        var province = await _context.Provinces
                        .FirstOrDefaultAsync(p => p.Id == district.ProvinceId);

                        if (province != null)
                        {
                            // Assign the province price
                            price = province.Price.Value;
                        }
                        else
                        {
                            return BadRequest(new
                            {
                                code = CommonErrorCodes.InvalidData,
                                data = (object)null,
                                message = "Province not found for the selected district."
                            });
                        }
                    }
                    else
                    {
                        return BadRequest(new
                        {
                            code = CommonErrorCodes.InvalidData,
                            data = (object)null,
                            message = "District not found for the selected ward."
                        });
                    }
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Ward not found for the selected drop-off point."
                    });
                }
            }

            else if (request.Types == "airport")
            {
                arival.DropOffId = null;
                arival.DropOffAddress = null;

                var airportConfig = await _context.Configs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "airport_price");

                if (airportConfig != null)
                {
                    price = decimal.Parse(airportConfig.Value);
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Airport price config not found."
                    });
                }
            }
            else
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid type for Arival."
                });
            }

            arival.Price = price;


            // Lưu Arival
            await _context.Arivals.AddAsync(arival);
            await _context.SaveChangesAsync();

            // Tạo mới Booking
            var booking = new Booking
            {
                Code = "XG" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CustomerId = customer.Id,
                ArivalId = arival.Id,
                StartAt = DateOnly.FromDateTime(DateTime.UtcNow),
                EndAt = null,
                Count = request.Count,
                Price = arival.Price,
                HasFull = request.HasFull,
                Status = "1",
                InviteId = driverId
            };

            await _context.Bookings.AddAsync(booking);
            await _context.SaveChangesAsync();

            var taxi = await FindDriverHelper.FindDriver(booking.Id, 0, _context);

            if (taxi == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Wait for the driver to accept this trip!"
                });
            }
            var driverPhoneNumber = taxi.Phone;
            if (driverPhoneNumber.StartsWith("0"))
            {
                driverPhoneNumber = "+84" + driverPhoneNumber.Substring(1);
            }

            try
            {
                TwilioClient.Init(configuation["Twilio:AccountSid"], configuation["Twilio:AuthToken"]);

                var message = MessageResource.Create(
                    body: "Your booking code is: {booking.Code}.",
                    from: new PhoneNumber(configuation["Twilio:PhoneNumber"]),
                    to: new PhoneNumber(driverPhoneNumber)
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = "Failed to send SMS.",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = new { bookingId = booking.Id },
                message = "Trip created successfully and SMS sent to the driver!"
            });
        }
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetDriverProfile()
        {
            // Lấy claim DriverId từ token
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId");
            if (driverIdClaim == null)
            {
                return Unauthorized(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Invalid token. Driver ID is missing."
                });
            }
            if (!int.TryParse(driverIdClaim.Value, out int driverId))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid driver ID."
                });
            }

            // Tìm driver bằng DriverId
            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Driver not found."
                });
            }

            var profileData = new
            {
                driver.Id,
                driver.Fullname,
                driver.Phone,
                driver.CreatedAt,
                driver.UpdatedAt
            };

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Driver profile retrieved successfully.",
                data = profileData
            });
        }

    }
}
