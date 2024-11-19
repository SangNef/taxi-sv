using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Collections.Generic;
using taxi_api.Models;

namespace taxi_api.Seeder
{
    public class ConfigSeeder
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new TaxiContext(
                serviceProvider.GetRequiredService<
                    DbContextOptions<TaxiContext>>()))
            {
                // Kiểm tra nếu đã có Admin
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
                            Value = "10",
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
                        }
                    };

                    // Thêm danh sách config vào context
                    context.Configs.AddRange(configs);
                }
                context.SaveChanges();
            }
        }
    }
}
