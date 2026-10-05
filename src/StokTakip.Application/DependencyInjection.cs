using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StokTakip.Application.Services;

namespace StokTakip.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s)
    {
        s.AddValidatorsFromAssemblyContaining<LoginDtoValidator>();
        s.AddScoped<IAuditLogger, AuditLogger>();
        s.AddScoped<AuthService>();
        s.AddScoped<UserService>();
        s.AddScoped<CategoryService>();
        s.AddScoped<PartService>();
        s.AddScoped<StockService>();
        s.AddScoped<DashboardService>();
        s.AddScoped<SupportService>();
        s.AddScoped<LeaveService>();
        s.AddScoped<AuditQueryService>();
        return s;
    }
}
