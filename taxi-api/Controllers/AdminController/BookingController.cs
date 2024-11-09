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
        public async Task<IActionResult> GetAllBookings()
        {
            // Sắp xếp bookings theo LIFO (Last-In, First-Out) dựa trên thời gian tạo (CreatedAt)
            var bookings = await _context.Bookings
                .Include(b => b.Customer)
                .Include(b => b.Arival)
                .Include(b => b.BookingDetails)
                .OrderByDescending(b => b.CreatedAt) // Sắp xếp theo CreatedAt giảm dần
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
                        DriverAssignments = b.BookingDetails.Select(bd => new
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
                        })
                    };
                }
            }));

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = bookingList,
                message = "Successfully retrieved the list of trips."
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

            // Validate PickUpId and DropOffId, set from Config if not provided
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

            // Validate PickUpId and DropOffId
            if (!await _context.Wards.AnyAsync(w => w.Id == request.PickUpId))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid pick-up point!"
                });
            }

            if (!await _context.Wards.AnyAsync(w => w.Id == request.DropOffId))
            {
                return BadRequest(new
                {
                    code = CommonErrorCodes.InvalidData,
                    data = (object)null,
                    message = "Invalid drop-off point!"
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

            // Format the customer's phone number
            var customerPhoneNumber = customer.Phone;
            if (customerPhoneNumber.StartsWith("0"))
            {
                customerPhoneNumber = "+84" + customerPhoneNumber.Substring(1);
            }

            try
            {
                // Initialize Twilio Client
                TwilioClient.Init(configuation["Twilio:AccountSid"], configuation["Twilio:AuthToken"]);

                // Send SMS to customer with booking code
                var message = MessageResource.Create(
                    body: $"Your booking code is: {booking.Code}.",
                    from: new PhoneNumber(configuation["Twilio:PhoneNumber"]),
                    to: new PhoneNumber(customerPhoneNumber)
                );
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    code = CommonErrorCodes.ServerError,
                    message = "Failed to send SMS.",
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }

            return Ok(new
            {
                code = CommonErrorCodes.Success,
                data = new { bookingId = booking.Id },
                message = "Trip created successfully and SMS sent to the customer!"
            });
        }


        //[HttpPut("edit/{bookingId}")]
        //public async Task<IActionResult> EditBooking(int bookingId, [FromBody] BookingRequestDto request)
        //{
        //    // Kiểm tra xem booking có tồn tại hay không
        //    var booking = await _context.Bookings
        //        .Include(b => b.Arival)
        //        .FirstOrDefaultAsync(b => b.Id == bookingId);

        //    if (booking == null)
        //    {
        //        return NotFound(new
        //        {
        //            code = CommonErrorCodes.NotFound,
        //            data = (object)null,
        //            message = "Booking not found."
        //        });
        //    }

        //    // Cập nhật thông tin khách hàng nếu có
        //    Customer customer;
        //    if (!string.IsNullOrEmpty(request.Name) && !string.IsNullOrEmpty(request.Phone))
        //    {
        //        customer = await _context.Customers.FirstOrDefaultAsync(c => c.Phone == request.Phone);
        //        if (customer != null && customer.Id != booking.CustomerId)
        //        {
        //            return BadRequest(new
        //            {
        //                code = CommonErrorCodes.InvalidData,
        //                data = (object)null,
        //                message = "Phone number already exists for another customer!"
        //            });
        //        }
        //        else if (customer == null)
        //        {
        //            customer = new Customer
        //            {
        //                Name = request.Name,
        //                Phone = request.Phone
        //            };
        //            await _context.Customers.AddAsync(customer);
        //            await _context.SaveChangesAsync();
        //        }
        //    }
        //    else if (request.CustomerId.HasValue)
        //    {
        //        customer = await _context.Customers.FindAsync(request.CustomerId);
        //        if (customer == null)
        //        {
        //            return BadRequest(new
        //            {
        //                code = CommonErrorCodes.InvalidData,
        //                data = (object)null,
        //                message = "Customer does not exist!"
        //            });
        //        }
        //    }
        //    else
        //    {
        //        return BadRequest(new
        //        {
        //            code = CommonErrorCodes.InvalidData,
        //            data = (object)null,
        //            message = "Please select or create a new customer!"
        //        });
        //    }

        //    // Cập nhật điểm đón và điểm trả
        //    if (request.PickUpId == null || !await _context.Wards.AnyAsync(w => w.Id == request.PickUpId))
        //    {
        //        return BadRequest(new
        //        {
        //            code = CommonErrorCodes.InvalidData,
        //            data = (object)null,
        //            message = "Invalid pick-up point!"
        //        });
        //    }

        //    booking.Arival.Type = request.Types;
        //    booking.Arival.Price = request.Price;
        //    booking.Arival.PickUpId = request.PickUpId;
        //    booking.Arival.PickUpAddress = request.PickUpAddress;
        //    booking.Arival.DropOffId = request.DropOffId;
        //    booking.Arival.DropOffAddress = request.DropOffAddress;

        //    if (request.Types == "province")
        //    {
        //        if (request.DropOffId == null || string.IsNullOrEmpty(request.DropOffAddress))
        //        {
        //            return BadRequest(new
        //            {
        //                code = CommonErrorCodes.InvalidData,
        //                data = (object)null,
        //                message = "Please select a destination!"
        //            });
        //        }
        //        booking.Arival.DropOffId = request.DropOffId;
        //        booking.Arival.DropOffAddress = request.DropOffAddress;
        //    }

        //    // Cập nhật thông tin booking
        //    booking.CustomerId = customer.Id;
        //    booking.StartAt = request.StartAt;
        //    booking.Count = request.Count;
        //    booking.Price = request.Price;
        //    booking.HasFull = request.HasFull;

        //    await _context.SaveChangesAsync();

        //    return Ok(new
        //    {
        //        code = CommonErrorCodes.Success,
        //        data = new { bookingId = booking.Id },
        //        message = "Booking updated successfully!"
        //    });
        //}

        [HttpDelete("delete/{bookingId}")]
        public async Task<IActionResult> DeleteBooking(int bookingId)
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
        }
    }
