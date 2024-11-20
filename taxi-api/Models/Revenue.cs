using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Revenue
{
    public int Id { get; set; }

    public bool Type { get; set; }

    public decimal Amount { get; set; }

    public string? Note { get; set; }
}
