using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Notification
{
    public int Id { get; set; }

    public int? DriverId { get; set; }

    public bool? IsRead { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Driver? Driver { get; set; }
}
