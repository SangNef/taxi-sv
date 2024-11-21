﻿using Microsoft.AspNetCore.Mvc;
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
    public class BookingController : ControllerBase
    {
        private readonly TaxiContext _context;
        private readonly IConfiguration configuation;


        public BookingController(TaxiContext context, IConfiguration configuation)
        {
            _context = context;
            this.configuation = configuation;
        }
          [HttpGet("list")]
            public async Task<IActionResult> GetAllBookings(
                [FromQuery] DateOnly? startDate,
                [FromQuery] DateOnly? endDate,
                [FromQuery] string? Code,
                [FromQuery] string? status,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 10)
            {
                // Khởi tạo truy vấn
                var query = _context.Bookings
                    .Include(b => b.Customer)
                    .Include(b => b.Arival)
                    .Include(b => b.BookingDetails)
                        .ThenInclude(bd => bd.Taxi)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(Code))
                {
                    query = query.Where(b => b.Code.Contains(Code));
                }

                // Lọc theo trạng thái nếu có
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(b => b.Status == status);
                }

                // Tính tổng số bản ghi
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

                // Chuẩn bị phản hồi
                var bookingList = bookings.Select(b => new
                {
                    b.Id,
                    b.Code,
                    CustomerName = b.Customer.Name,
                    b.StartAt,
                    b.EndAt,
                    b.Status,
                    b.Price,
                    b.Count,
                    ArivalDetails = new
                    {
                        b.Arival.PickUpAddress,
                        b.Arival.DropOffAddress,
                        b.Arival.Type,
                        PickUpWard = wards.FirstOrDefault(w => w.Id == b.Arival.PickUpId)?.Name,
                        PickUpDistrict = districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == b.Arival.PickUpId)?.DistrictId)?.Name,
                        PickUpProvince = provinces.FirstOrDefault(p => p.Id == districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == b.Arival.PickUpId)?.DistrictId)?.ProvinceId)?.Name,
                        DropOffWard = wards.FirstOrDefault(w => w.Id == b.Arival.DropOffId)?.Name,
                        DropOffDistrict = districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == b.Arival.DropOffId)?.DistrictId)?.Name,
                        DropOffProvince = provinces.FirstOrDefault(p => p.Id == districts.FirstOrDefault(d => d.Id == wards.FirstOrDefault(w => w.Id == b.Arival.DropOffId)?.DistrictId)?.ProvinceId)?.Name
                    },
                    BookingDetail = b.BookingDetails.Select(bd => new
                    {
                        bd.TaxiId,
                        TaxiDetails = bd.Taxi != null ? new
                        {
                            bd.Taxi.Name,
                            bd.Taxi.LicensePlate
                        } : null
                    })
                });

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
                var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId);
                if (booking == null)
                {
                    return NotFound(new
                    {
                        code = CommonErrorCodes.NotFound,
                        data = (object)null,
                        message = "Booking unvalid."
                    });
                }
                if(booking.Status == "1")
                    {
                    booking.DeletedAt = DateTime.Now;
                    booking.Status = "5";
                     }
                    else
                    {
                        return BadRequest(new
                        {
                            code = CommonErrorCodes.InvalidData,
                            message = "Unable to delete this booking."
                        });
                    }

            await _context.SaveChangesAsync();

                return Ok(new
                {
                    code = CommonErrorCodes.Success,
                    data = new { bookingId = booking.Id },
                    message = "Booking deleted successfully."
                });
            }

        [HttpPost("get-booking-by-code")]
        public async Task<IActionResult> GetBookingByCode([FromBody] BookingCodeRequestDto request)
        {
            // Kiểm tra nếu mã code không hợp lệ
            if (string.IsNullOrEmpty(request.Code))
            {
                return BadRequest(new
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
                .Where(b => b.Code == request.Code)
                .FirstOrDefaultAsync();
            if (booking == null)
            {
                return NotFound(new
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
                Status = booking.Status,
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
                DriverAssignments = booking.BookingDetails.Select(bd => new
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
    }
}
