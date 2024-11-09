using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using taxi_api.DTO;
using taxi_api.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly TaxiContext _context;
        private readonly IPasswordHasher<Admin> _passwordHasher;
        private readonly IConfiguration configuation;
        private readonly IMemoryCache _cache;

        public AdminController(TaxiContext context, IPasswordHasher<Admin> passwordHasher, IConfiguration configuation, IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            this.configuation = configuation;
            _cache = cache;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] AdminLoginDto loginDto)
        {
            if (loginDto == null)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid login data."
                });
            }

            // Find admin by email (or you may use a different unique identifier)
            var admin = _context.Admins.FirstOrDefault(a => a.Email == loginDto.Email);
            if (admin == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Admin not found."
                });
            }

            // Verify hashed password
            var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(admin, admin.Password, loginDto.Password);
            if (passwordVerificationResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Invalid password."
                });
            }          
            // Check if the account is locked (if `DeletedAt` indicates account status)
            if (admin.DeletedAt != null)
            {
                return Unauthorized(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Your account is locked. Please contact support."
                });
            }

            // Define response data for the admin
            var responseData = new
            {
                admin.Id,
                admin.Email,
                admin.CreatedAt,
                admin.UpdatedAt
            };

            var responseDataJson = JsonSerializer.Serialize(responseData);

            // Create claims with additional response data
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("AdminId", admin.Id.ToString()),
                new Claim("Email", admin.Email ?? ""),
                new Claim("ResponseData", responseDataJson)
            };

            // Generate JWT token
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuation["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: configuation["Jwt:Issuer"],
                audience: configuation["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(30),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Admin logged in successfully.",
                data = new
                {
                    token = tokenString
                },
            });
        }
       
        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var adminIdClaim = User.Claims.FirstOrDefault(c => c.Type == "AdminId");
            if (adminIdClaim == null)
            {
                return Unauthorized(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Invalid token. Admin ID is missing."
                });
            }
            if (!int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid admin ID."
                });
            }

            var admin = await _context.Admins.FindAsync(adminId);
            if (admin == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Admin not found."
                });
            }

            var profileData = new
            {
                admin.Id,
                admin.Email,
                admin.Name,
                admin.CreatedAt,
                admin.UpdatedAt
            };

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Admin profile retrieved successfully.",
                data = profileData
            });
        }

    }
}
