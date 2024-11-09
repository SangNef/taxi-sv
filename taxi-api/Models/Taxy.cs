using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Taxy
{
    public int Id { get; set; }

    public int? DriverId { get; set; }

    public string? Name { get; set; }

    public string? LicensePlate { get; set; }

    public int? Seat { get; set; }

    public bool? InUse { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();

    public virtual Driver? Driver { get; set; }
}
