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
using Microsoft.CodeAnalysis.Scripting;

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
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Invalid password."
                });
            }

            // Verify hashed password
            var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(admin, admin.Password, loginDto.Password);
            if (passwordVerificationResult == PasswordVerificationResult.Failed)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Invalid password."
                });
            }          
            // Check if the account is locked (if `DeletedAt` indicates account status)
            if (admin.DeletedAt != null)
            {
                return Ok(new
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
                return Ok(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Invalid token. Admin ID is missing."
                });
            }
            if (!int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid admin ID."
                });
            }

            var admin = await _context.Admins.FindAsync(adminId);
            if (admin == null)
            {
                return Ok(new
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
                admin.UpdatedAt,
                admin.Role
            };

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Admin profile retrieved successfully.",
                data = profileData
            });
        }

        [Authorize]
        [HttpGet("list")]
        public IActionResult GetAllAdmins(string searchName = null, string role = null, string email = null, int page = 1, int pageSize = 10)
        {
            var query = _context.Admins.AsQueryable();

            if (!string.IsNullOrEmpty(searchName))
            {
                query = query.Where(a => a.Name.Contains(searchName));
            }

            if (!string.IsNullOrEmpty(role))
            {
                query = query.Where(a => a.Role == role);
            }

            if (!string.IsNullOrEmpty(email))
            {
                query = query.Where(a => a.Email.Contains(email)); 
            }
            var totalAdmins = query.Count(); 
            var admins = query
                         .Skip((page - 1) * pageSize) 
                         .Take(pageSize)
                         .OrderByDescending(b => b.CreatedAt) 
                         .ToList();
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
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Admin creation data is invalid."
                });
            }

            // Kiểm tra xem email đã tồn tại trong cơ sở dữ liệu chưa
            var existingAdmin = _context.Admins.FirstOrDefault(a => a.Email == adminCreateDto.Email);
            if (existingAdmin != null)
            {
                return Ok(new
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
                Password = hashedPassword,
                Role = AdminRole.Admin.ToString(), 
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
            // Lấy thông tin admin hiện tại từ token
            var adminIdClaim = User.Claims.FirstOrDefault(c => c.Type == "AdminId");
            if (adminIdClaim == null)
            {
                return Unauthorized(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Invalid token. Admin ID is missing."
                });
            }

            // Kiểm tra admin hiện tại có quyền superadmin hay không
            var currentAdmin = _context.Admins.FirstOrDefault(a => a.Id == int.Parse(adminIdClaim.Value));
            if (currentAdmin == null || currentAdmin.Role != AdminRole.SuperAdmin.ToString())
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Only superadmin can delete admins."
                });
            }

            // Tìm admin cần xóa
            var adminToDelete = _context.Admins.FirstOrDefault(a => a.Id == id);
            if (adminToDelete == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Admin not found."
                });
            }

            // Kiểm tra nếu admin cần xóa là superadmin thì không được phép xóa
            if (adminToDelete.Role == AdminRole.SuperAdmin.ToString())
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Superadmin cannot be deleted."
                });
            }

            // Cập nhật trường DeletedAt
            adminToDelete.DeletedAt = DateTime.UtcNow;
            _context.Admins.Update(adminToDelete);
            _context.SaveChanges();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Admin deleted successfully.",
                data = new
                {
                    adminToDelete.Id,
                    adminToDelete.Name,
                    adminToDelete.DeletedAt
                }
            });
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateAdminProfileRequest request)
        {
            // Lấy AdminId từ Claims
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
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid admin ID."
                });
            }

            var admin = await _context.Admins.FindAsync(adminId);
            if (admin == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Admin not found."
                });
            }

            admin.Name = request.Name ?? admin.Name;
            admin.Email = request.Email ?? admin.Email;

            _context.Admins.Update(admin);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Admin profile updated successfully."
            });
        }

        [Authorize]
        [HttpPut("profile/password")]
        public async Task<IActionResult> UpdatePassword([FromBody] UpdateAdminPasswordRequest request)
        {
            // Lấy AdminId từ Claims
            var adminIdClaim = User.Claims.FirstOrDefault(c => c.Type == "AdminId");
            if (adminIdClaim == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Unauthorized,
                    message = "Invalid token. Admin ID is missing."
                });
            }

            if (!int.TryParse(adminIdClaim.Value, out int adminId))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid admin ID."
                });
            }

            // Tìm admin theo adminId
            var admin = await _context.Admins.FindAsync(adminId);
            if (admin == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Admin not found."
                });
            }

            // Kiểm tra mật khẩu cũ nếu cần
            if (!string.IsNullOrEmpty(request.OldPassword))
            {
                // Kiểm tra mật khẩu cũ có đúng không
                var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(admin, admin.Password, request.OldPassword);

                if (passwordVerificationResult != PasswordVerificationResult.Success)
                {
                    return Ok(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        message = "Old password is incorrect."
                    });
                }
            }

            // Kiểm tra mật khẩu mới có hợp lệ hay không
            if (string.IsNullOrEmpty(request.NewPassword))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "New password is required."
                });
            }

            // Mã hóa mật khẩu mới
            var hashedNewPassword = _passwordHasher.HashPassword(admin, request.NewPassword);

            // Cập nhật mật khẩu
            admin.Password = hashedNewPassword;

            // Lưu thay đổi vào cơ sở dữ liệu
            _context.Admins.Update(admin);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Admin password updated successfully."
            });
        }

    }
}
