using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Province
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public decimal? Price { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<District> Districts { get; set; } = new List<District>();
}
