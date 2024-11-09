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
        [HttpGet("booking-from-the-System")]
        public async Task<IActionResult> BookingFromTheSystem()
        {
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;

            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new
                {
                    message = "Unauthorized: Driver ID not found."
                });
            }

            var assignedBookings = await _context.BookingDetails
                .Where(bd => bd.TaxiId == driverId && bd.Status == "1")
                .Include(bd => bd.Booking)
                .Include(bd => bd.Booking.Arival) // Bao gồm thông tin Arival
                .Include(bd => bd.Booking.Customer) // Bao gồm thông tin Customer
                .ToListAsync();

            if (assignedBookings == null || assignedBookings.Count == 0)
            {
                return NotFound(new { message = "No assigned bookings found." });
            }

            // Lấy tất cả thông tin taxi
            var taxies = await _context.Taxies.ToListAsync();

            var bookingList = await Task.WhenAll(assignedBookings.Select(async bd =>
            {
                var booking = bd.Booking;

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
                    TaxiDetails = taxies.FirstOrDefault(t => t.Id == bd.TaxiId)
                };
            }));

            return Ok(new { data = bookingList });
        }

        [Authorize] 
        [HttpGet("pending-booking")]
        public async Task<IActionResult> PendingBooking()
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

                    // Lọc danh sách BookingDetails có status = 0
                    var pendingBookingDetails = b.BookingDetails
                        .Where(bd => bd.Status == "0")
                        .Select(bd => new
                        {
                            bd.BookingId,
                            bd.Status,
                            bd.TaxiId,
                            TaxiDetails = taxies.Where(t => t.Id == bd.TaxiId).Select(t => new
                            {
                                t.Id,
                                t.DriverId,
                                t.Name,
                                t.LicensePlate,
                                t.Seat,
                                t.InUse,
                                t.CreatedAt,
                                t.UpdatedAt,
                                t.DeletedAt
                            }).FirstOrDefault()
                        }).ToList();

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
                        DriverAssignments = pendingBookingDetails // Chỉ lấy những BookingDetail có status = 0
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
        // POST: api/DriverTaxi/accept-booking
        [Authorize]
        [HttpPost("accept-booking")]
        public async Task<IActionResult> AcceptBooking([FromBody] DriverBookingStoreDto request)
        {
            // Bước 1: Lấy DriverId từ claims
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;

            // Kiểm tra tính hợp lệ của DriverId
            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new
                {
                    message = "Unauthorized: Driver ID not found."
                });
            }

            // Bước 2: Tìm booking theo BookingId
            var booking = await _context.Bookings.FindAsync(request.BookingId);
            if (booking == null)
            {
                return NotFound(new { message = "Booking not found" });
            }

            // Bước 3: Lấy thông tin tài xế từ DriverId
            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver == null)
            {
                return NotFound(new { message = "Driver not found" });
            }

            var taxi = await _context.Taxies
                 .Where(t => t.DriverId == driver.Id && (t.InUse == true))
                 .FirstOrDefaultAsync();


            if (taxi == null)
            {
                return NotFound(new { message = "No available taxi in use for this driver" });
            }
            // Bước 5: Kiểm tra tổng số ghế đã đặt trong các chuyến đang xử lý
            var currentSeatCount = await _context.BookingDetails
                .Where(bd => bd.TaxiId == taxi.Id && bd.Status == "2") // chỉ tính các chuyến đang xử lý
                .SumAsync(bd => bd.Booking.Count); // giả sử Booking có trường SeatCount để lưu số ghế đã đặt

            // Kiểm tra nếu tổng số ghế đã đặt cộng thêm chuyến mới sẽ vượt quá số ghế
            if (currentSeatCount + booking.Count > taxi.Seat)
            {
                return BadRequest(new { message = "The taxi has reached its seat limit for current bookings." });
            }

            // Bước 5: Tạo mới BookingDetail
            var bookingDetail = new BookingDetail
            {
                BookingId = request.BookingId,
                TaxiId = taxi.Id,
                Status = "2",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Bước 6: Cập nhật điểm của tài xế
            driver.Point -= (int?)((booking.Price * 90) / 100000);
            await _context.SaveChangesAsync();

            // Bước 7: Kiểm tra và gán hoa hồng
            var commission = driver.Commission;
            if (commission == null)
            {
                return NotFound(new { message = "Commission not found for this driver" });
            }

            // Bước 8: Tính toán tổng giá tiền
            bookingDetail.Commission = commission;
            //bookingDetail.TotalPrice = booking.Price - (commission * booking.Price / 100);

            // Bước 9: Thêm BookingDetail vào cơ sở dữ liệu
            await _context.BookingDetails.AddAsync(bookingDetail);

            // Cập nhật trạng thái booking
            booking.Status = "2"; // Đã xử lý
            await _context.SaveChangesAsync();

            // Bước 10: Trả về kết quả thành công
            return Ok(new { message = "Success" });
        }

        [Authorize]
        [HttpGet("trip-accepted")]
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
                .Where(bd => bd.Status == "2")
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
        public async Task<IActionResult> CancelBooking([FromBody] int bookingDetailId)
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

            // Tìm kiếm booking detail mà tài xế được chỉ định
            var bookingDetail = await _context.BookingDetails
                .Where(bd => bd.Id == bookingDetailId && bd.TaxiId == driverId && bd.Status == "1")
                .FirstOrDefaultAsync();

            if (bookingDetail == null)
            {
                return NotFound(new { message = "Booking detail not found or you are not authorized to cancel this booking." });
            }

            if(bookingDetail.Status == "2")
            {
                return BadRequest(new {message = "The driver has accepted this trip and cannot cancel it !" });
            }
            bookingDetail.Status = "0"; 
            _context.BookingDetails.Update(bookingDetail);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Booking has been canceled successfully." });
        }

    }
}
