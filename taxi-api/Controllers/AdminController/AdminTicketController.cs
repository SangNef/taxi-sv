using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/ticket")]
    [ApiController]
    public class AdminTicketController : ControllerBase
    {
        private readonly TaxiContext _context;

        public AdminTicketController(TaxiContext context)
        {
            _context = context;
        }

        [HttpGet()]
        public async Task<IActionResult> Index([FromQuery] string code = null, [FromQuery] string nameOrPhone = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var ticketsQuery = _context.Tickets
                .Include(t => t.Booking)
                .ThenInclude(b => b.Arival)
                .Include(t => t.Booking.Customer)
                .AsQueryable();

            // Tìm kiếm theo BookingCode
            if (!string.IsNullOrEmpty(code))
            {
                ticketsQuery = ticketsQuery.Where(t => t.Booking.Code.Contains(code));
            }

            // Tìm kiếm theo NameOrPhone
            if (!string.IsNullOrEmpty(nameOrPhone))
            {
                ticketsQuery = ticketsQuery.Where(t =>
                    t.Booking.Customer.Name.Contains(nameOrPhone) ||
                    t.Booking.Customer.Phone.Contains(nameOrPhone));
            }

            // Tính tổng số bản ghi
            var totalRecords = await ticketsQuery.CountAsync();

            // Phân trang: Skip và Take
            var tickets = await ticketsQuery
                .Skip((page - 1) * pageSize)  // Bỏ qua các bản ghi của các trang trước
                .Take(pageSize)              // Lấy các bản ghi trong trang hiện tại
                .ToListAsync();

            // Lấy thông tin liên quan đến địa chỉ (Wards, Districts, Provinces)
            var pickUpWardIds = tickets.Select(t => t.Booking?.Arival?.PickUpId).Where(id => id.HasValue).Distinct().ToList();
            var dropOffWardIds = tickets.Select(t => t.Booking?.Arival?.DropOffId).Where(id => id.HasValue).Distinct().ToList();

            var wards = await _context.Wards
                .Where(w => pickUpWardIds.Contains(w.Id) || dropOffWardIds.Contains(w.Id))
                .ToListAsync();

            var districtIds = wards.Select(w => w.DistrictId).Distinct().ToList();
            var districts = await _context.Districts
                .Where(d => districtIds.Contains(d.Id))
                .ToListAsync();

            var provinceIds = districts.Select(d => d.ProvinceId).Distinct().ToList();
            var provinces = await _context.Provinces
                .Where(p => provinceIds.Contains(p.Id))
                .ToListAsync();

            // Lấy danh sách vé đã phân trang
            var ticketList = tickets.Select(t => new
            {
                t.Id,
                BookingCode = t.Booking?.Code,
                Name = t.Booking?.Customer?.Name,
                Phone = t.Booking?.Customer?.Phone,
                t.Booking?.StartAt,
                t.Booking?.EndAt,
                t.Booking?.Status,
                t.Booking?.Price,
                t.Booking?.Count,
                Content = t.Content,
                ArivalDetails = new
                {
                    PickUpAddress = t.Booking?.Arival?.PickUpAddress,
                    DropOffAddress = t.Booking?.Arival?.DropOffAddress,
                    Type = t.Booking?.Arival?.Type,
                    PickUpWard = wards.FirstOrDefault(w => w.Id == t.Booking?.Arival?.PickUpId)?.Name,
                    PickUpDistrict = districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == t.Booking?.Arival?.PickUpId)?.DistrictId)?.Name,
                    PickUpProvince = provinces.FirstOrDefault(p => p.Id == districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == t.Booking?.Arival?.PickUpId)?.DistrictId)?.ProvinceId)?.Name,
                    DropOffWard = wards.FirstOrDefault(w => w.Id == t.Booking?.Arival?.DropOffId)?.Name,
                    DropOffDistrict = districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == t.Booking?.Arival?.DropOffId)?.DistrictId)?.Name,
                    DropOffProvince = provinces.FirstOrDefault(p => p.Id == districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == t.Booking?.Arival?.DropOffId)?.DistrictId)?.ProvinceId)?.Name
                }
            }).ToList();

            // Tính tổng số trang
            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = ticketList,
                message = "Successfully retrieved the list of tickets.",
                totalRecords,
                currentPage = page,
                totalPages
            });
        }
    }
}
