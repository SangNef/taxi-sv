using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Booking
{
    public int Id { get; set; }

    public string? Code { get; set; }

    public int? CustomerId { get; set; }

    public int? ArivalId { get; set; }

    public int? InviteId { get; set; }

    public DateOnly? StartAt { get; set; }

    public DateTime? EndAt { get; set; }

    public int? Count { get; set; }

    public decimal? Price { get; set; }

    public bool? HasFull { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Arival? Arival { get; set; }

    public virtual ICollection<BookingDetail> BookingDetails { get; set; } = new List<BookingDetail>();

    public virtual Customer? Customer { get; set; }

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
