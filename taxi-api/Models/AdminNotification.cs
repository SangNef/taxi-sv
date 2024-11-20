using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class AdminNotification
{
    public int Id { get; set; }

    public bool IsRead { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public string? Navigate { get; set; }
}
