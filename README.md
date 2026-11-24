# Travel Planner

Travel Planner helps travelers compare transport and lodging against a shared budget. The goal is a publicly accessible beta that stays within free hosting and API quotas.

## Where we are

1. Defined the first trip flow around Delhi → Goa, round-trip travel, and a total budget in INR.
2. Scaffolded an Angular 21 frontend and an ASP.NET Core API on .NET 10. Angular has a starter page and a development proxy for API calls.
3. Built a trip preview endpoint using sample flights and hotels. It validates requests, calculates group costs, and returns budget-friendly options.
4. Added Swagger UI for interactive testing and an HTTP request file for repeatable tests.
5. Prepared a Dockerfile and Render Blueprint for public deployment. A live deployment has yet to be verified.
6. Added development-only OTP request and verification endpoints for Indian mobile numbers.

## Project structure

| Path | Purpose |
| --- | --- |
| [frontend/](frontend/) | Angular app and development proxy |
| [backend/Program.cs](backend/Program.cs) | API routes, Swagger, and static file hosting |
| [backend/Trips/TripPreview.cs](backend/Trips/TripPreview.cs) | Trip validation, sample offers, and budget calculations |
| [backend/TravelPlanner.Api.http](backend/TravelPlanner.Api.http) | Example HTTP requests |
| [Dockerfile](Dockerfile) | Packages Angular and ASP.NET Core as one web service |
| [render.yaml](render.yaml) | Render Free service, health check, and secret placeholders |

## 1. Start the backend

Use .NET SDK 10 and Node.js 24 with npm. From the repository root, run:

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

Codes expire after five minutes and allow five failed attempts. Wait 30 seconds before requesting another code for the same number. A new code replaces the previous one. Challenges are held in memory and reset on restart. These routes are available only in Development. SMS delivery, account lookup, signup, and login sessions are pending.

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

### Provider configuration

The sample trip preview runs without API keys. The Blueprint declares the following server-side variables for future integrations.

| Variable | Planned use | Provider |
| --- | --- | --- |
| `GROQ_API_KEY` | AI itinerary suggestions | [Groq Console](https://console.groq.com/keys) |
| `TAVILY_API_KEY` | Web search for attractions and travel research | [Tavily dashboard](https://app.tavily.com/) |
| `AVIATIONSTACK_API_KEY` | Flight status and aviation data | [Aviationstack](https://aviationstack.com/) |
| `RAILWAY_API_KEY` | Trial of train schedules and fare quotes | [Railway Engine setup](https://railengine.vercel.app/docs/getting-started) |

Enter actual values in Render when setting up the service or under **Environment → Add variable** afterward. For a demo-only deployment, the manual Docker setup works without provider variables. Keep values out of Angular, Git, and the Dockerfile. ASP.NET Core reads process environment variables automatically. For future local provider tests, set them in the PowerShell session that starts the backend. [Render environment variables](https://render.com/docs/configure-environment-variables)

## Next milestones

1. Add a verified signup session, PostgreSQL user accounts, sign-in sessions, and protected dashboard access.
2. Connect the Angular trip form to the preview API and display budget results and validation errors.
3. Add PostgreSQL on Neon for saved trips and reopening plans.
4. Test provider coverage before adding live prices, train and bus options, and AI-assisted itineraries.

Update this README in the same commit whenever a change affects setup, API requests or responses, environment variables, deployment, or milestone status.
