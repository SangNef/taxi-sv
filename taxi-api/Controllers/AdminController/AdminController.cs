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

        [HttpGet("list")]
        public IActionResult GetAllAdmins(string searchName = null, string role = null, int page = 1, int pageSize = 10)
        {
            // Khởi tạo truy vấn cơ bản để tìm tất cả admin
            var query = _context.Admins.AsQueryable();

            // Tìm kiếm theo tên nếu có
            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(a => a.Name.Contains(searchName));
            }

            // Tìm kiếm theo role nếu có
            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(a => a.Role == role);
            }

            var totalAdmins = query.Count(); // Tổng số admin thỏa mãn điều kiện
            var admins = query
                         .Skip((page - 1) * pageSize) // Bỏ qua các admin ở các trang trước
                         .Take(pageSize) // Lấy một số lượng admin theo kích thước trang
                         .ToList();

            // Trả về kết quả dưới dạng paginated response
            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Admins retrieved successfully.",
                data = admins,
                pagination = new
                {
                    totalAdmins,
                    currentPage = page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)totalAdmins / pageSize)
                }
            });
        }


        [Authorize]
        [HttpPost("create")]
        public IActionResult CreateAdmin([FromBody] AdminCreateDto adminCreateDto)
        {
            if (adminCreateDto == null)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Admin creation data is invalid."
                });
            }

            // Kiểm tra xem email đã tồn tại trong cơ sở dữ liệu chưa
            var existingAdmin = _context.Admins.FirstOrDefault(a => a.Email == adminCreateDto.Email);
            if (existingAdmin != null)
            {
                return Conflict(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Email is already in use."
                });
            }

            // Mã hóa mật khẩu
            var hashedPassword = _passwordHasher.HashPassword(null, adminCreateDto.Password);

            // Tạo mới một admin
            var admin = new Admin
            {
                Name = adminCreateDto.Name,
                Email = adminCreateDto.Email,
                Phone = adminCreateDto.Phone,
                Role = "Admin",
                Password = hashedPassword,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Lưu admin vào cơ sở dữ liệu
            _context.Admins.Add(admin);
            _context.SaveChanges();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Admin created successfully.",
                data = new
                {
                    admin.Id,
                    admin.Name,
                    admin.Email,
                    admin.CreatedAt,
                    admin.UpdatedAt
                }
            });
        }

        [Authorize]
        [HttpDelete("delete/{id}")]
        public IActionResult DeleteAdmin(int id)
        {
            // Lấy thông tin AdminId và Role từ token
            var roleClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
            if (roleClaim == null)
            {
                return Unauthorized(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Role information is missing in the token."
                });
            }

            // Kiểm tra xem role có phải là "super admin" không
            if (roleClaim.Value.ToLower() == "super admin")
            {
                return Unauthorized(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Super admin cannot be banned."
                });
            }

            var admin = _context.Admins.FirstOrDefault(a => a.Id == id);
            if (admin == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Admin not found."
                });
            }

            // Kiểm tra xem Admin có bị khóa chưa
            if (admin.DeletedAt == null)
            {
                // Nếu Admin chưa bị khóa, đánh dấu DeletedAt với thời gian hiện tại để khóa
                admin.DeletedAt = DateTime.UtcNow;
                _context.Admins.Update(admin);
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Admin banned successfully."
                });
            }
            else
            {
                // Nếu Admin đã bị khóa, bỏ đánh dấu DeletedAt để mở khóa
                admin.DeletedAt = null;
                _context.Admins.Update(admin);
                _context.SaveChanges();
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Admin unbanned successfully."
                });
            }
        }

        //delete tý làm lại

    }
}
