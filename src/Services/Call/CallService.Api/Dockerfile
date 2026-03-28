FROM mcr.microsoft.com/dotnet/aspnet:10.0.4 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.104 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["NuGet.Config", "."]
COPY ["src/BuildingBlocks/Auth/Auth.csproj", "src/BuildingBlocks/Auth/"]
COPY ["src/BuildingBlocks/Contracts/Contracts.csproj", "src/BuildingBlocks/Contracts/"]
COPY ["src/BuildingBlocks/Idempotency/Idempotency.csproj", "src/BuildingBlocks/Idempotency/"]
COPY ["src/BuildingBlocks/Observability/Observability.csproj", "src/BuildingBlocks/Observability/"]
COPY ["src/BuildingBlocks/Outbox/Outbox.csproj", "src/BuildingBlocks/Outbox/"]
COPY ["src/BuildingBlocks/ServiceDefaults/ServiceDefaults.csproj", "src/BuildingBlocks/ServiceDefaults/"]
COPY ["src/Services/Call/CallService.Api/CallService.Api.csproj", "src/Services/Call/CallService.Api/"]
RUN dotnet restore "src/Services/Call/CallService.Api/CallService.Api.csproj"
COPY . .
RUN dotnet publish "src/Services/Call/CallService.Api/CallService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "CallService.Api.dll"]
