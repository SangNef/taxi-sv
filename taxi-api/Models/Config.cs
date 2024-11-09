using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Config
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public string? ConfigKey { get; set; }

    public string? Value { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}
