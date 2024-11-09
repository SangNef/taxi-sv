using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Page
{
    public int Id { get; set; }

    public string? Title { get; set; }

    public string? Slug { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<PageContent> PageContents { get; set; } = new List<PageContent>();
}
