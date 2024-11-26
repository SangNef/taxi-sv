using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/revenue")]
    [ApiController]
    public class AdminRevenueController : ControllerBase
    {
        private readonly TaxiContext _context;

        public AdminRevenueController(TaxiContext context)
        {
            _context = context;
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetRevenue(bool? type = null, int page = 1, int pageSize = 10)
        {
            var query = _context.Revenues.AsQueryable();

            if (type.HasValue)
            {
                query = query.Where(r => r.Type == type.Value);
            }

            query = query.Where(r => r.DeletedAt == null);

            var totalRevenues = await query.CountAsync();

            var revenues = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize) 
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Type,
                    r.Amount,
                    r.Note,
                    r.CreatedAt,
                    r.UpdatedAt
                })
                .ToListAsync();

            // Trả về dữ liệu kèm thông tin phân trang
            return Ok(new
            {
                code = "SUCCESS",
                message = "Revenues retrieved successfully.",
                data = revenues,
                pagination = new
                {
                    totalRevenues,
                    currentPage = page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)totalRevenues / pageSize)
                }
            });
        }
    }
}
