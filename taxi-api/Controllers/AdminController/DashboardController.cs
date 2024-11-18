using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using taxi_api.Models;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly TaxiContext _context;

        public DashboardController(TaxiContext context)
        {
            _context = context;
        }

        [HttpGet("overview")]
        public async Task<IActionResult> GetDashboardOverview()
        {
            var today = DateOnly.FromDateTime(DateTime.Today); // Chuyển today's DateTime thành DateOnly
            var startOfMonth = new DateOnly(today.Year, today.Month, 1); // Ngày đầu tháng
            var startOfSixMonthsAgo = today.AddMonths(-6); // Ngày 6 tháng trước

            // 1. Số chuyến trong tháng
            var tripsThisMonth = await _context.Bookings
                .Where(b => b.StartAt.HasValue)
                .ToListAsync(); // Lấy dữ liệu về bộ nhớ

            var tripsThisMonthCount = tripsThisMonth
                .Where(b => b.StartAt.Value >= startOfMonth && b.StartAt.Value <= today)
                .Count();

            // 2. Tổng doanh thu trong tháng
            var totalRevenueThisMonth = tripsThisMonth
                .Where(b => b.StartAt.Value >= startOfMonth && b.StartAt.Value <= today)
                .Sum(b => b.Price);

            // 3. Tổng số tài xế
            var totalDrivers = await _context.Drivers.CountAsync();

            // 4. Tổng số khách hàng
            var totalEmployees = await _context.Customers.CountAsync();

            // Tổng số nhân viên
            var totalAdmin = await _context.Admins
                .Where(a => a.Role == "admin")
                .CountAsync();

            // 5. Doanh thu và số chuyến trong 6 tháng gần nhất
            var revenueLastSixMonths = await _context.Bookings
                .Where(b => b.StartAt.HasValue && b.StartAt.Value >= startOfSixMonthsAgo)
                .ToListAsync(); // Lấy dữ liệu về bộ nhớ

            var revenueAndTripsLastSixMonths = revenueLastSixMonths
                .GroupBy(b => new { b.StartAt.Value.Year, b.StartAt.Value.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalRevenue = g.Sum(b => b.Price),
                    TripCount = g.Count()
                })
                .OrderByDescending(g => g.Year)
                .ThenByDescending(g => g.Month)
                .ToList();

            // Trả về kết quả dưới dạng JSON
            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = new
                {
                    tripsThisMonth = tripsThisMonthCount,
                    totalRevenueThisMonths = totalRevenueThisMonth,
                    totalDriver = totalDrivers,
                    totalEmployee = totalEmployees,
                    revenueLastSixMonths = revenueAndTripsLastSixMonths
                },
                message = "Dashboard data retrieved successfully"
            });
        }
    }
}
