using System;
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
                return null; // Không tìm thấy booking hoặc Arival
            }

            var bookingStartDate = booking.StartAt;
            var bookingCount = booking.Count;
            var dropOffId = booking.Arival.DropOffId;
            var type = booking.Arival.Type;

            // Lấy danh sách tài xế có xe đang sử dụng và không trùng inviteId
            var drivers = await context.Drivers
                .Include(d => d.Taxies)
                .Where(d => d.DeletedAt == null && d.Taxies.Any(t => t.InUse == true) && d.Id != inviteId)
                .ToListAsync();

            if (!drivers.Any())
            {
                return null; // Không có tài xế phù hợp
            }

            // Danh sách tài xế hợp lệ theo từng ưu tiên
            var priorityDrivers = new List<Driver>();

            // Ưu tiên 1: Tài xế đang có chuyến đi cùng đường và cùng ngày
            foreach (var driver in drivers)
            {
                var taxi = driver.Taxies.FirstOrDefault();
                if (taxi != null)
                {
                    var matchingBookingCount = await context.BookingDetails
                        .Include(bd => bd.Booking)
                        .Where(bd => bd.TaxiId == taxi.Id &&
                                     bd.Booking.StartAt == bookingStartDate &&
                                     bd.Booking.Arival != null &&
                                     bd.Booking.Arival.DropOffId == dropOffId &&
                                     bd.Booking.Arival.Type == type &&
                                     bd.Status != "3") // Không tính chuyến bị hủy
                        .SumAsync(bd => bd.Booking.Count);

                    if (matchingBookingCount + bookingCount <= taxi.Seat)
                    {
                        priorityDrivers.Add(driver);
                    }
                }
            }

            // Nếu không tìm thấy tài xế trong ưu tiên 1, chuyển sang ưu tiên 2
            if (!priorityDrivers.Any())
            {
                foreach (var driver in drivers)
                {
                    var taxi = driver.Taxies.FirstOrDefault();
                    if (taxi != null)
                    {
                        var completedBookingCount = await context.BookingDetails
                            .Include(bd => bd.Booking)
                            .Where(bd => bd.TaxiId == taxi.Id &&
                                         bd.Booking.Status == "4" &&
                                         bd.Status == "2")
                            .CountAsync();

                        if (completedBookingCount > 0)
                        {
                            priorityDrivers.Add(driver);
                        }
                    }
                }
            }

            // Nếu không tìm thấy tài xế trong ưu tiên 2, chuyển sang ưu tiên 3
            if (!priorityDrivers.Any())
            {
                priorityDrivers = drivers.Where(d => !context.BookingDetails
                    .Any(bd => bd.TaxiId == d.Taxies.FirstOrDefault().Id &&
                               bd.Booking.StartAt == bookingStartDate &&
                               bd.Status != "3")).ToList();
            }

            if (!priorityDrivers.Any())
            {
                return null; // Không có tài xế nào phù hợp
            }

            // Chọn ngẫu nhiên một tài xế từ danh sách ưu tiên
            var selectedDriver = priorityDrivers[new Random().Next(priorityDrivers.Count)];
            var selectedTaxi = selectedDriver.Taxies.FirstOrDefault();

            if (selectedTaxi != null)
            {
                // Lưu thông tin vào BookingDetail
                var bookingDetail = new BookingDetail
                {
                    BookingId = bookingId,
                    TaxiId = selectedTaxi.Id,
                    Status = "1", // Trạng thái mới
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Commission = selectedDriver.Commission // Thêm commission từ tài xế
                };

                await context.BookingDetails.AddAsync(bookingDetail);
                await context.SaveChangesAsync();

                return selectedDriver;
            }

            return null;
        }
    }
}
