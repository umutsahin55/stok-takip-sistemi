using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StokTakip.Application;

namespace StokTakip.Api.Infrastructure;

/// <summary>Merkezi hata yönetimi: iç detaylar (stack trace, SQL) istemciye sızdırılmaz.</summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log)
{
    static (int Status, object Body) Map(Exception ex) => ex switch
    {
        ValidationException v => (400, new { error = "Doğrulama hatası.", errors = v.Errors.Select(e => new { field = e.PropertyName, message = e.ErrorMessage }).ToList() }),
        AppException a => (a.StatusCode, new { error = a.Message }),
        DbUpdateConcurrencyException => (409, new { error = "Kayıt başka bir işlem tarafından güncellendi, lütfen tekrar deneyin." }),
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (409, new { error = "Benzersizlik kuralı ihlal edildi (kod/barkod/kullanıcı adı zaten var)." }),
        _ => (500, new { error = "Beklenmeyen bir hata oluştu." })
    };

    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            if (ctx.Response.HasStarted) throw;
            var (status, body) = Map(ex);
            if (status >= 500) log.LogError(ex, "Unhandled exception");
            ctx.Response.Clear();
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(body);
        }
    }
}

/// <summary>Tarayıcı geri-önbellekleme engeli + temel güvenlik başlıkları.</summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["Cache-Control"] = "no-store, no-cache, must-revalidate";
            h["Pragma"] = "no-cache";
            h["Expires"] = "0";
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            if (!ctx.Request.Path.StartsWithSegments("/swagger"))
                h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            return Task.CompletedTask;
        });
        return next(ctx);
    }
}

/// <summary>
/// CSRF: cookie ile kimlik doğrulanan state-değiştiren isteklerde özel başlık zorunlu (X-Requested-With).
/// Cookie SameSite=Strict ile birlikte çift katmanlı koruma sağlar. Bearer header kullanan istemciler etkilenmez.
/// </summary>
public class CsrfGuardMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx)
    {
        var safe = HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method) || HttpMethods.IsOptions(ctx.Request.Method);
        var usesCookie = ctx.Request.Cookies.ContainsKey("access_token") && !ctx.Request.Headers.ContainsKey("Authorization");
        if (!safe && usesCookie && ctx.Request.Headers["X-Requested-With"] != "XMLHttpRequest")
        {
            ctx.Response.StatusCode = 403;
            await ctx.Response.WriteAsJsonAsync(new { error = "CSRF doğrulaması başarısız." });
            return;
        }
        await next(ctx);
    }
}

/// <summary>Controller parametrelerini FluentValidation ile doğrular.</summary>
public class ValidationFilter(IServiceProvider sp) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext ctx, ActionExecutionDelegate next)
    {
        foreach (var arg in ctx.ActionArguments.Values)
        {
            if (arg is null) continue;
            if (sp.GetService(typeof(IValidator<>).MakeGenericType(arg.GetType())) is IValidator v)
            {
                var result = await v.ValidateAsync(new ValidationContext<object>(arg));
                if (!result.IsValid) throw new ValidationException(result.Errors);
            }
        }
        await next();
    }
}
