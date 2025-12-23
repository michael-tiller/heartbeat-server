# Docker Setup for Heartbeat Server

This directory contains Docker configuration for containerizing and running the Heartbeat server.

## Files

- `Dockerfile` - Multi-stage build for production-ready container
- `docker-compose.yml` - Local development setup with PostgreSQL
- `.dockerignore` - Optimizes build context by excluding unnecessary files

## Quick Start

### Prerequisites

- Docker Desktop (Windows/Mac) or Docker Engine + Docker Compose (Linux)
- .NET SDK 10.0 (for local development)

### Running with Docker Compose

1. **Start the services** (PostgreSQL + Server):
   ```bash
   cd server
   docker-compose up -d
   ```

2. **View logs**:
   ```bash
   docker-compose logs -f server
   ```

3. **Stop the services**:
   ```bash
   docker-compose down
   ```

4. **Stop and remove volumes** (cleans database):
   ```bash
   docker-compose down -v
   ```

### Building the Docker Image

```bash
# From the server directory
docker build -f Dockerfile -t heartbeat-server:latest ..

# Or from the root directory
docker build -f server/Dockerfile -t heartbeat-server:latest .
```

### Running the Container Manually

```bash
docker run -d \
  --name heartbeat-server \
  -p 5166:5166 \
  -e ConnectionStrings__DefaultConnection="Host=your-postgres-host;Port=5432;Database=heartbeat;Username=user;Password=pass" \
  heartbeat-server:latest
```

## Database Configuration

The server supports two database providers:

1. **PostgreSQL** (default in Docker) - Set via `ConnectionStrings__DefaultConnection`
2. **SQLite** (fallback for local dev) - Used when no connection string is provided

### PostgreSQL Connection String Format

```
Host=postgres;Port=5432;Database=heartbeat;Username=heartbeat_user;Password=heartbeat_password
```

## Environment Variables

- `ASPNETCORE_ENVIRONMENT` - Environment name (Development, Production, etc.)
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `ASPNETCORE_URLS` - Server URLs (default: `http://0.0.0.0:5166`)

## Docker Compose Services

### `postgres`

- PostgreSQL 16 Alpine image
- Port: `5432`
- Database: `heartbeat`
- User: `heartbeat_user`
- Password: `heartbeat_password`
- Data persisted in `postgres_data` volume

### `server`

- Built from `Dockerfile`
- Port: `5166`
- Depends on `postgres` service
- Logs mounted to `./logs` directory

## Development Workflow

For local development with hot reload, run the server directly:

```bash
cd server
dotnet watch run
```

Use Docker Compose when you need:

- PostgreSQL database
- Production-like environment
- Isolated containerized setup

## Production Considerations

1. **Security**: Update default PostgreSQL credentials
2. **Secrets**: Use Docker secrets or environment variable files (not committed)
3. **Health Checks**: Container includes health check endpoint at `/health`
4. **Logging**: Logs are written to `/app/logs` in the container
5. **Non-root User**: Container runs as non-root user `appuser`

## Troubleshooting

### Docker Desktop Not Running

If you see an error like `unable to get image` or `The system cannot find the file specified`:

1. **Start Docker Desktop** - Make sure Docker Desktop is running on Windows
2. **Wait for Docker to be ready** - Wait until Docker Desktop shows "Docker Desktop is running"
3. **Verify Docker is accessible**:
   ```bash
   docker ps
   ```
   This should return a list of containers (or an empty list), not an error

### Database Connection Issues

Check PostgreSQL is healthy:

```bash
docker-compose ps
docker-compose logs postgres
```

### Port Conflicts

If port 5166 is already in use, modify `docker-compose.yml`:

```yaml
ports:
  - "5167:5166"  # Map host port 5167 to container port 5166
```

### Rebuild After Code Changes

```bash
docker-compose build server
docker-compose up -d server
```

