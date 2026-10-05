using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StokTakip.Application;

namespace StokTakip.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration cfg)
    {
        s.AddDbContext<AppDbContext>(o => o.UseSqlServer(cfg.GetConnectionString("Default")));
        s.AddScoped<IUnitOfWork, UnitOfWork>();
        s.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        s.Configure<JwtOptions>(cfg.GetSection("Jwt"));
        s.AddSingleton<ITokenService, JwtTokenService>();
        return s;
    }
}
