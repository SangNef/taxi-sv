using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using taxi_api.DTO;
using taxi_api.Models;

namespace taxi_api.Controllers.DriverController
{
    [Route("api/driver")]
    [ApiController]
    public class DriverBookingController : ControllerBase
    {
        private readonly TaxiContext _context;

        public DriverBookingController(TaxiContext context)
        {
            _context = context;
        }
        // GET: api/DriverTaxi/get-assigned-bookings
        // Trip from the system
        [Authorize]
        [HttpGet("booking-status-detail-1")]
        public async Task<IActionResult> BookingDetailStt1()
        {
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;

            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new
                {
                    message = "Unauthorized: Driver ID not found."
                });
            }

            // Lấy danh sách Taxi thuộc tài xế đang đăng nhập
            var taxies = await _context.Taxies
                .Where(t => t.DriverId == driverId && t.InUse == true)
                .ToListAsync();

            if (taxies == null || !taxies.Any())
            {
                return NotFound(new { message = "No taxis found for the current driver." });
            }

            // Lấy danh sách BookingDetail có TaxiId thuộc danh sách các Taxi của tài xế
            var assignedBookings = await _context.BookingDetails
                .Where(bd => taxies.Select(t => t.Id).Contains(bd.TaxiId.Value) && bd.Status == "1")
                .Include(bd => bd.Booking)
                .Include(bd => bd.Booking.Arival)
                .Include(bd => bd.Booking.Customer)
                .ToListAsync();

            if (assignedBookings == null || assignedBookings.Count == 0)
            {
                return NotFound(new { message = "No assigned bookings found." });
            }

            var bookingList = new List<object>();

            foreach (var bd in assignedBookings)
            {
                var booking = bd.Booking;

                // Lấy thông tin pickUpWard
                var pickUpWard = await _context.Wards
                    .Where(w => w.Id == booking.Arival.PickUpId)
                    .Include(w => w.District)
                    .ThenInclude(d => d.Province)
                    .Select(w => new
                    {
                        WardId = w.Id,
                        WardName = w.Name,
                        District = new
                        {
                            DistrictName = w.District.Name,
                        },
                        Province = new
                        {
                            ProvinceName = w.District.Province.Name,
                        }
                    })
                    .FirstOrDefaultAsync();

                // Lấy thông tin dropOffWard
                var dropOffWard = await _context.Wards
                    .Where(w => w.Id == booking.Arival.DropOffId)
                    .Include(w => w.District)
                    .ThenInclude(d => d.Province)
                    .Select(w => new
                    {
                        WardId = w.Id,
                        WardName = w.Name,
                        District = new
                        {
                            DistrictName = w.District.Name,
                        },
                        Province = new
                        {
                            ProvinceName = w.District.Province.Name,
                            ProvincePrice = w.District.Province.Price
                        }
                    })
                    .FirstOrDefaultAsync();

                bookingList.Add(new
                {
                    BookingId = booking.Id,
                    Code = booking.Code,
                    CustomerName = booking.Customer?.Name,
                    CustomerPhone = booking.Customer?.Phone,
                    StartAt = booking.StartAt,
                    EndAt = booking.EndAt,
                    Price = booking.Price,
                    Status = booking.Status,
                    PassengerCount = booking.Count,
                    HasFull = booking.HasFull,
                    InviteId = booking.InviteId,
                    ArivalDetails = new
                    {
                        booking.Arival.Type,
                        booking.Arival.Price,
                        PickUpId = booking.Arival.PickUpId,
                        PickUpDetails = pickUpWard,
                        DropOffId = booking.Arival.DropOffId,
                        DropOffDetails = dropOffWard
                    },
                    TaxiDetails = new
                    {
                        bd.Taxi.Id,
                        bd.Taxi.Name,
                        bd.Taxi.LicensePlate,
                        bd.Taxi.Seat
                    }
                });
            }
            return Ok(new { data = bookingList });
        }


        // POST: api/DriverTaxi/accept-booking
        [Authorize]
        [HttpPost("update-booking")]
        public async Task<IActionResult> UpdateBooking([FromBody] DriverBookingStoreDto request)
        {
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;

            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new { message = "Unauthorized: Driver ID not found." });
            }

            var booking = await _context.Bookings.FindAsync(request.BookingId);
            if (booking == null)
            {
                return NotFound(new { message = "Booking not found." });
            }

            if (booking.Status == "1")
            {
                // Bước 3: Lấy thông tin tài xế từ DriverId
                var driver = await _context.Drivers.FindAsync(driverId);
                if (driver == null)
                {
                    return NotFound(new { message = "Driver not found" });
                }

                var taxi = await _context.Taxies
                    .Where(t => t.DriverId == driver.Id && t.InUse == true)
                    .FirstOrDefaultAsync();

                if (taxi == null)
                {
                    return NotFound(new { message = "No available taxi in use for this driver." });
                }

                // Bước 4: Kiểm tra tổng số ghế đã đặt trong các chuyến đang xử lý
                var currentSeatCount = await _context.BookingDetails
                    .Where(bd => bd.TaxiId == taxi.Id && bd.Status == "2") // chỉ tính các chuyến đang xử lý
                    .SumAsync(bd => bd.Booking.Count);

                if (currentSeatCount + booking.Count > taxi.Seat) // Kiểm tra số ghế vượt mức
                {
                    return BadRequest(new { message = "The taxi has reached its seat limit for current bookings." });
                }

                // Bước 5: Kiểm tra và cập nhật trạng thái BookingDetail
                var existingBookingDetail = await _context.BookingDetails
                    .Where(bd => bd.BookingId == request.BookingId && bd.Status == "1") // Lấy BookingDetail có Status = 1
                    .FirstOrDefaultAsync();

                if (existingBookingDetail != null)
                {
                    // Cập nhật trạng thái BookingDetail
                    existingBookingDetail.Status = "2";
                    existingBookingDetail.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Nếu chưa có BookingDetail, tạo mới
                    var newBookingDetail = new BookingDetail
                    {
                        BookingId = request.BookingId,
                        TaxiId = taxi.Id,
                        Status = "2",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _context.BookingDetails.AddAsync(newBookingDetail);
                }

                // Cập nhật trạng thái booking
                booking.Status = "2"; // Đã xử lý
                booking.UpdatedAt = DateTime.UtcNow;

                // Bước 6: Cập nhật điểm tài xế
                driver.Point -= (int?)((booking.Price * 90) / 100000);

                // Bước 7: Cập nhật hoa hồng
                var commission = driver.Commission;
                if (commission == null)
                {
                    return NotFound(new { message = "Commission not found for this driver." });
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Booking and details updated successfully status is 2." });
            }
            else if (booking.Status == "2")
            {
                booking.Status = "3"; 
                booking.UpdatedAt = DateTime.UtcNow;

                var bookingDetails = await _context.BookingDetails
                    .Where(bd => bd.BookingId == request.BookingId)
                    .ToListAsync();

                foreach (var detail in bookingDetails)
                {
                    detail.Status = "2";
                    detail.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Booking status updated to 3." });
            }
            else if (booking.Status == "3")
            {
                booking.Status = "4";
                booking.UpdatedAt = DateTime.UtcNow;

                var bookingDetails = await _context.BookingDetails
                    .Where(bd => bd.BookingId == request.BookingId)
                    .ToListAsync();

                foreach (var detail in bookingDetails)
                {
                    detail.Status = "2";
                    detail.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Booking status updated to 4." });
            }
            else
            {
                return BadRequest(new { message = "Booking status is not valid for updating." });
            }
        }

        [Authorize]
        [HttpGet("list-booking-status-1")]
        public async Task<IActionResult> BookingStt1()
        {
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;

            // Kiểm tra tính hợp lệ của DriverId
            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new
                {
                    message = "Unauthorized: Driver ID not found."
                });
            }

            var bookings = await _context.Bookings
                .Where(b => b.Status == "1")
                .Include(b => b.Customer)
                .Include(b => b.Arival)
                .Include(b => b.BookingDetails)
                .ToListAsync();

            if (bookings == null || !bookings.Any())
            {
                return NotFound(new
                {
                    code = CommonErrorCodes.NotFound,
                    data = (object)null,
                    message = "No trips found."
                });
            }

            // Lấy tất cả thông tin taxi
            var taxies = await _context.Taxies.ToListAsync();

            var bookingList = await Task.WhenAll(bookings.Select(async b =>
            {
                using (var context = new TaxiContext())
                {
                    var pickUpWard = await context.Wards
                        .Where(w => w.Id == b.Arival.PickUpId)
                        .Include(w => w.District)
                        .ThenInclude(d => d.Province)
                        .Select(w => new
                        {
                            WardId = w.Id,
                            WardName = w.Name,
                            District = new
                            {
                                DistrictName = w.District.Name,
                            },
                            Province = new
                            {
                                ProvinceName = w.District.Province.Name,
                            }
                        })
                        .FirstOrDefaultAsync();

                    var dropOffWard = await context.Wards
                        .Where(w => w.Id == b.Arival.DropOffId)
                        .Include(w => w.District)
                        .ThenInclude(d => d.Province)
                        .Select(w => new
                        {
                            WardId = w.Id,
                            WardName = w.Name,
                            District = new
                            {
                                DistrictName = w.District.Name,
                            },
                            Province = new
                            {
                                ProvinceName = w.District.Province.Name,
                                ProvincePrice = w.District.Province.Price
                            }
                        })
                        .FirstOrDefaultAsync();
                    return new
                    {
                        BookingId = b.Id,
                        Code = b.Code,
                        CustomerName = b.Customer?.Name,
                        CustomerPhone = b.Customer?.Phone,
                        StartAt = b.StartAt,
                        EndAt = b.EndAt,
                        Price = b.Price,
                        Status = b.Status,
                        PassengerCount = b.Count,
                        HasFull = b.HasFull,
                        InviteId = b.InviteId,
                        ArivalDetails = new
                        {
                            b.Arival.Type,
                            b.Arival.Price,
                            PickUpId = b.Arival.PickUpId,
                            PickUpDetails = pickUpWard,
                            DropOffId = b.Arival.DropOffId,
                            DropOffDetails = dropOffWard
                        },
                    };
                }
            }));

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = bookingList,
                message = "Successfully the list of trips pending ."
            });
        }

        [Authorize]
        [HttpGet("get-status-2-3")]
        public async Task<IActionResult> TripAccepted()
        {
            // Lấy DriverId từ claims
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;

            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new
                {
                    message = "Unauthorized: Driver ID not found."
                });
            }

            // Lấy tất cả BookingDetail có status là 2 và thông tin liên quan
            var bookingDetails = await _context.BookingDetails
                .Where(bd => bd.Status == "2" && bd.Status == "3")
                .Include(bd => bd.Booking)
                .Include(bd => bd.Booking.Arival)
                .Include(bd => bd.Booking.Customer)
                .ToListAsync();

            if (bookingDetails == null || !bookingDetails.Any())
            {
                return NotFound(new { message = "No booking details found with status 2." });
            }

            var taxies = await _context.Taxies.ToListAsync();

            var bookingList = await Task.WhenAll(bookingDetails.Select(async bd =>
            {
                var booking = bd.Booking;

                //lọc id theo tên
                //string customerName = null;
                //if (booking.Customer != null)
                //{
                //    customerName = booking.Customer.Name;
                //}
                //else if (booking.CustomerId.HasValue)
                //{
                //    var customer = await _context.Customers
                //        .Where(c => c.Id == booking.CustomerId.Value)
                //        .FirstOrDefaultAsync();
                //    customerName = customer?.Name;
                //}

                // Lấy chi tiết phường, quận, tỉnh cho điểm đón
                var pickUpWard = await _context.Wards
                    .Where(w => w.Id == booking.Arival.PickUpId)
                    .Include(w => w.District)
                    .ThenInclude(d => d.Province)
                    .Select(w => new
                    {
                        WardName = w.Name,
                        District = new
                        {
                            DistrictName = w.District.Name,
                        },
                        Province = new
                        {
                            ProvinceName = w.District.Province.Name,
                        }
                    })
                    .FirstOrDefaultAsync();

                var dropOffWard = await _context.Wards
                    .Where(w => w.Id == booking.Arival.DropOffId)
                    .Include(w => w.District)
                    .ThenInclude(d => d.Province)
                    .Select(w => new
                    {
                        WardId = w.Id,
                        WardName = w.Name,
                        District = new
                        {
                            DistrictName = w.District.Name,
                        },
                        Province = new
                        {
                            ProvinceName = w.District.Province.Name,
                            ProvincePrice = w.District.Province.Price
                        }
                    })
                    .FirstOrDefaultAsync();

                return new
                {
                    Code = booking.Code,
                    CustomerName = booking.Customer?.Name,
                    CustomerPhone = booking.Customer?.Phone,
                    StartAt = booking.StartAt,
                    EndAt = booking.EndAt,
                    Price = booking.Price,
                    Status = booking.Status,
                    PassengerCount = booking.Count,
                    HasFull = booking.HasFull,
                    InviteId = booking.InviteId,
                    ArivalDetails = new
                    {
                        booking.Arival.Type,
                        booking.Arival.Price,
                        PickUpId = booking.Arival.PickUpId,
                        PickUpDetails = pickUpWard,
                        DropOffId = booking.Arival.DropOffId,
                        DropOffDetails = dropOffWard
                    },
                    TaxiDetails = taxies.FirstOrDefault(t => t.Id == bd.TaxiId) 
                };
            }));

            return Ok(new { data = bookingList });
        }

        [Authorize]
        [HttpPost("cancel-booking")]
        public async Task<IActionResult> CancelBooking([FromBody] DriverBookingStoreDto cancelBookingDto)
        {
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;

            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new
                {
                    message = "Unauthorized: Driver ID not found."
                });
            }

            var bookingId = cancelBookingDto.BookingId;

            // Lấy booking detail liên quan đến tài xế đang đăng nhập
            var bookingDetail = await _context.BookingDetails
                .Include(bd => bd.Booking)
                .FirstOrDefaultAsync(bd => bd.BookingId == bookingId && bd.Taxi.DriverId == driverId);

            if (bookingDetail == null)
            {
                return NotFound(new { message = "Booking not found or you are not authorized to cancel this booking." });
            }

            if (bookingDetail.Booking.Status == "2" && bookingDetail.Status == "2")
            {
                bookingDetail.Booking.Status = "1"; 
                bookingDetail.Status = "0";    

                _context.BookingDetails.Update(bookingDetail);
                _context.Bookings.Update(bookingDetail.Booking);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Booking has been successfully canceled and updated." });
            }

            if (bookingDetail.Booking.Status == "3" && bookingDetail.Status == "2")
            {
                return BadRequest(new { message = "Cannot cancel booking: Driver is already picking up the customer." });
            }
            if (bookingDetail.Booking.Status == "4" && bookingDetail.Status == "2")
            {
                return BadRequest(new { message = "Cannot cancel booking: Driver is already complete." });
            }

            return BadRequest(new { message = "Cannot cancel booking: Invalid booking or status conditions." });
        }

    }
}
