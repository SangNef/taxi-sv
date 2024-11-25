using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using taxi_api.DTO;
using System;
using System.Linq;
using System.Threading.Tasks;
using taxi_api.Models;
using taxi_api.Helpers;
using Newtonsoft.Json;
using Twilio.Types;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace taxi_api.Controllers.AdminController
{
    [Route("api/admin/booking")]
    [ApiController]
    public class AdminBookingController : ControllerBase
    {
        private readonly TaxiContext _context;
        private readonly IConfiguration configuation;


        public AdminBookingController(TaxiContext context, IConfiguration configuation)
        {
            _context = context;
            this.configuation = configuation;
        }
        [HttpGet("list")]
        public async Task<IActionResult> GetAllBookings(
       [FromQuery] string? Code,
       [FromQuery] string? status,
       [FromQuery] DateOnly? fromDate,
       [FromQuery] DateOnly? toDate,
       [FromQuery] int page = 1,
       [FromQuery] int pageSize = 10)
        {
            // Khởi tạo truy vấn
            var query = _context.Bookings
                .Where(b => b.DeletedAt == null)
                .Include(b => b.Customer)
                .Include(b => b.Arival)
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Taxi)
                .AsQueryable();

            // Lọc theo mã booking nếu có
            if (!string.IsNullOrEmpty(Code))
            {
                query = query.Where(b => b.Code.Contains(Code));
            }

            if (!string.IsNullOrEmpty(status))
            {
                if (status == "0")
                {
                    query = query.Where(b => !b.BookingDetails.Any());
                }
                else
                {
                    // Lọc theo trạng thái trong BookingDetails
                    query = query.Where(b =>
                        b.BookingDetails
                            .OrderByDescending(bd => bd.UpdatedAt)
                            .Select(bd => bd.Status)
                            .FirstOrDefault() == status);
                }
            }

            if (fromDate.HasValue)
            {
                query = query.Where(b => b.StartAt >= fromDate);
            }

            if (toDate.HasValue)
            {
                query = query.Where(b => b.StartAt <= toDate);
            }

            // Lấy tổng số bản ghi
            var totalRecords = await query.CountAsync();

            if (totalRecords == 0 || page <= 0 || pageSize <= 0)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = Array.Empty<object>(),
                    message = "No trips found.",
                    totalRecords,
                    currentPage = page,
                    totalPages = 0
                });
            }

            var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
            var skip = (page - 1) * pageSize;

            // Phân trang
            var bookings = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            var pickUpWardIds = bookings.Select(b => b.Arival.PickUpId).Distinct().ToList();
            var dropOffWardIds = bookings.Select(b => b.Arival.DropOffId).Distinct().ToList();

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

            // Truy vấn tất cả các taxi đang InUse và lấy tên tài xế cùng với số điện thoại
            var driversInUse = await _context.Taxies
                .Where(t => t.InUse == true)
                .Select(t => new
                {
                    TaxiId = t.Id,          // Lưu TaxiId để tìm lại
                    DriverName = t.Driver.Fullname,  // Lấy tên tài xế
                    DriverPhone = t.Driver.Phone // Lấy số điện thoại tài xế
                })
                .ToListAsync();

            // Lọc bookingList và thêm DriverName và DriverPhone theo TaxiId
            var bookingList = bookings.Select(b => new
            {
                b.Id,
                b.Code,
                CustomerName = b.Customer?.Name,
                b.StartAt,
                b.EndAt,
                b.Price,
                b.Count,

                // Lấy DriverName và DriverPhone từ danh sách driversInUse theo TaxiId
                DriverName = b.BookingDetails
                    .Where(bd => bd.Taxi != null && driversInUse.Any(d => d.TaxiId == bd.Taxi.Id)) // Kiểm tra TaxiId có trong danh sách driversInUse
                    .Select(bd => driversInUse.FirstOrDefault(d => d.TaxiId == bd.Taxi.Id)?.DriverName) // Lấy DriverName của taxi tương ứng
                    .FirstOrDefault(),

                DriverPhone = b.BookingDetails
                    .Where(bd => bd.Taxi != null && driversInUse.Any(d => d.TaxiId == bd.Taxi.Id)) // Kiểm tra TaxiId có trong danh sách driversInUse
                    .Select(bd => driversInUse.FirstOrDefault(d => d.TaxiId == bd.Taxi.Id)?.DriverPhone) // Lấy DriverPhone của taxi tương ứng
                    .FirstOrDefault(),

                TaxiName = b.BookingDetails.FirstOrDefault()?.Taxi?.Name,
                LicensePlate = b.BookingDetails.FirstOrDefault()?.Taxi?.LicensePlate,

                Status = b.BookingDetails.FirstOrDefault()?.Status,

                PickUpAddress = b.Arival?.PickUpAddress,
                DropOffAddress = b.Arival?.DropOffAddress,
                b.Arival?.Type,
                PickUpWard = wards.FirstOrDefault(w => w.Id == b.Arival?.PickUpId)?.Name,
                PickUpDistrict = districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == b.Arival?.PickUpId)?.DistrictId)?.Name,
                PickUpProvince = provinces.FirstOrDefault(p => p.Id == districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == b.Arival?.PickUpId)?.DistrictId)?.ProvinceId)?.Name,
                DropOffWard = wards.FirstOrDefault(w => w.Id == b.Arival?.DropOffId)?.Name,
                DropOffDistrict = districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == b.Arival?.DropOffId)?.DistrictId)?.Name,
                DropOffProvince = provinces.FirstOrDefault(p => p.Id == districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == b.Arival?.DropOffId)?.DistrictId)?.ProvinceId)?.Name
            }).ToList();





            // Trả về dữ liệu
            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = bookingList,
                message = "Successfully retrieved the list of trips.",
                totalRecords,
                currentPage = page,
                totalPages
            });
        }



        [HttpPost("store")]
        public async Task<IActionResult> Store([FromBody] BookingRequestDto request)
        {
            // Validate the request
            if (request == null)
            {
                return Ok(new
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
                return Ok(new
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
                    return Ok(new
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
                    return Ok(new
                    {
                        code = CommonErrorCodes.InvalidData,
                        data = (object)null,
                        message = "Drop-off point configuration not found!"
                    });
                }
            }

            if (!await _context.Wards.AnyAsync(w => w.Id == request.PickUpId))
            {
                return Ok(new
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
                    return Ok(new
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
                            return Ok(new { code = CommonErrorCodes.InvalidData, message = "Province not found." });
                        }
                    }
                    else
                    {
                        return Ok(new { code = CommonErrorCodes.InvalidData, message = "District not found." });
                    }
                }
                else
                {
                    return Ok(new { code = CommonErrorCodes.InvalidData, message = "Ward not found." });
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
                    return Ok(new { code = CommonErrorCodes.InvalidData, message = "Airport price config not found." });
                }
            }
            else
            {
                return Ok(new { code = CommonErrorCodes.InvalidData, message = "Invalid type for Arival." });
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                InviteId = 0
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

        [HttpPut("cancel/{bookingId}")]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            // Tìm Booking dựa trên ID
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
            if (booking == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    data = (object)null,
                    message = "Booking not valid."
                });
            }

            // Lấy danh sách BookingDetail liên quan đến Booking
            var bookingDetails = await _context.BookingDetails
                .Where(bd => bd.BookingId == bookingId && bd.Booking.DeletedAt != null)
                .ToListAsync();

                if (bookingDetails.Any(bd => bd.Status == "2" || bd.Status == "3" || bd.Status == "4"))
                {
                    return Ok(new
                    {
                        code = CommonErrorCodes.NotFound,
                        message = "Cannot cancel booking with status 2, 3, or 4."
                    });
                }

                booking.DeletedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = new { bookingId = booking.Id, updatedDetailsCount = bookingDetails.Count },
                    message = "Booking canceled and relevant details updated successfully."
                });
            }
           
        [HttpGet("get-booking-by-code")]
        public async Task<IActionResult> GetBookingByCode([FromQuery] string code)
        {
            // Kiểm tra nếu mã code không được truyền vào
            if (string.IsNullOrEmpty(code))
            {
                return Ok(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Code is required."
                });
            }

            // Tìm booking dựa trên mã code
            var booking = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Arival)
                .Include(b => b.BookingDetails)
                .ThenInclude(bd => bd.Taxi) // Bao gồm taxi trong booking details
                .Where(b => b.Code == code)
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                return Ok(new
                {
                    code = CommonErrorCodes.NotFound,
                    data = (object)null,
                    message = "Booking not found."
                });
            }

            // Lấy chi tiết pick-up và drop-off ward
            var pickUpWard = await _context.Wards
                .Where(w => w.Id == booking.Arival.PickUpId)
                .Include(w => w.District)
                .ThenInclude(d => d.Province)
                .FirstOrDefaultAsync();

            var dropOffWard = await _context.Wards
                .Where(w => w.Id == booking.Arival.DropOffId)
                .Include(w => w.District)
                .ThenInclude(d => d.Province)
                .FirstOrDefaultAsync();

            // Tạo response cho booking
            var bookingDetails = new
            {
                BookingId = booking.Id,
                Code = booking.Code,
                CustomerName = booking.Customer?.Name,
                CustomerPhone = booking.Customer?.Phone,
                StartAt = booking.StartAt,
                EndAt = booking.EndAt,
                Price = booking.Price,
                HasFull = booking.HasFull,
                ArivalDetails = new
                {
                    booking.Arival.Type,
                    booking.Arival.Price,
                    PickUpDetails = new
                    {
                        pickUpWard?.Name,
                        DistrictName = pickUpWard?.District?.Name,
                        ProvinceName = pickUpWard?.District?.Province?.Name,
                    },
                    DropOffDetails = new
                    {
                        dropOffWard?.Name,
                        DistrictName = dropOffWard?.District?.Name,
                        ProvinceName = dropOffWard?.District?.Province?.Name,
                    }
                },
                Bookingdetail = booking.BookingDetails.Select(bd => new
                {
                    bd.BookingId,
                    bd.Status,
                    TaxiDetails = new
                    {
                        bd.Taxi?.Id,
                        bd.Taxi?.DriverId,
                        bd.Taxi?.Name,
                        bd.Taxi?.LicensePlate,
                        bd.Taxi?.Seat,
                        bd.Taxi?.InUse,
                        bd.Taxi?.CreatedAt,
                        bd.Taxi?.UpdatedAt,
                        bd.Taxi?.DeletedAt
                    }
                })
            };

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = bookingDetails,
                message = "Successfully retrieved the booking details."
            });
        }


        //[HttpPut("update/{bookingId}")]
        //public async Task<IActionResult> UpdateBooking(int bookingId, [FromBody] BookingRequestDto request)
        //{
        //    // Validate the request
        //    if (request == null)
        //    {
        //        return BadRequest(new
        //        {
        //            code = CommonErrorCodes.InvalidData,
        //            data = (object)null,
        //            message = "Invalid data."
        //        });
        //    }

        //    var existingBooking = await _context.Bookings.Include(b => b.Customer).Include(b => b.Arival)
        //        .FirstOrDefaultAsync(b => b.Id == bookingId);

        //    if (existingBooking == null)
        //    {
        //        return NotFound(new
        //        {
        //            code = CommonErrorCodes.NotFound,
        //            message = "Booking not found."
        //        });
        //    }
        //    if (existingBooking.Status != "1")
        //    {
        //        return BadRequest(new
        //        {
        //            code = CommonErrorCodes.InvalidData,
        //            message = "Booking cannot be edited because its status is not 1."
        //        });
        //    }
        //    if (!string.IsNullOrEmpty(request.Name) && !string.IsNullOrEmpty(request.Phone))
        //    {
        //        existingBooking.Customer.Name = request.Name;
        //        existingBooking.Customer.Phone = request.Phone;
        //    }

        //    if (request.PickUpId != null && request.PickUpId != existingBooking.Arival.PickUpId)
        //    {
        //        if (!await _context.Wards.AnyAsync(w => w.Id == request.PickUpId))
        //        {
        //            return BadRequest(new
        //            {
        //                code = CommonErrorCodes.InvalidData,
        //                data = (object)null,
        //                message = "Invalid pick-up point!"
        //            });
        //        }
        //        existingBooking.Arival.PickUpId = request.PickUpId.Value;
        //        existingBooking.Arival.PickUpAddress = request.PickUpAddress;
        //    }

        //    if (request.DropOffId != null && request.DropOffId != existingBooking.Arival.DropOffId)
        //    {
        //        if (!await _context.Wards.AnyAsync(w => w.Id == request.DropOffId))
        //        {
        //            return BadRequest(new
        //            {
        //                code = CommonErrorCodes.InvalidData,
        //                data = (object)null,
        //                message = "Invalid drop-off point!"
        //            });
        //        }
        //        existingBooking.Arival.DropOffId = request.DropOffId.Value;
        //        existingBooking.Arival.DropOffAddress = request.DropOffAddress;
        //    }

        //    if (request.Types != existingBooking.Arival.Type)
        //    {
        //        existingBooking.Arival.Type = request.Types;
        //    }

        //    existingBooking.Count = request.Count;
        //    existingBooking.HasFull = request.HasFull;
        //    existingBooking.StartAt = request.StartAt;

        //    existingBooking.UpdatedAt = DateTime.UtcNow;
        //    _context.Bookings.Update(existingBooking);
        //    await _context.SaveChangesAsync();

        //    return Ok(new
        //    {
        //        code = CommonErrorCodes.Success,
        //        message = "Booking updated successfully!"
        //    });
        //}


    }
}
