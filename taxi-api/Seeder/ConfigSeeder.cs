using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

public class ConfigSeeder
{
    public static void Initialize(IServiceProvider serviceProvider)
    {
        using (var context = new TaxiContext(
            serviceProvider.GetRequiredService<DbContextOptions<TaxiContext>>()))
        {
            // Kiểm tra nếu đã có Configs
            if (!context.Configs.Any())
            {
                var configs = new List<Config>
                {
                    new Config
                    {
                        ConfigKey = "airport_price",
                        Name = "airport_price",
                        Value = "100000",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Config
                    {
                        ConfigKey = "default_arival_pickup",
                        Name = "pickup_id",
                        Value = "1",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Config
                    {
                        ConfigKey = "default_arival_dropoff",
                        Name = "dropoff_id",
                        Value = "1",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Config
                    {
                        ConfigKey = "default_comission",
                        Name = "default_commission",
                        Value = "30",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Config
                    {
                        ConfigKey = "default_royalty",
                        Name = "default_royalty",
                        Value = "50",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Config
                    {
                        ConfigKey = "main_color",
                        Name = "main_color",
                        Value = "#FFFF00", 
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Config
                    {
                        ConfigKey = "homescreen_banner",
                        Name = "homescreen_banner",
                        Value = "https://res.cloudinary.com/dx2o9ki2g/image/upload/v1732002062/cum5i1alrfrpgkrqfckb.webp", 
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                     new Config
                    {
                        ConfigKey = "refund_3_day",
                        Name = "refund_3_day",
                        Value = "80",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Config
                    {
                        ConfigKey = "refund_1_day",
                        Name = "refund_1_day",
                        Value = "50",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    },
                    new Config
                    {
                        ConfigKey = "refund_overdue",
                        Name = "refund_overdue",
                        Value = "0",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }
                };

                // Thêm danh sách config vào context
                context.Configs.AddRange(configs);
                context.SaveChanges();
            }
        }
    }
}
