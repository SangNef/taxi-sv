using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class PageContent
{
    public int Id { get; set; }

    public int? PageId { get; set; }

    public string? SubTitle { get; set; }

    public string? Content { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Page? Page { get; set; }
}
