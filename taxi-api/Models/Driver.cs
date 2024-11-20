using System;
using System.Collections.Generic;

namespace taxi_api.Models;

public partial class Driver
{
    public int Id { get; set; }

    public string? Fullname { get; set; }

    public string? Phone { get; set; }

    public string? Password { get; set; }

    public bool? IsActive { get; set; }

    public decimal? Price { get; set; }

    public int? Commission { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<PaymentHistory> PaymentHistories { get; set; } = new List<PaymentHistory>();

    public virtual ICollection<Taxy> Taxies { get; set; } = new List<Taxy>();

    public virtual ICollection<Wallet> Wallets { get; set; } = new List<Wallet>();
}
