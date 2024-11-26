using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;
using System.Linq;
using System.Threading.Tasks;
using taxi_api.DTO;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/admin/provinces")]
    [ApiController]
    public class AdminProvinceController : ControllerBase
    {
        private readonly TaxiContext _context;

        // Constructor to initialize context
        public AdminProvinceController(TaxiContext context)
        {
            _context = context;
        }

        [HttpGet("get-all-provinces")]
        public IActionResult GetAllProvinces([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string name = null, [FromQuery] decimal? price = null)
        {
            // Ensure page and pageSize are valid
            if (page <= 0 || pageSize <= 0)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Page number and page size must be greater than 0."
                });
            }

            // Get the queryable list of provinces
            var query = _context.Provinces.AsQueryable();

            // Apply search filter if name is provided
            if (!string.IsNullOrEmpty(name))
            {
                query = query.Where(p => p.Name.Contains(name));
            }

            // Apply search filter if price is provided
            if (price.HasValue)
            {
                query = query.Where(p => p.Price == price.Value);
            }

            // Calculate the total number of records based on the filtered query
            var totalProvinces = query.Count();

            // Get the data for the requested page
            var provinces = query
                .Skip((page - 1) * pageSize) 
                .Take(pageSize)  
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.DeletedAt
                })
                .ToList();

            // Calculate total pages based on total count and pageSize
            var totalPages = (int)Math.Ceiling((double)totalProvinces / pageSize);

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "The list of provinces and their prices has been successfully retrieved.",
                data = provinces,
                pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalRecords = totalProvinces,
                    totalPages = totalPages
                }
            });
        }

        [HttpPut("update-province-price/{id}")]
        public IActionResult UpdateProvincePrice(int id, [FromBody] ProvinceDto provinceDto)
        {
            // Validate the input
            if (provinceDto.Price == null || provinceDto.Price <= 0)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Price must be provided and greater than 0."
                });
            }

            // Find the province by id
            var province = _context.Provinces.FirstOrDefault(p => p.Id == id);
            if (province == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Province not found."
                });
            }

            // Update the price and updated timestamp
            province.Price = provinceDto.Price;
            province.UpdatedAt = DateTime.UtcNow;

            try
            {
                // Save changes to the database
                _context.SaveChanges();

                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    message = "Province price updated successfully.",
                    data = new
                    {
                        province.Id,
                        province.Name,
                        province.Price,
                        province.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = $"An error occurred while updating the province price: {ex.Message}"
                });
            }
        }

        [HttpGet("search-location")]
        public async Task<IActionResult> GetWardInfoByName([FromQuery] string wardName)
        {
            if (string.IsNullOrEmpty(wardName))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = (object)null,
                    message = "Ward null ."
                });
            }

            var wardInfo = await _context.Wards
                .Where(w => EF.Functions.Like(w.Name, $"%{wardName}%"))
                .Include(w => w.District)
                .ThenInclude(d => d.Province)
                .Select(w => new
                {
                    WardId = w.Id,
                    WardName = w.Name,
                    District = new
                    {
                        DistrictId = w.District.Id,
                        DistrictName = w.District.Name,
                    },
                    Province = new
                    {
                        ProvinceId = w.District.Province.Id,
                        ProvinceName = w.District.Province.Name,
                        ProvincePrice = w.District.Province.Price
                    }
                })
                .Take(30)
                .ToListAsync();

            if (!wardInfo.Any())
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = (object)null,
                    message = "No matching wards found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = wardInfo,
                message = "Success"
            });
        }
    }
}
