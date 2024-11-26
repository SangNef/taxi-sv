using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Helpers
{
    public static class FindDriverHelper
    {
        public static async Task<Driver> FindDriver(int bookingId, int inviteId, TaxiContext context)
        {
            // Lấy thông tin booking
            var booking = await context.Bookings
                .Include(b => b.Arival)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null || booking.Arival == null)
            {
                return null; 
            }

            var bookingStartDate = booking.StartAt;
            var bookingCount = booking.Count;
            var dropOffId = booking.Arival.DropOffId;
            var type = booking.Arival.Type; 

            var drivers = await context.Drivers
                .Include(d => d.Taxies)
                .Where(d => d.DeletedAt == null && d.Taxies.Any(t => t.InUse == true) && d.Id != booking.InviteId)
                .ToListAsync();

            if (!drivers.Any())
            {
                return null;
            }

            var validDrivers = new List<Driver>();

            foreach (var driver in drivers)
            {
                var taxi = driver.Taxies.FirstOrDefault();
                if (taxi.Seat < bookingCount)
                {
                    return null;
                }
                if (taxi != null)
                {
                    var totalBookings = await context.BookingDetails
                        .Include(bd => bd.Booking)
                        .Where(bd => bd.TaxiId == taxi.Id &&
                                     bd.Booking.StartAt == bookingStartDate &&
                                     bd.Booking.Arival != null &&
                                     bd.Booking.Arival.DropOffId == dropOffId &&
                                     bd.Booking.Arival.Type == type) 
                        .SumAsync(bd => bd.Booking.Count);

                    if (totalBookings + bookingCount <= taxi.Seat)
                    {
                        validDrivers.Add(driver);
                    }

                }
                if (taxi != null)
                {
                    var completedBookingCount = await context.BookingDetails
                        .Include(bd => bd.Booking)
                        .Where(bd => bd.TaxiId == taxi.Id &&
                                     bd.Status == "4")
                        .CountAsync();

                    if (completedBookingCount > 0)
                    {
                        validDrivers.Add(driver);
                    }
                }
            }

            if (!validDrivers.Any())
            {
                validDrivers = drivers.Where(d => !context.BookingDetails
                    .Any(bd => bd.TaxiId == d.Taxies.FirstOrDefault().Id &&
                               bd.Booking.StartAt == bookingStartDate)).ToList();
            }

            if (!validDrivers.Any())
            {
                return null;
            }

            var selectedDriver = validDrivers[new Random().Next(validDrivers.Count)];
            var selectedTaxi = selectedDriver.Taxies.FirstOrDefault();

            if (selectedTaxi != null)
            {
                var bookingDetail = new BookingDetail
                {
                    BookingId = bookingId,
                    TaxiId = selectedTaxi.Id,
                    Status = "1", 
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Commission = selectedDriver.Commission 
                };

                await context.BookingDetails.AddAsync(bookingDetail);
                await context.SaveChangesAsync();

                var notification = new Notification
                {
                    DriverId = selectedDriver.Id,
                    Title = "Have a new trip",
                    Content = $"A new trip has been assigned to you. Start time: {booking.StartAt}. Please check and confirm.",
                    Navigate = $"{booking.Id}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                await context.Notifications.AddAsync(notification);
                await context.SaveChangesAsync();

                return selectedDriver;
            }

            return null;
        }
    }
}