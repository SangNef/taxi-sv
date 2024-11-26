using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Controllers
{
    [Route("api/terms")]
    [ApiController]
    public class AdminTermsController : ControllerBase
    {
        private readonly TaxiContext _context;

        public AdminTermsController(TaxiContext context)
        {
            _context = context;
        }

        // Get all terms with pagination and search
        [HttpGet]
        public async Task<IActionResult> GetTerms(string? title = null, int page = 1, int pageSize = 10)
        {
            var query = _context.Terms.AsQueryable();

            if (!string.IsNullOrEmpty(title))
            {
                query = query.Where(t => t.Title.Contains(title));
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var terms = await query
                .Where(t => t.DeletedAt == null)
                .OrderByDescending(b => b.CreatedAt) 
                .OrderBy(t => t.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                code = "Success",
                message = "Terms retrieved successfully.",
                data = terms,
                pagination = new
                {
                    totalItems,
                    currentPage = page,
                    pageSize,
                    totalPages
                }
            });
        }

        // Get term by id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTerm(int id)
        {
            var term = await _context.Terms.FindAsync(id);

            if (term == null || term.DeletedAt != null)
            {
                return NotFound(new { code = "NotFound", message = "Term not found." });
            }

            return Ok(new { code = "Success", message = "Term retrieved successfully.", data = term });
        }

        // Create a new term
        [HttpPost]
        public async Task<IActionResult> CreateTerm([FromBody] Term term)
        {
            term.CreatedAt = DateTime.UtcNow;
            term.UpdatedAt = DateTime.UtcNow;

            _context.Terms.Add(term);
            await _context.SaveChangesAsync();

            return Ok(new { code = "Success", message = "Term created successfully.", data = term });
        }

        // Update an existing term
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTerm(int id, [FromBody] Term updatedTerm)
        {
            var term = await _context.Terms.FindAsync(id);

            if (term == null || term.DeletedAt != null)
            {
                return NotFound(new { code = "NotFound", message = "Term not found." });
            }

            term.Title = updatedTerm.Title;
            term.Slug = updatedTerm.Slug;
            term.Content = updatedTerm.Content;
            term.Type = updatedTerm.Type;
            term.UpdatedAt = DateTime.UtcNow;

            _context.Terms.Update(term);
            await _context.SaveChangesAsync();

            return Ok(new { code = "Success", message = "Term updated successfully.", data = term });
        }

        [HttpPut("soft-delete/{id}")]
        public async Task<IActionResult> DeleteTerm(int id)
        {
            var term = await _context.Terms.FindAsync(id);

            if (term == null || term.DeletedAt != null)
            {
                return NotFound(new { code = "NotFound", message = "Term not found or already deleted." });
            }

            term.DeletedAt = DateTime.UtcNow;

            _context.Terms.Update(term);
            await _context.SaveChangesAsync();

            return Ok(new { code = "Success", message = "Term soft deleted successfully." });
        }

    }
}
