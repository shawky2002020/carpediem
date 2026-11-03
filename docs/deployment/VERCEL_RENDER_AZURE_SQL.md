# Vercel + Render + Azure SQL deployment

This application is deployed as a static Angular SPA on Vercel and a containerized ASP.NET Core API on Render. The API continues to use SQL Server through Azure SQL Database, so no database-provider migration is required.

## Prerequisites

- A Vercel project connected to `shawky2002020/carpediem`, with `frontend` as its root directory.
- A Render account connected to the same repository.
- An Azure subscription with an Azure SQL Database using the free offer.

## Provision Azure SQL

1. Create an Azure SQL logical server and a database named `Carpediem`; select **Apply offer** for the free tier.
2. Temporarily allow your own IP address in the logical server firewall.
3. Run the idempotent migration from a machine that can reach the server:

   ```powershell
   $env:ConnectionStrings__Carpediem = "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=Carpediem;User ID=<admin>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30"
   dotnet ef database update --project src/Infrastructure/Carpediem.Infrastructure.csproj --startup-project src/Carpediem.Web/Carpediem.Web.csproj
   ```

4. In Azure SQL networking, allow the Render service's outbound CIDR ranges for the selected region. Render exposes the exact ranges after the service is created.

> Production does not seed local development users. Create the initial administrator using your approved operational bootstrap process before making the application public.

## Deploy the API on Render

1. In Render, choose **New → Blueprint** and select this repository. Render reads `render.yaml`.
2. Enter `ConnectionStrings__Carpediem` as a secret using the Azure SQL connection string.
3. Deploy and verify `https://<render-service>.onrender.com/healthz` responds with `200`.

The free service sleeps after 15 idle minutes, which means the first API request can take roughly a minute. It is suitable for a demo or hobby environment, not a production SLA.

## Wire Vercel to the API

After Render gives you the final service URL, add this rewrite **before** the SPA fallback in `frontend/vercel.json`:

```json
{
  "source": "/api/:path*",
  "destination": "https://<render-service>.onrender.com/api/:path*"
}
```

Keeping the API behind the Vercel origin preserves the cookie-based session and avoids cross-origin cookie and CORS issues. Re-deploy the Vercel project after this change.

## Verification

1. Visit the Vercel URL and load a deep application route directly; it should return the SPA, not a 404.
2. Confirm `/api/health` through the Vercel domain reaches the API.
3. Register or sign in, then verify `Carpediem.Session` and `Carpediem.Antiforgery` cookies are set on the Vercel origin.
