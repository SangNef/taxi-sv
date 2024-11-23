using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Ticket
{
    public int Id { get; set; }

    public int BookingId { get; set; }

    public string? Content { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;
}
