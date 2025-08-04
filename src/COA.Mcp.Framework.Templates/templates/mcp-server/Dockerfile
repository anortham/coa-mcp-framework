# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["McpServerTemplate.csproj", "."]
RUN dotnet restore "./McpServerTemplate.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "McpServerTemplate.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "McpServerTemplate.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create non-root user
RUN useradd -m -s /bin/bash mcpuser && chown -R mcpuser:mcpuser /app
USER mcpuser

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "McpServerTemplate.dll"]