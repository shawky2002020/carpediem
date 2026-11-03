# Vercel + MonsterASP.NET + MSSQL deployment

Carpediem uses Vercel for the Angular SPA and MonsterASP.NET's free Windows hosting for the .NET 10 API and MSSQL database. The free plan supplies one `runasp.net` website and one 1 GB MSSQL database; it is suitable for a demo or hobby deployment, rather than production traffic.

## Provision the API host

1. In the MonsterASP.NET Control Panel, create a **Free website** in the EU datacenter and note its `https://<api-name>.runasp.net` address.
2. Create one **MSSQL** database named `Carpediem` and retain its connection details in the control panel only.
3. Publish the API locally:

   ```powershell
   dotnet publish src/Carpediem.Web/Carpediem.Web.csproj -c Release -o artifacts/monsterasp-api
   ```

4. Deploy the contents of `artifacts/monsterasp-api` through the website's Web Deploy, Visual Studio publish profile, Git deployment, or WebFTP option. `web.config` from the publish output must be uploaded with the application.
5. Configure the production connection string in the host's private application configuration (never commit it):

   | Name | Value |
   | --- | --- |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |
   | `ConnectionStrings__Carpediem` | MonsterASP MSSQL connection string |
   | `ReverseProxy__TrustedNetworks__0` | `10.0.0.0/8` |

6. Apply the EF Core migrations to the newly created MSSQL database, then verify `https://<api-name>.runasp.net/healthz` returns `200`.

> Production deliberately does not seed local development identities. Create the initial administrator through the approved operational bootstrap process before opening the application to users.

## Route Vercel API traffic

After MonsterASP.NET provides the API hostname, add this rewrite **before** the SPA fallback in `frontend/vercel.json`:

```json
{
  "source": "/api/:path*",
  "destination": "https://<api-name>.runasp.net/api/:path*"
}
```

Proxying API traffic through Vercel keeps the session cookie on one origin and avoids cross-origin cookie and CORS problems. Commit the hostname-only rewrite and let Vercel redeploy from `main`.

## Free-tier considerations

- The free plan has 256 MB RAM, limited traffic, and no always-running process; the first API request after idling may be slow.
- The free tier only includes a `runasp.net` subdomain and a single database. A custom domain or additional site requires a paid plan.
- HTTPS can be activated in the hosting control panel after the first successful upload.
