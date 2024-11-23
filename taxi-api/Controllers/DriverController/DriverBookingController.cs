using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.DTO;
using taxi_api.Helpers;
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
            var bookingDetails = await _context.BookingDetails
                .Where(bd => bd.Taxi.DriverId == driverId && bd.Status == "1")
                .Include(bd => bd.Booking) 
                .Include(bd => bd.Booking.Arival)
                .Include(bd => bd.Booking.Customer)
                .ToListAsync();

            if (bookingDetails == null || bookingDetails.Count == 0)
            {
                return NotFound(new { message = "No assigned bookings found." });
            }

            var bookingList = new List<object>();

            foreach (var bd in bookingDetails)
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
                    Count = booking.Count,
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
        [Authorize]
        [HttpPost("update-booking")]
        public async Task<IActionResult> UpdateBooking([FromBody] DriverBookingStoreDto request)
        {
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;

            // Kiểm tra tính hợp lệ của DriverId
            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new { message = "Unauthorized: Driver ID not found." });
            }

            // Tìm Booking theo BookingId
            var booking = await _context.Bookings.FindAsync(request.BookingId);
            if (booking == null)
            {
                return NotFound(new { message = "Booking not found." });
            }

            // Tìm BookingDetails liên quan đến BookingId
            var bookingDetails = await _context.BookingDetails
                .Where(bd => bd.BookingId == request.BookingId)
                .ToListAsync();

            if (!bookingDetails.Any())
            {
                return NotFound(new { message = "BookingDetails not found." });
            }

            // Tìm taxi liên quan đến Booking
            var taxi = await _context.Taxies
                .FirstOrDefaultAsync(t => t.Id == bookingDetails.First().TaxiId);

            if (taxi == null)
            {
                return NotFound(new { message = "Taxi not found." });
            }

            // Tìm tài xế liên quan đến taxi
            var driver = await _context.Drivers.FindAsync(taxi.DriverId);
            if (driver == null)
            {
                return NotFound(new { message = "Driver not found." });
            }

            // Thay đổi trạng thái cho từng BookingDetail
            foreach (var bookingDetail in bookingDetails)
            {
                if (bookingDetail.Status == "2")
                {
                    bookingDetail.Status = "3";
                }
                else if (bookingDetail.Status == "3")
                {
                   
                    if (booking.InviteId != 0)
                    {
                        var driverInvite = await _context.Drivers.FindAsync(booking.InviteId);

                        if (driverInvite == null)
                        {
                            return NotFound(new { message = "Invited driver not found." });
                        }

                        decimal commissionRate = driver.Commission.GetValueOrDefault(0);
                        decimal bookingPrice = booking.Price.GetValueOrDefault(0);
                        decimal deduction = (bookingPrice * commissionRate) / 100;

                        var config = await _context.Configs.FirstOrDefaultAsync(c => c.ConfigKey == "default_royalty");
                        if (config == null || !decimal.TryParse(config.Value, out decimal defaultRoyalty))
                        {
                            return NotFound(new { message = "Config default_royalty not found or invalid." });
                        }

                        decimal result2 = deduction * defaultRoyalty / 100;
                        driverInvite.Price += result2;

                        bookingDetail.Status = "4";
                        booking.EndAt = DateTime.UtcNow;

                        var newWallet = new Wallet
                        {
                            DriverId = driverInvite.Id,
                            Type = "Successfully received paid Royalty.",
                            Price = result2,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _context.Wallets.AddAsync(newWallet);
                        await _context.SaveChangesAsync();

                        var newNotificationAdmin = new AdminNotification
                        {
                            IsRead = false,
                            Title = $"Driver {driver.Fullname} complete booking #{booking.Code}",
                            Content = $"Driver {driver.Fullname} has completed booking #{booking.Code} and deducted {result2} for the driver who created the trip",
                            Navigate = $"/booking/{booking.Code}"
                        };
                        await _context.AdminNotifications.AddAsync(newNotificationAdmin);
                    }
                    else
                    {
                        bookingDetail.Status = "4";
                        booking.EndAt = DateTime.UtcNow;

                        var newNotificationAdmin = new AdminNotification
                        {
                            IsRead = false,
                            Title = $"Driver {driver.Fullname} complete booking #{booking.Code}",
                            Content = $"Driver {driver.Fullname} has completed booking #{booking.Code}",
                            Navigate = $"/booking/{booking.Code}"
                        };
                        await _context.AdminNotifications.AddAsync(newNotificationAdmin);
                    }
                }
                else
                {
                    return BadRequest(new { message = "Booking is ready successfully" });
                }

                bookingDetail.UpdatedAt = DateTime.UtcNow;
            }

            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Booking details status updated successfully.",
                endAt = bookingDetails.Any(bd => bd.Status == "4") ? booking.EndAt : null,
                driverPoint = driver.Price,
                bookingDetailStatuses = bookingDetails.Select(bd => new
                {
                    bd.Id,
                    bd.Status
                })
            });
        }


        [Authorize]
        [HttpGet("get-status-2-3")]
        public async Task<IActionResult> TripAcceptedAndDelete()
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

            // Sửa điều kiện lấy các BookingDetail với các status là 2, 3, 4, 5
            var bookingDetails = await _context.BookingDetails
                .Where(bd => bd.Taxi.DriverId == driverId &&
                             (bd.Status == "2" || bd.Status == "3" || bd.Status == "4" || bd.Status == "5"))
                .Include(bd => bd.Booking)
                .Include(bd => bd.Booking.Arival)
                .Include(bd => bd.Booking.Customer)
                .ToListAsync();

            if (bookingDetails == null || bookingDetails.Count == 0)
            {
                return NotFound(new { message = "No assigned bookings found with statuses 2, 3, 4, or 5." });
            }

            var bookingList = new List<object>();

            foreach (var bd in bookingDetails)
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
                    Count = booking.Count,
                    HasFull = booking.HasFull,
                    InviteId = booking.InviteId,
                    Status = bd.Status,
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


        [Authorize]
        [HttpGet("booking-nodriver")]
        public async Task<IActionResult> GetBookingNoDriver()
        {
            // Lấy danh sách Booking chưa có BookingDetail
            var bookingsWithoutDetails = await _context.Bookings
                .Where(b => !_context.BookingDetails.Any(bd => bd.BookingId == b.Id) && b.DeletedAt == null)
                .Include(b => b.Arival)
                .Include(b => b.Customer)
                .ToListAsync();
            
            // Lấy danh sách Booking có chính xác 5 BookingDetails
            var bookingsWithFiveDetails = await _context.Bookings
                .Where(b => _context.BookingDetails.Count(bd => bd.BookingId == b.Id) == 5 && b.DeletedAt == null)
                .Include(b => b.Arival)
                .Include(b => b.Customer)
                .ToListAsync();

            // Kết hợp hai danh sách
            var combinedBookings = bookingsWithoutDetails.Concat(bookingsWithFiveDetails).ToList();

            if (!combinedBookings.Any())
            {
                return Ok(new { message = "No bookings found matching the criteria." });
            }

            // Chuẩn bị dữ liệu trả về
            var bookingList = new List<object>();

            foreach (var booking in combinedBookings)
            {
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
                    }
                });
            }

            // Trả về kết quả
            return Ok(new { data = bookingList });
        }

        [Authorize]
        [HttpPut("cancel-booking")]
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

            // Tìm booking detail liên quan đến tài xế
            var bookingDetail = await _context.BookingDetails
                .Include(bd => bd.Booking)
                .FirstOrDefaultAsync(bd => bd.BookingId == bookingId && bd.Taxi.DriverId == driverId);

            if (bookingDetail == null)
            {
                return NotFound(new { message = "Booking detail not found or not associated with this driver." });
            }

            if (bookingDetail.Booking.DeletedAt != null)
            {
                return BadRequest(new { message = "This booking has already been deleted." });
            }

            switch (bookingDetail.Status)
            {
                case "1":
                //case "2":
                    bookingDetail.Status = "5"; 
                    _context.BookingDetails.Update(bookingDetail);
                    await _context.SaveChangesAsync();

                    return Ok(new { message = "Booking has been successfully canceled and updated." });

                case "3":
                    return BadRequest(new { message = "Cannot cancel booking: Driver is already picking up the customer." });

                case "4":
                    return BadRequest(new { message = "Cannot cancel booking: Driver has already completed the booking." });

                default:
                    return BadRequest(new { message = "Cannot cancel booking: Invalid status." });
            }
        }
       
        [Authorize]
        [Authorize]
        [HttpPost("claim-booking")]
        public async Task<IActionResult> ClaimBooking([FromBody] DriverBookingStoreDto request)
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

            // Kiểm tra BookingDetail liên quan đến BookingId
            var existingBookingDetails = await _context.BookingDetails
                .Where(bd => bd.BookingId == request.BookingId)
                .ToListAsync();

            // Kiểm tra nếu tồn tại trạng thái khác "5" (1, 2, 3, 4)
            if (existingBookingDetails.Any(bd => bd.Status != "5"))
            {
                return BadRequest(new { message = "This booking has already been claimed or is in progress." });
            }

            // Kiểm tra tài xế và taxi
            var taxi = await _context.Taxies
                .Where(t => t.DriverId == driverId && t.InUse == true)
                .FirstOrDefaultAsync();

            if (taxi == null)
            {
                return NotFound(new { message = "No available taxi in use for this driver." });
            }

            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver == null)
            {
                return NotFound(new { message = "Driver not found." });
            }

            decimal commissionRate = driver.Commission.GetValueOrDefault(0);
            decimal bookingPrice = booking.Price.GetValueOrDefault(0);
            decimal deduction = (bookingPrice * commissionRate) / 100;

            if (driver.Price.GetValueOrDefault(0) < (int)deduction)
            {
                return BadRequest(new { message = "The driver doesn't have enough money." });
            }

            driver.Price -= deduction;

            var newWallet = new Wallet
            {
                DriverId = driverId,
                Type = "Successfully received the trip and paid commission.",
                Price = -deduction,
                CreatedAt = DateTime.UtcNow
            };
            await _context.Wallets.AddAsync(newWallet);

            var adminNotification = new AdminNotification
            {
                IsRead = false,
                Title = $"Driver {driver.Fullname} successfully claimed booking #{booking.Code}",
                Content = $"Driver {driver.Fullname} has successfully claimed booking #{booking.Code} and a commission of {deduction:C} has been deducted from their wallet.",
                Navigate = $"/booking/{booking.Code}"
            };
            await _context.AdminNotifications.AddAsync(adminNotification);

            var currentSeatCount = await _context.BookingDetails
                .Where(bd => bd.TaxiId == taxi.Id && bd.Status == "2")
                .SumAsync(bd => bd.Booking.Count);

            if (currentSeatCount + booking.Count > taxi.Seat)
            {
                return BadRequest(new { message = "The taxi has reached its seat limit for current bookings." });
            }

            var commission = driver.Commission;
            if (commission == null)
            {
                return NotFound(new { message = "Commission not found for this driver." });
            }

            // Tạo BookingDetail mới
            var newBookingDetail = new BookingDetail
            {
                BookingId = request.BookingId,
                TaxiId = taxi.Id,
                Status = "2",
                Commission = commission,
                TotalPrice = booking.Price - deduction,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.BookingDetails.AddAsync(newBookingDetail);

            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Booking successfully claimed and status updated to '2'." });
        }

        [Authorize]
        [HttpPost("update-booking-detail-1")]
        public async Task<IActionResult> UpdateBookingDetail([FromBody] DriverBookingStoreDto request)
        {
            // Lấy DriverId từ Claims
            var driverIdClaim = User.Claims.FirstOrDefault(c => c.Type == "DriverId")?.Value;
            if (string.IsNullOrEmpty(driverIdClaim) || !int.TryParse(driverIdClaim, out int driverId))
            {
                return Unauthorized(new { message = "Unauthorized: Driver ID not found." });
            }

            // Tìm Booking theo BookingId
            var booking = await _context.Bookings.FindAsync(request.BookingId);
            if (booking == null)
            {
                return Ok(new { message = "Booking not found." });
            }

            // Kiểm tra và lấy BookingDetail liên quan đến BookingId
            var bookingDetail = await _context.BookingDetails
                .Where(bd => bd.BookingId == request.BookingId)
                .FirstOrDefaultAsync();

            if (bookingDetail == null)
            {
                return Ok(new { message = "BookingDetail not found." });
            }

            // Chỉ cập nhật nếu trạng thái hiện tại là "1"
            if (bookingDetail.Status != "1")
            {
                return Ok(new { message = "BookingDetail status must be '1' to update." });
            }

            // Tìm taxi của tài xế
            var taxi = await _context.Taxies
                .Where(t => t.DriverId == driverId && t.InUse == true)
                .FirstOrDefaultAsync();

            if (taxi == null)
            {
                return Ok(new { message = "No available taxi in use for this driver." });
            }

            // Kiểm tra tài xế và xác minh tài xế không nhận chuyến của chính mình
            var driver = await _context.Drivers.FindAsync(driverId);
            if (driver == null)
            {
                return Ok(new { message = "Driver not found." });
            }

            if (driverId == booking.InviteId)
            {
                return Ok(new { message = "The driver cannot claim his trip." });
            }

            // Kiểm tra ghế trống trên taxi
            var currentSeatCount = await _context.BookingDetails
                .Where(bd => bd.TaxiId == taxi.Id && bd.Status == "2")
                .SumAsync(bd => bd.Booking.Count);

            if (currentSeatCount + booking.Count > taxi.Seat)
            {
                return Ok(new { message = "The taxi has reached its seat limit for current bookings." });
            }

            // Tính toán hoa hồng
            decimal commissionRate = driver.Commission.GetValueOrDefault(0);
            decimal bookingPrice = booking.Price.GetValueOrDefault(0);
            decimal deduction = (bookingPrice * commissionRate) / 100;

            // Kiểm tra ví tài xế
            if (driver.Price.GetValueOrDefault(0) < deduction)
            {
                return Ok(new { message = "The driver doesn't have enough money." });
            }
            var commission = driver.Commission;
            if (commission == null)
            {
                return Ok(new { message = "Commission not found for this driver." });
            }
            // Trừ tiền từ ví tài xế
            driver.Price -= deduction;

            var existingBookingDetail = await _context.BookingDetails
                 .FirstOrDefaultAsync(bd => bd.BookingId == request.BookingId);

            if (existingBookingDetail != null)
            {
                // Cập nhật nếu đã tồn tại
                existingBookingDetail.TaxiId = taxi.Id;
                existingBookingDetail.Status = "2";
                existingBookingDetail.Commission = commission;
                existingBookingDetail.TotalPrice = booking.Price - deduction;
                existingBookingDetail.UpdatedAt = DateTime.UtcNow;
            }
            // Cập nhật thời gian cập nhật Booking
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Ghi nhận giao dịch vào ví
            var newWallet = new Wallet
            {
                DriverId = driverId,
                Type = "Successfully updated the booking and paid commission.",
                Price = -deduction,
                CreatedAt = DateTime.UtcNow
            };
            await _context.Wallets.AddAsync(newWallet);

            var adminNotification = new AdminNotification
            {
                IsRead = false,
                Title = $"Driver {driver.Fullname} updated booking #{booking.Code}",
                Content = $"Driver {driver.Fullname} has updated booking #{booking.Code} and a commission of {deduction:C} has been deducted from their wallet.",
                Navigate = $"/booking/{booking.Code}"
            };
            await _context.AdminNotifications.AddAsync(adminNotification);

            // Lưu thay đổi vào database
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "BookingDetail successfully updated to '2'.",
                bookingDetailId = bookingDetail.Id,
                driverBalance = driver.Price
            });
        }

        [HttpPost("store")]
        public async Task<IActionResult> Store([FromBody] BookingRequestDto request)
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

            // Validate the request
            if (request == null)
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid data."
                });
            }

            Customer customer;

            if (!string.IsNullOrEmpty(request.Name) && !string.IsNullOrEmpty(request.Phone))
            {
                customer = new Customer
                {
                    Name = request.Name,
                    Phone = request.Phone
                };
                await _context.Customers.AddAsync(customer);
            }
            else
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Please select or create a new customer!"
                });
            }

            if (request.PickUpId == null)
            {
                var pickupConfig = await _context.Configs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "default_arival_pickup");
                if (pickupConfig != null)
                {
                    request.PickUpId = int.Parse(pickupConfig.Value);
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Pick-up point configuration not found!"
                    });
                }
            }

            if (request.DropOffId == null)
            {
                var dropoffConfig = await _context.Configs
                    .FirstOrDefaultAsync(c => c.ConfigKey == "default_arival_dropoff");
                if (dropoffConfig != null)
                {
                    request.DropOffId = int.Parse(dropoffConfig.Value);
                }
                else
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Drop-off point configuration not found!"
                    });
                }
            }

            if (!await _context.Wards.AnyAsync(w => w.Id == request.PickUpId))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid pick-up point!"
                });
            }

            // Create Arival and handle pricing
            var arival = new Arival
            {
                Type = request.Types,
                PickUpId = request.PickUpId,
                PickUpAddress = request.PickUpAddress,
                DropOffId = request.DropOffId,
                DropOffAddress = request.DropOffAddress
            };

            decimal price = 0;

            if (request.Types == "province")
            {
                var ward = await _context.Wards.FirstOrDefaultAsync(w => w.Id == request.DropOffId);

                if (!await _context.Wards.AnyAsync(w => w.Id == request.DropOffId))
                {
                    return BadRequest(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Invalid drop-off point!"
                    });
                }
                if (ward != null)
                {
                    var district = await _context.Districts.FirstOrDefaultAsync(d => d.Id == ward.DistrictId);
                    if (district != null)
                    {
                        var province = await _context.Provinces.FirstOrDefaultAsync(p => p.Id == district.ProvinceId);
                        if (province != null)
                        {
                            price = province.Price.Value;
                        }
                        else
                        {
                            return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Province not found." });
                        }
                    }
                    else
                    {
                        return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "District not found." });
                    }
                }
                else
                {
                    return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Ward not found." });
                }
            }
            else if (request.Types == "airport")
            {
                arival.DropOffId = null;
                arival.DropOffAddress = null;

                var airportConfig = await _context.Configs.FirstOrDefaultAsync(c => c.ConfigKey == "airport_price");
                if (airportConfig != null)
                {
                    price = decimal.Parse(airportConfig.Value);
                }
                else
                {
                    return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Airport price config not found." });
                }
            }
            else
            {
                return BadRequest(new { code = CommonErrorCodes.InvalidData, message = "Invalid type for Arival." });
            }

            arival.Price = price;

            // Save Arival
            await _context.Arivals.AddAsync(arival);
            await _context.SaveChangesAsync();

            // Create Booking
            var booking = new Booking
            {
                Code = "XG" + DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CustomerId = customer.Id,
                ArivalId = arival.Id,
                StartAt = DateOnly.FromDateTime(DateTime.UtcNow),
                EndAt = null,
                Count = request.Count,
                Price = arival.Price,
                HasFull = request.HasFull,
                InviteId = driverId,
            };

            await _context.Bookings.AddAsync(booking);
            await _context.SaveChangesAsync();

            var taxi = await FindDriverHelper.FindDriver(booking.Id, 0, _context);

            if (taxi == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    message = "Wait for the driver to accept this trip!"
                });
            }


            // Format the customer's phone number
            //var customerPhoneNumber = customer.Phone;
            //if (customerPhoneNumber.StartsWith("0"))
            //{
            //    customerPhoneNumber = "+84" + customerPhoneNumber.Substring(1);
            //}

            //try
            //{
            //    // Initialize Twilio Client
            //    TwilioClient.Init(configuation["Twilio:AccountSid"], configuation["Twilio:AuthToken"]);

            //    // Send SMS to customer with booking code
            //    var message = MessageResource.Create(
            //        body: $"Your booking code is: {booking.Code}.",
            //        from: new PhoneNumber(configuation["Twilio:PhoneNumber"]),
            //        to: new PhoneNumber(customerPhoneNumber)
            //    );
            //}
            //catch (Exception ex)
            //{
            //    return StatusCode(500, new
            //    {
            //        code = CommonErrorCodes.ServerError,
            //        message = "Failed to send SMS.",
            //        error = ex.Message,
            //        stackTrace = ex.StackTrace
            //    });
            //}

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = new { bookingId = booking.Id },
                message = "Trip created successfully and SMS sent to the customer!"
            });
        }

    }
}
