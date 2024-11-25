using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/admin/pages")]
    [ApiController]
    public class AdminPageController : ControllerBase
    {
        private readonly TaxiContext _context;

        public AdminPageController(TaxiContext context)
        {
            _context = context;
        }

        // 1. Get all pages with their contents
        [HttpGet]
        public async Task<IActionResult> GetAllPages()
        {
            var pages = await _context.Pages
                .Include(p => p.PageContents)
                .ToListAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Pages retrieved successfully.",
                data = pages
            });
        }

        // 2. Get a single page by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPageById(int id)
        {
            var page = await _context.Pages
                .Include(p => p.PageContents)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (page == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Page not found."
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Page retrieved successfully.",
                data = page
            });
        }

        // 3. Create a new page
        [HttpPost]
        public async Task<IActionResult> CreatePage([FromBody] Page page)
        {
            if (page == null)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Invalid page data."
                });
            }

            page.CreatedAt = DateTime.UtcNow;

            _context.Pages.Add(page);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Page created successfully.",
                data = page
            });
        }

        // 4. Update an existing page
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePage(int id, [FromBody] Page page)
        {
            var existingPage = await _context.Pages
                .Include(p => p.PageContents)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (existingPage == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Page not found."
                });
            }

            existingPage.Title = page.Title;
            existingPage.Slug = page.Slug;
            existingPage.UpdatedAt = DateTime.UtcNow;

            // Update page contents
            if (page.PageContents != null && page.PageContents.Any())
            {
                existingPage.PageContents = page.PageContents;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Page updated successfully.",
                data = existingPage
            });
        }

        // 5. Delete a page and its contents
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePage(int id)
        {
            var page = await _context.Pages
                .Include(p => p.PageContents)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (page == null)
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    message = "Page not found."
                });
            }

            // Soft delete the page and its contents
            page.DeletedAt = DateTime.UtcNow;

            foreach (var content in page.PageContents)
            {
                content.DeletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                message = "Page deleted successfully."
            });
        }
    }
}
