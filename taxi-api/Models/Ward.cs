using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Ward
{
    public int Id { get; set; }

    public string? Name { get; set; }

    public int DistrictId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Arival> ArivalDropOffs { get; set; } = new List<Arival>();

    public virtual ICollection<Arival> ArivalPickUps { get; set; } = new List<Arival>();

    public virtual District? District { get; set; }
}
