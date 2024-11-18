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
                return BadRequest(new
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
                .Skip((page - 1) * pageSize)  // Skip the records of previous pages
                .Take(pageSize)  // Take the number of records defined by pageSize
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


    }
}
