using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
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

            // Tìm BookingDetail liên quan đến BookingId
            var bookingDetails = await _context.BookingDetails
                .Where(bd => bd.BookingId == request.BookingId)
                .ToListAsync();

            if (bookingDetails == null || !bookingDetails.Any())
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

            if (booking.Status == "2")
            {
                booking.Status = "3";
            }
            else if (booking.Status == "3")
            {
                booking.Status = "4";
                booking.EndAt = DateTime.UtcNow;

                if (booking.InviteId != null)
                {
                    var driverInvite = await _context.Drivers.FindAsync(booking.InviteId);

                    if (driverInvite == null)
                    {
                        return NotFound(new { message = "DriverInvite not found." });
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

                    var newWallet = new Wallet
                    {
                        DriverId = driverInvite.Id,
                        Type = "Successfully received paid Royalty.",
                        Price = +result2,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _context.Wallets.AddAsync(newWallet);

                    await _context.SaveChangesAsync();
                }
            }
            else
            {
                return BadRequest(new { message = "Invalid booking status transition." });
            }

            foreach (var bookingDetail in bookingDetails)
            {
                bookingDetail.Status = "2";
                bookingDetail.UpdatedAt = DateTime.UtcNow;
            }

            booking.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Booking status updated successfully.",
                bookingStatus = booking.Status,
                endAt = booking.Status == "4" ? booking.EndAt : null,
                driverPoint = driver.Price,
                bookingDetailStatuses = bookingDetails.Select(bd => new
                {
                    bd.Id,
                    bd.Status
                })
            });
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
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = Array.Empty<object>(),
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
                bookingDetail.Status = "3";    

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
        [Authorize]
        [HttpPost("claim-booking")]
        public async Task<IActionResult> ClaimBooking([FromBody] DriverBookingStoreDto request)
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

            if (booking.Status != "1")
            {
                return BadRequest(new { message = "This booking cannot be claimed because it is already accepted or processed." });
            }

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

            if (driver.Price.GetValueOrDefault(0) < (int)deduction)
            {
                return BadRequest(new { message = "The driver doesn't have enough money." });
            }
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

            // Lưu BookingDetail vào database
            await _context.BookingDetails.AddAsync(newBookingDetail);

            // Cập nhật trạng thái của Booking
            booking.Status = "2";
            booking.UpdatedAt = DateTime.UtcNow;

            // Lưu các thay đổi
            await _context.SaveChangesAsync();

            return Ok(new { message = "Booking successfully claimed and status updated to '2'." });
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
                Status = "1",
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
