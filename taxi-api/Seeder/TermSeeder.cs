using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Seeder
{
    public class TermSeeder
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new TaxiContext(
                serviceProvider.GetRequiredService<DbContextOptions<TaxiContext>>()))
            {
                if (!context.Terms.Any()) 
                {
                    var terms = new List<Term>
                    {
                        new Term
                        {
                            Title = "Terms for App Users",
                            Slug = "terms-app-users-app",
                            Content = "These are the terms and conditions for using the taxi app services. Users must agree before they can access the services.",
                            Type = "app",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Term
                        {
                            Title = "Terms for App Users",
                            Slug = "terms-app-users-web",
                            Content = "These are the terms and conditions for using the taxi app services. Users must agree before they can access the services.",
                            Type = "web",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Term
                        {
                            Title = "Refund Policy",
                            Slug = "refund-policy-app",
                            Content = "Our refund policy ensures customer satisfaction and reliability. If there are any issues with your service, we guarantee a fair resolution.",
                            Type = "app",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Term
                        {
                            Title = "Refund Policy",
                            Slug = "refund-policy-web",
                            Content = "Our refund policy ensures customer satisfaction and reliability. If there are any issues with your service, we guarantee a fair resolution.",
                            Type = "web",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Term
                        {
                            Title = "Driver Code of Conduct",
                            Slug = "driver-code-of-conduct-app",
                            Content = "Drivers must adhere to these rules for providing safe rides. Violating these rules may result in penalties or termination of their account.",
                            Type = "app",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Term
                        {
                            Title = "Driver Code of Conduct",
                            Slug = "driver-code-of-conduct-web",
                            Content = "Drivers must adhere to these rules for providing safe rides. Violating these rules may result in penalties or termination of their account.",
                            Type = "web",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Term
                        {
                            Title = "User Agreement",
                            Slug = "user-agreement-app",
                            Content = "This agreement outlines the terms of using our taxi services, including your rights, responsibilities, and any limitations that may apply.",
                            Type = "app",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        },
                        new Term
                        {
                            Title = "User Agreement",
                            Slug = "user-agreement-web",
                            Content = "This agreement outlines the terms of using our taxi services, including your rights, responsibilities, and any limitations that may apply.",
                            Type = "web",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        }
                    };

                    context.Terms.AddRange(terms);
                    context.SaveChanges();
                }
            }
        }
    }
}
