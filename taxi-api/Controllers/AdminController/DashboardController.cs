using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using taxi_api.Models;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/admin")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly TaxiContext _context;

        public DashboardController(TaxiContext context)
        {
            _context = context;
        }
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var today = DateOnly.FromDateTime(DateTime.Today); // Chuyển today's DateTime thành DateOnly
            var startOfMonth = new DateOnly(today.Year, today.Month, 1); // Ngày đầu tháng
            var startOfSixMonthsAgo = today.AddMonths(-5); // Ngày bắt đầu của 6 tháng gần nhất (bao gồm tháng hiện tại)

            // 1. Số chuyến trong tháng
            var tripsThisMonth = await _context.Bookings
                .Where(b => b.StartAt.HasValue)
                .ToListAsync(); 

            var tripsThisMonthCount = tripsThisMonth
                .Where(b => b.StartAt.Value >= startOfMonth && b.StartAt.Value <= today)
                .Count();

            var totalRevenueThisMonth = tripsThisMonth
                .Where(b => b.StartAt.Value >= startOfMonth && b.StartAt.Value <= today)
                .Sum(b => b.Price);

            var totalDrivers = await _context.Drivers.CountAsync();

            var totalCustomers = await _context.Customers.CountAsync();

            var totalReviews = await _context.Reviews.CountAsync();

            var revenueLastSixMonths = await _context.Bookings
                .Where(b => b.StartAt.HasValue && b.StartAt.Value >= startOfSixMonthsAgo)
                .ToListAsync();

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

            var monthsList = Enumerable.Range(0, 6)
                .Select(i => new
                {
                    Year = startOfSixMonthsAgo.AddMonths(i).Year,
                    Month = startOfSixMonthsAgo.AddMonths(i).Month,
                    TotalRevenue = revenueAndTripsLastSixMonths
                        .FirstOrDefault(x => x.Year == startOfSixMonthsAgo.AddMonths(i).Year && x.Month == startOfSixMonthsAgo.AddMonths(i).Month)?.TotalRevenue ?? 0,
                    TripCount = revenueAndTripsLastSixMonths
                        .FirstOrDefault(x => x.Year == startOfSixMonthsAgo.AddMonths(i).Year && x.Month == startOfSixMonthsAgo.AddMonths(i).Month)?.TripCount ?? 0
                })
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
                    totalCustomer = totalCustomers,
                    totalReviews = totalReviews,
                    revenueLastSixMonths = monthsList
                },
                message = "Dashboard data retrieved successfully"
            });
        }


    }
}
