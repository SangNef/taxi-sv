using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Feedback
{
    public int Id { get; set; }

    public int? BookingId { get; set; }

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeleteAt { get; set; }

    public virtual Booking? Booking { get; set; }
}
