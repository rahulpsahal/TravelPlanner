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

## Project structure

| Path | Purpose |
| --- | --- |
| [frontend/](frontend/) | Angular app and development proxy |
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

## 3. Test OTP verification in Swagger

Start the backend with `dotnet run` and open [Swagger UI](http://localhost:5274/swagger).

1. Call **POST /api/auth/otp/request** with `{"phoneNumber":"+919876543210"}`. Copy `challengeId` and `developmentCode` from the response.
2. Call **POST /api/auth/otp/verify** with those values as `challengeId` and `code`. Send the code as a string to preserve any leading zeros.
3. Expect HTTP 200 and `{"status":"signup_required"}`. Successful verification consumes the code.

Codes expire after five minutes and allow five failed attempts. Wait 30 seconds before requesting another code for the same number. A new code replaces the previous one. SQL Server stores a keyed hash of the code. Restarting the backend invalidates outstanding codes because the development signing key is regenerated. OTP logs record outcomes and attempt counts without logging phone numbers or codes. These routes are available only in Development. SMS delivery, account lookup, signup, and login sessions are pending.

## 4. Start the Angular preview

In a second terminal, run these commands from the repository root:

```powershell
cd frontend
npm ci
npm start
```

Open the [Angular starter page](http://localhost:4200/). The [proxied health endpoint](http://localhost:4200/api/health) checks that Angular can reach ASP.NET Core on port 5274. The trip form is the next feature to connect.

## 5. Deploy the public beta

Once the repository is available to Render, create a Blueprint using [render.yaml](render.yaml). You can also create a Free Docker web service manually using the root Dockerfile and `/api/health` as the health check path. The container serves Angular and the API from the same HTTPS address. After deploying, open the public homepage and check `/api/health`. [Render deployment guide](https://render.com/docs/free)

### Provider APIs

Use Swagger's **Travel providers** section or the examples in [TravelPlanner.Api.http](backend/TravelPlanner.Api.http).

| Endpoint | Input and result |
| --- | --- |
| `GET /api/travel/hotels` | Provide `destination=Goa`. Returns hotel-related web links and snippets, with no room rates or availability. |
| `GET /api/travel/trains/{trainNumber}/fare` | Provide `source`, `destination`, `travelClass`, and `date`. Defaults to general quota `GN`. Returns the provider's fare in INR for a known train and class, with a link to confirm availability on IRCTC. |
| `GET /api/travel/flights` | Provide `flightCode=AI101` or `origin=DEL&destination=GOI`. Returns current flight status and airport details. Airfares and future schedules are outside this endpoint. |
| `POST /api/travel/itinerary` | Provide `destination`, `days` (1–14), and optional `interests`. Returns a suggested itinerary as text from Groq. |

The sample trip preview still runs without keys. Each provider endpoint returns HTTP 503 if its key is missing. The four endpoints share a per-instance rate limit of 10 requests per minute. Provider quotas can impose lower limits.

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

1. Add a verified signup session, SQL Server user registration, sign-in sessions, and protected dashboard access.
2. Connect the Angular trip form to the preview API and display budget results and validation errors.
3. Choose hosted SQL Server for deployment and add tables for saving and reopening trips.
4. Test provider coverage with credentials before connecting the travel endpoints to the dashboard. Identify sources for live hotel rates, airfares, and bus options.

Update this README in the same commit whenever a change affects setup, API requests or responses, environment variables, deployment, or milestone status.
