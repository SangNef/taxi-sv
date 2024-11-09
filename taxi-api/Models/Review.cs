using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Review
{
    public int Id { get; set; }

    public int? BookingDetailId { get; set; }

    public string? Review1 { get; set; }

    public int? Rate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual BookingDetail? BookingDetail { get; set; }
}
