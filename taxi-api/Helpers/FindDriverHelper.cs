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
                .Include(b => b.Arival) // Bao gồm dữ liệu Arival để lấy Type và dropOffId
                .FirstOrDefaultAsync(b => b.Id == bookingId);

            if (booking == null || booking.Arival == null)
            {
                return null; // Không tìm thấy booking hoặc Arival
            }

            var bookingStartDate = booking.StartAt;
            var bookingCount = booking.Count;
            var dropOffId = booking.Arival.DropOffId; // Lấy dropOffId từ Arival
            var type = booking.Arival.Type; // Lấy Type từ Arival

            // Bước 1: Lấy danh sách tài xế có xe đang sử dụng và không trùng inviteId
            var drivers = await context.Drivers
                .Include(d => d.Taxies)
                .Where(d => d.DeletedAt == null && d.Taxies.Any(t => t.InUse == true) && d.Id != inviteId) 
                .ToListAsync();

            if (!drivers.Any())
            {
                return null; // Không tìm thấy tài xế phù hợp
            }

            var validDrivers = new List<Driver>();

            // Bước 2: Lọc tài xế dựa trên chuyến đi tương tự và số ghế
            foreach (var driver in drivers)
            {
                var taxi = driver.Taxies.FirstOrDefault();
                if (taxi != null)
                {
                    // Tính tổng số khách đã đặt với tài xế này trong ngày, cùng dropOffId và Type
                    var totalBookings = await context.BookingDetails
                        .Include(bd => bd.Booking)
                        .Where(bd => bd.TaxiId == taxi.Id &&
                                     bd.Booking.StartAt == bookingStartDate && 
                                     bd.Booking.Arival != null &&
                                     bd.Booking.Arival.DropOffId == dropOffId &&
                                     bd.Booking.Arival.Type == type) // Kiểm tra điều kiện type và dropOffId
                        .SumAsync(bd => bd.Booking.Count);

                    // Kiểm tra nếu tổng số khách <= số ghế của taxi
                    if (totalBookings + bookingCount <= taxi.Seat)
                    {
                        validDrivers.Add(driver);
                    }
                }
            }

            // Nếu không tìm thấy tài xế với điều kiện tương tự, chọn ngẫu nhiên tài xế không có chuyến đi trong ngày
            if (!validDrivers.Any())
            {
                validDrivers = drivers.Where(d => !context.BookingDetails
                    .Any(bd => bd.TaxiId == d.Taxies.FirstOrDefault().Id &&
                               bd.Booking.StartAt == bookingStartDate)).ToList();
            }

            if (!validDrivers.Any())
            {
                var bookingDetail = new BookingDetail
                {
                    BookingId = bookingId,
                    TaxiId = null,
                    Status = "0",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Commission = 0
                };

                await context.BookingDetails.AddAsync(bookingDetail);
                await context.SaveChangesAsync();

                return null;
            }

            // Chọn ngẫu nhiên một tài xế từ danh sách hợp lệ
            var selectedDriver = validDrivers[new Random().Next(validDrivers.Count)];
            var selectedTaxi = selectedDriver.Taxies.FirstOrDefault();

            if (selectedTaxi != null)
            {
                // Lưu thông tin vào BookingDetail với commission từ tài xế
                var bookingDetail = new BookingDetail
                {
                    BookingId = bookingId,
                    TaxiId = selectedTaxi.Id,
                    Status = "1", // Trạng thái mới
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Commission = selectedDriver.Commission // Thêm commission từ tài xế được chọn
                };

                await context.BookingDetails.AddAsync(bookingDetail);
                await context.SaveChangesAsync();

                return selectedDriver;
            }

            return null;
        }
    }
}
