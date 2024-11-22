using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using taxi_api.Models;

namespace taxi_api.Seeder
{
    public class SeederAdmin
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new TaxiContext(
                serviceProvider.GetRequiredService<
                    DbContextOptions<TaxiContext>>()))
            {
                // Kiểm tra nếu đã có Admin
                if (!context.Admins.Any())
                {
                    // Tạo admin mặc định
                    var passwordHasher = serviceProvider.GetRequiredService<IPasswordHasher<Admin>>();

                    var admin = new Admin
                    {
                        Name = "Admin",
                        Email = "Admin@taxi.com",
                        Phone = "0123456789",
                        Role = "SuperAdmin",
                        DeletedAt = null,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    admin.Password = passwordHasher.HashPassword(admin, "Admin");

                    // Thêm admin vào context
                    context.Admins.Add(admin);
                }
                context.SaveChanges();
            }
        }
    }
}
