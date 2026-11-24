FROM node:24-bookworm-slim AS frontend
WORKDIR /src
COPY frontend/package*.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/TravelPlanner.Api.csproj ./
RUN dotnet restore
COPY backend/ ./
COPY --from=frontend /src/dist/travel-planner/browser/ ./wwwroot/
RUN dotnet publish --no-restore -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TravelPlanner.Api.dll"]
