# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy global.json and project files for dependency restoration
COPY global.json .
COPY domain/Heartbeat.Domain.csproj ./domain/
COPY contracts/dotnet/Heartbeat.Contracts.csproj ./contracts/dotnet/
COPY server/Heartbeat.Server.csproj ./server/

# Restore dependencies (restoring server will automatically restore domain and contracts)
# This layer will be cached if project files don't change
RUN dotnet restore server/Heartbeat.Server.csproj

# Copy all source files
COPY server/src ./server/src
COPY domain/src ./domain/src
COPY contracts/dotnet/src ./contracts/dotnet/src

# Build the application
WORKDIR /src/server
RUN dotnet build -c Release --no-restore -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release --no-restore -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && \
  apt-get install -y --no-install-recommends curl && \
  rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published application
COPY --from=publish /app/publish .

# Create logs directory with proper permissions
RUN mkdir -p logs && chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 5166

# Set environment variables
ENV ASPNETCORE_URLS=http://0.0.0.0:5166
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:5166/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Heartbeat.Server.dll"]

