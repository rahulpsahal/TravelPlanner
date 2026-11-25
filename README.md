# Travel Planner

Travel Planner helps travelers compare transport and lodging against a shared budget. The goal is a publicly accessible beta that stays within free hosting and API quotas.

## Where we are

1. Defined the first trip flow around Delhi → Goa, round-trip travel, and a total budget in INR.
2. Scaffolded an Angular 21 frontend and an ASP.NET Core API on .NET 10. Angular has a starter page and a development proxy for API calls.
3. Built a trip preview endpoint using sample flights and hotels. It validates requests, calculates group costs, and returns budget-friendly options.
4. Added Swagger UI for interactive testing and an HTTP request file for repeatable tests.
5. Prepared a Dockerfile and Render Blueprint for public deployment. A live deployment has yet to be verified.
6. Added development-only OTP request and verification endpoints for Indian mobile numbers.
7. Added EF Core with SQL Server LocalDB for authentication data. OTP challenges and verification attempts are saved to SQL Server.
8. Completed the local signup and returning-user flow with cookie authentication and SQL Server accounts.
9. Built the mobile WanderAI sign-in and onboarding flow, with Discover, Planner, Itinerary, Co-Pilot, and Wallet tabs.

## Project structure

| Path | Purpose |
| --- | --- |
| [frontend/](frontend/) | Angular routes, forms, typed API client, and development proxy |
| [backend/Program.cs](backend/Program.cs) | Service registration, middleware, Swagger, and static file hosting |
| [backend/Controllers/](backend/Controllers/) | HTTP routes, binding, and Swagger response metadata |
| [backend/DTOs/](backend/DTOs/) | Public request and response contracts |
| [backend/Services/](backend/Services/) | OTP processing, trip calculations, and external provider integrations |
| [backend/Logging/](backend/Logging/) | Shared request and response logging and endpoint name constants |
| [backend/Data/](backend/Data/) | EF Core context, authentication models, and SQL Server migrations |
| [backend/TravelPlanner.Api.http](backend/TravelPlanner.Api.http) | Example HTTP requests |
| [Dockerfile](Dockerfile) | Packages Angular and ASP.NET Core as one web service |
| [render.yaml](render.yaml) | Render Free service, health check, and secret placeholders |

## 1. Start the backend

Use .NET SDK 10 and Node.js 24 with npm. On Windows, the Development configuration uses the `TravelPlanner` database on `(localdb)\MSSQLLocalDB` with Windows authentication.

From the repository root, install the EF Core CLI once and apply pending migrations:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.14
dotnet ef database update --project backend -- --environment Development
```

The initial migration creates `Users` and `OtpChallenges`. EF Core records applied migrations in `__EFMigrationsHistory`. Then start the backend:

```powershell
cd backend
dotnet run
```

The default launch profile enables Development, and `backend/appsettings.Development.json` sets the local URL. Open [API health](http://localhost:5274/api/health). It should return `{"status":"ok"}`. Keep this terminal running.

## 2. Test the trip preview in Swagger

Open [Swagger UI](http://localhost:5274/swagger), expand **POST /api/trips/preview**, and select **Try it out**. Paste the following request and select **Execute**. Adjust the dates if needed so departure is today or later and return is after departure.

```json
{
  "origin": "Delhi",
  "destination": "Goa",
  "departureDate": "2027-12-10",
  "returnDate": "2027-12-13",
  "travelers": 2,
  "budget": 25000
}
```

Expect **HTTP 200** and two options within budget. The cheapest totals **₹21,600**, including ₹14,400 for round-trip flights and ₹7,200 for three hotel nights. That leaves ₹3,400. Hotel pricing assumes two travelers per room.

- Options are ordered by total price. If nothing fits, the API returns the cheapest option with a negative `budgetDifference`.
- Invalid trip details return HTTP 400 with field-level errors.
- The response identifies prices as demo data. Provider links open general search pages.
- Use Swagger or the [HTTP request file](backend/TravelPlanner.Api.http) to send the POST body. Pasting the endpoint URL into a browser sends GET. Swagger is enabled under the Development launch profile.

## 3. Test signup and login in Swagger

Start the backend with `dotnet run` and open [Swagger UI](http://localhost:5274/swagger).

1. Call **POST /api/auth/otp/request** with `{"phoneNumber":"+919876543210"}`. Copy `challengeId` and `developmentCode` from the response.
2. Call **POST /api/auth/otp/verify** with those values as `challengeId` and `code`. Send the code as a string to preserve any leading zeros.
3. For a new number, expect HTTP 200 and `{"status":"signup_required"}`. Use the same browser and Swagger origin to call **POST /api/auth/signup** with `{"displayName":"Sample Traveler"}`. Expect HTTP 201. Signup uses the phone from the verification cookie.
4. Call **GET /api/auth/me** to see the signed-in account. Call **POST /api/auth/logout**, then check that **GET /api/auth/me** returns HTTP 401.
5. Request and verify another code for the same number after the 30-second cooldown. Expect `{"status":"authenticated"}`, then call **GET /api/auth/me** to confirm you are signed in to the existing account.

Codes are single-use, expire after five minutes, and allow five failed attempts. A new challenge replaces the previous one. SQL Server stores a keyed hash of the code. Restarting the backend invalidates outstanding challenges because the signing key is regenerated. A verified signup cookie expires after 10 minutes. The separate sign-in cookie expires after eight hours. Both are HTTP-only and use SameSite Strict. Auth logs contain metadata only. Development returns a generated code. On Render, opt in to the single-number fixed-code test described below. Public registration for other numbers still needs an SMS provider.

## 4. Start the Angular preview

In a second terminal, run these commands from the repository root:

```powershell
cd frontend
npm ci
npm start
```

Open [Travel Planner](http://localhost:4200/). Enter a mobile number, use the displayed development OTP, and complete signup if prompted. Angular verifies your session before opening `/dashboard`. Sign out from the dashboard to return to login. The [proxied health endpoint](http://localhost:4200/api/health) checks the backend connection.

**Explore without signing in** opens `/preview`, including on public deployments. Discover opens destination presets in Planner. Planner calls the provider APIs for stays, flights, trains, and itineraries. Co-Pilot uses Groq for travel questions and suggested activity swaps. Wallet shows the selected comparison and budget in INR. Wallet selections and itinerary results are for the current page session. Signup preferences are stored in the current browser. Google and Apple sign-in, real bookings, and general SMS sign-in are pending.

## 5. Deploy the public beta

Once the repository is available to Render, create a Blueprint using [render.yaml](render.yaml). You can also create a Free Docker web service manually using the root Dockerfile and `/api/health` as the health check path. The container serves Angular and the API from the same HTTPS address. After deploying, open the public homepage and check `/api/health`. [Render deployment guide](https://render.com/docs/free)

### Hosted SQL Server and fixed-code sign-in

1. In a personal Azure subscription, create an Azure SQL Database named `TravelPlanner`. Apply the free offer and select **Auto-pause the database until next month** for the behavior when the free limit is reached. Windows LocalDB cannot be reached from Render. [Azure SQL free offer](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer)
2. In Render, open the web service's **Connect → Outbound** tab. Add those IP ranges to the Azure SQL server firewall, plus your own client IP for SSMS and migrations. Azure firewall rules use start and end IP addresses. [Render outbound IP addresses](https://render.com/docs/outbound-ip-addresses) · [Azure SQL firewall rules](https://learn.microsoft.com/en-us/azure/azure-sql/database/firewall-configure)
3. Apply EF migrations to the **Azure** database using its admin connection string. Run this from the repo root in PowerShell 7 after installing the EF Core CLI. The connection string includes `Server=tcp:<server>.database.windows.net,1433;Database=TravelPlanner;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;`.

   ```powershell
   $env:ConnectionStrings__TravelPlanner = Read-Host -MaskInput "Azure SQL admin connection string"
   try {
     dotnet ef database update --project backend -- --environment Production
   } finally {
     Remove-Item Env:ConnectionStrings__TravelPlanner
   }
   ```

4. In SSMS, connect directly to the Azure `TravelPlanner` database as the administrator. Replace the placeholder with a generated password, then run this SQL in a private session. Use the app user's connection string in Render.

   ```sql
   CREATE USER [travelplanner_app] WITH PASSWORD = '<generated-password>';
   ALTER ROLE [db_datareader] ADD MEMBER [travelplanner_app];
   ALTER ROLE [db_datawriter] ADD MEMBER [travelplanner_app];
   ```

5. In **Render → Environment**, add the following variables and keep the existing provider keys. Keep ASP.NET Core in Production.

   | Variable | Value |
   | --- | --- |
   | `ConnectionStrings__TravelPlanner` | Azure SQL connection string for the app's SQL user |
   | `Auth__FixedOtpEnabled` | `true` |
   | `Auth__FixedOtpPhone` | Your full number in `+91` format |
   | `Auth__FixedOtpCode` | A randomly chosen six-digit test code |

6. Push the deployment branch `travelplannerAPIs` and confirm that Render deploys the latest commit. If auto-deploy is off, use **Manual Deploy → Deploy latest commit**. Open the HTTPS site, request a code with the configured number, enter the code from Render, and sign up. Sign out and repeat to verify that returning login skips signup. [Render deploys](https://render.com/docs/deploys)

The hosted mode accepts only the configured number, delivers no SMS, and omits the code from API responses. After five incorrect attempts it locks the number for 24 hours, including across resends. Remove `Auth__FixedOtpEnabled` and redeploy after testing to disable fixed-code sign-in.

### Provider APIs

Use Swagger's **Travel providers** section or the examples in [TravelPlanner.Api.http](backend/TravelPlanner.Api.http).

| Endpoint | Input and result |
| --- | --- |
| `GET /api/travel/hotels` | Provide `destination=Goa`. Returns hotel-related web links and snippets, with no room rates or availability. |
| `GET /api/travel/trains/{trainNumber}/fare` | Provide `source`, `destination`, `travelClass`, and `date`. Defaults to general quota `GN`. Returns the provider's fare in INR for a known train and class, with a link to confirm availability on IRCTC. |
| `GET /api/travel/flights` | Provide `flightCode=AI101` or `origin=DEL&destination=GOI`. Returns current flight status and airport details. Airfares and future schedules are outside this endpoint. |
| `POST /api/travel/itinerary` | Provide `destination`, `days` (1–14), and optional `interests`. Returns a suggested itinerary as text from Groq. |
| `POST /api/travel/copilot` | Provide `question` (2–500 characters) and an optional `destination`. Returns travel guidance from Groq. |

The sample trip preview still runs without keys. Each provider endpoint returns HTTP 503 if its key is missing. Provider endpoints share a per-instance rate limit of 10 requests per minute. Provider quotas can impose lower limits.

| Variable | Used for | Provider |
| --- | --- | --- |
| `GROQ_API_KEY` | AI itinerary suggestions | [Groq Console](https://console.groq.com/keys) |
| `TAVILY_API_KEY` | Web search for attractions and travel research | [Tavily dashboard](https://app.tavily.com/) |
| `AVIATIONSTACK_API_KEY` | Flight status and aviation data | [Aviationstack](https://aviationstack.com/) |
| `RAILWAY_API_KEY` | Train fare quotes | [Railway Engine setup](https://railengine.vercel.app/docs/getting-started) |

Enter actual values in Render when setting up the service or under **Environment → Add variable** afterward. For a demo-only deployment, the manual Docker setup works without provider variables. Keep values out of Angular, Git, and the Dockerfile. ASP.NET Core reads process environment variables automatically. For local provider tests, set them in the PowerShell session that starts the backend. Set optional `GROQ_MODEL` to override `openai/gpt-oss-20b`. [Render environment variables](https://render.com/docs/configure-environment-variables)

## API request and response logs

Every `/api` call produces two structured console entries, one when the request starts and one when it finishes. The entries share a `TraceId` and use a route template and `MethodName`, such as `HotelsReqResp`. The response entry includes HTTP status and elapsed time.

When the SQL Server connection is configured, the `AddApiLogs` migration supplies an `ApiLogs` table with one row per call. `RequestJson` contains the method, route template, selected travel query parameters, and JSON body when captured. `ResponseJson` contains the status and JSON body when captured. Both columns are validated as JSON by SQL Server. Auth calls record metadata only. Other JSON bodies up to 16 KiB can be saved, with known credential and contact fields redacted. Requests must have a declared content length for body capture. Headers and raw query strings are omitted. A database logging failure is reported to the console and leaves the API response intact.

For hosted SQL Server, set `ConnectionStrings__TravelPlanner` on the backend and apply migrations to that database before deployment.

Every API call receives a server-generated GUID in the `X-Request-Id` response header. The same value is written to both console entries and to `dbo.ApiLogs.RequestId` when database logging is configured. The `AddApiLogRequestId` migration assigns unique GUIDs to existing log rows and keeps the numeric `Id` for sorting.

When adding an API, define a `ReqResp` name in [ApiLogNames.cs](backend/Logging/ApiLogNames.cs) and assign it to the controller action's HTTP route attribute using `Name = ApiLogNames.YourEndpointReqResp`. Put its public contracts in `DTOs/` and its implementation in `Services/`.

## Next milestones

1. Provision personal Azure SQL and test hosted sign-in for one number. Add real SMS delivery for general public signup.
2. Add tables and a dashboard flow for saving and reopening trips.
3. Verify live provider coverage with credentials. Identify sources for live hotel rates, airfares, and bus options.

Update this README in the same commit whenever a change affects setup, API requests or responses, environment variables, deployment, or milestone status.
