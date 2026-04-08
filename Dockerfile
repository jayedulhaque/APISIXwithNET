# syntax=docker/dockerfile:1
# SDK and runtime use 9.x: the .NET 8 images hit SIGBUS (exit 135) on some Docker Desktop/WSL2 hosts.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TaskApi/TaskApi.csproj TaskApi/
RUN dotnet restore TaskApi/TaskApi.csproj

COPY TaskApi/ TaskApi/
WORKDIR /src/TaskApi
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskApi.dll"]
