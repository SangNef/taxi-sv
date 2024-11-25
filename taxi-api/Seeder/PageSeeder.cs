using Microsoft.EntityFrameworkCore;
using taxi_api.Models;

namespace taxi_api.Seeder
{
    public class PageSeeder
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new TaxiContext(
                serviceProvider.GetRequiredService<DbContextOptions<TaxiContext>>()))
            {
                if (!context.Pages.Any())
                {
                    var pages = new List<Page>
                    {
                        new Page
                        {
                            Title = "Terms of Service",
                            Slug = "terms-of-service",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            PageContents = new List<PageContent>
                            {
                                new PageContent
                                {
                                    SubTitle = "User Agreement",
                                    Content = "Users must comply with all regulations and laws relating to carpool bookings. Any fraud, misuse or violation of regulations may result in account blocking or denial of service. We are not responsible for illegal actions or improper use of the service.",
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                },
                                new PageContent
                                {
                                    SubTitle = "Payment Policies",
                                    Content = "Payment must be made before booking is confirmed. We do not guarantee reservations if payment is not completed. Users need to check transaction information carefully to avoid confusion. In case of cancellation, refund fees will apply as per applicable policy.",
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                }
                            }
                        },
                        new Page
                        {
                            Title = "Privacy Policy",
                            Slug = "privacy-policy",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            PageContents = new List<PageContent>
                            {
                                new PageContent
                                {
                                    SubTitle = "Data Collection",
                                    Content = "We collect user data to improve service quality, including booking history, contact information and reviews. This data helps improve user experience, ensure efficient service operations, and assist in resolving issues that arise.",
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                },
                                new PageContent
                                {
                                    SubTitle = "Data Usage",
                                    Content = "User data will not be shared with third parties without consent. However, we may use data for analysis, service optimization, or sending notifications related to bookings. We are committed to information security, using security measures to protect data from the risk of unauthorized access or misuse.",
                                    CreatedAt = DateTime.UtcNow,
                                    UpdatedAt = DateTime.UtcNow
                                }
                            }
                        }
                    };

                    context.Pages.AddRange(pages);
                    context.SaveChanges();
                }
            }
        }
    }
}
