using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Arival
{
    public int Id { get; set; }

    public string? Type { get; set; }

    public int? PickUpId { get; set; }

    public string? PickUpAddress { get; set; }

    public int? DropOffId { get; set; }

    public string? DropOffAddress { get; set; }

    public decimal? Price { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual Ward? DropOff { get; set; }

    public virtual Ward? PickUp { get; set; }
}
