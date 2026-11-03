# Vercel + Azure App Service + Azure SQL deployment

Carpediem is deployed as a static Angular SPA on Vercel and a .NET 10 ASP.NET Core API on Azure App Service's Free (F1) plan. Azure SQL Database's free offer preserves the existing SQL Server/EF Core provider, so no database migration is required.

## Provision the API host

1. In Azure Portal, create a **Web App** with these settings:
   - Publish: **Code**
   - Runtime: **.NET 10 (LTS)**
   - Operating system: **Windows**
   - Pricing plan: **Free F1**
2. Under **Settings → Environment variables**, add:

   | Name | Value |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__Carpediem` | Azure SQL connection string (mark as deployment slot setting) |
   | `ReverseProxy__TrustedNetworks__0` | `10.0.0.0/8` |

3. In **Deployment Center**, link `shawky2002020/carpediem`, branch `main`, with `/src/Carpediem.Web` as the application path. Configure the build to publish `src/Carpediem.Web/Carpediem.Web.csproj` for `net10.0`.
4. Confirm `https://<api-app>.azurewebsites.net/healthz` returns `200`.

## Provision Azure SQL

1. Create an Azure SQL logical server and a database named `Carpediem`; choose **Apply offer** on the free database page.
2. Temporarily allow your development IP in the logical server firewall, then apply the migration:

   ```powershell
   $env:ConnectionStrings__Carpediem = "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=Carpediem;User ID=<admin>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30"
   dotnet ef database update --project src/Infrastructure/Carpediem.Infrastructure.csproj --startup-project src/Carpediem.Web/Carpediem.Web.csproj
   ```

3. Enable Azure-service access on the logical server firewall so App Service can reach the database.

> Production deliberately does not seed local development identities. Create the initial administrator using the approved operational bootstrap process before exposing the application publicly.

## Route Vercel API traffic

After Azure provides the API hostname, add this rewrite **before** the SPA fallback in `frontend/vercel.json`:

```json
{
  "source": "/api/:path*",
  "destination": "https://<api-app>.azurewebsites.net/api/:path*"
}
```

Proxying API traffic through Vercel keeps the cookie session on one origin and avoids cross-origin cookie/CORS problems. Redeploy the Vercel project after saving the rewrite.

## Free-tier guardrails

- Azure App Service F1 is for development/demo use and has a daily shared-compute allowance.
- Azure SQL's free offer has monthly compute/storage limits; choose auto-pause when the free limit is reached to avoid overage charges.
