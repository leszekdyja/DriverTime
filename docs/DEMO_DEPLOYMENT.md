# DriverTime demo deployment

## Requirements

- Docker Engine with Docker Compose v2
- a Linux VPS with ports 80/443 exposed through a reverse proxy
- a domain with an HTTPS certificate for public deployment

## Configuration

1. Copy `.env.example` to `.env`.
2. Replace every `CHANGE_ME` value with a strong, unique secret.
3. Set `PUBLIC_APP_URL` to the public HTTPS URL, for example `https://demo.drivertime.app`.
4. Set `DEMO_DATA_ENABLED=true` only when the environment should contain the sample fleet.
5. Change `DEMO_PASSWORD` before exposing the demo publicly.

The React application uses a relative `/api` URL in Docker. Nginx serves the frontend and proxies API requests to the internal `api` service, so no public API hostname is required.

## Start

```powershell
docker compose --env-file .env up --build -d
```

Open `http://localhost:8080` locally or the configured HTTPS domain behind the VPS reverse proxy.

## Verify

```powershell
docker compose ps
docker compose logs api --tail 100
docker compose exec postgres pg_isready -U drivertime -d drivertime
```

When demo data is enabled, sign in with the values configured in `DEMO_EMAIL` and `DEMO_PASSWORD`.

## Production notes

- Terminate TLS in Caddy, Traefik, Nginx Proxy Manager, or the host Nginx installation.
- Expose the `web` service publicly. The API port can remain bound to localhost or be removed from `docker-compose.yml` when direct access is unnecessary.
- Back up the `drivertime-postgres` volume regularly.
- Keep `.env` outside version control and rotate JWT, database, admin, and demo credentials before each public demo.
- Demo records are idempotent. Restarting the API does not duplicate seeded drivers, imports, or activities.
