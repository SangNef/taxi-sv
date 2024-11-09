using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class District
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int? ProvinceId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Province? Province { get; set; }

    public virtual ICollection<Ward> Wards { get; set; } = new List<Ward>();
}
