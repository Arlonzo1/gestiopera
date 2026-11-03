using BillaresPos.Application.Common.Abstractions;
using BillaresPos.Infrastructure.Common;
using BillaresPos.Infrastructure.Identity;
using BillaresPos.Infrastructure.Persistence;
using BillaresPos.Infrastructure.Reporting;
using BillaresPos.Infrastructure.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BillaresPos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPostgresPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));
        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPdfService, QuestPdfService>();
        if (bool.Parse(configuration["Firebase:Enabled"] ?? "false"))
            services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();
        else
            services.AddSingleton<IPushNotificationService, FakePushNotificationService>();
        if (bool.Parse(configuration["Email:Enabled"] ?? "false"))
        {
            if (string.Equals(configuration["Email:Provider"], "Brevo", StringComparison.OrdinalIgnoreCase))
                services.AddSingleton<IEmailService>(serviceProvider => new BrevoEmailService(
                    new HttpClient(),
                    configuration,
                    serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<BrevoEmailService>>()));
            else
                services.AddSingleton<IEmailService, SmtpEmailService>();
        }
        else
            services.AddSingleton<IEmailService, DisabledEmailService>();
        if (bool.Parse(configuration["WhatsApp:Enabled"] ?? "false"))
            services.AddSingleton<IWhatsAppService>(serviceProvider => new MetaWhatsAppService(
                new HttpClient(),
                configuration,
                serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<MetaWhatsAppService>>()));
        else
            services.AddSingleton<IWhatsAppService, FakeWhatsAppService>();
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        return services;
    }
}


