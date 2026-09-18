namespace Carpediem.Web.Security;

public static class AntiforgeryConfiguration
{
    public const string HeaderName = "X-XSRF-TOKEN";

    public static IServiceCollection AddCarpediemAntiforgery(this IServiceCollection services)
    {
        services.AddAntiforgery(options =>
        {
            options.HeaderName = HeaderName;
            options.Cookie.Name = "Carpediem.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });
        return services;
    }
}
