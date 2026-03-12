FROM mcr.microsoft.com/dotnet/aspnet:10.0.3 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:10.0.103 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["NuGet.Config", "."]
COPY ["src/BuildingBlocks/Auth/Auth.csproj", "src/BuildingBlocks/Auth/"]
COPY ["src/BuildingBlocks/Contracts/Contracts.csproj", "src/BuildingBlocks/Contracts/"]
COPY ["src/BuildingBlocks/Observability/Observability.csproj", "src/BuildingBlocks/Observability/"]
COPY ["src/Gateway/ApiGateway/ApiGateway.csproj", "src/Gateway/ApiGateway/"]
RUN dotnet restore "src/Gateway/ApiGateway/ApiGateway.csproj"
COPY . .
RUN dotnet publish "src/Gateway/ApiGateway/ApiGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
