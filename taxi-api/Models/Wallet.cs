using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Wallet
{
    public int Id { get; set; }

    public int? DriverId { get; set; }

    public string? Type { get; set; }

    public decimal? Price { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Driver? Driver { get; set; }
}
