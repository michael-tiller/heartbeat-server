# CI/CD

GitHub Actions pipeline for build, test, and container publishing.

---

## What's Automated

- **Build + Test** — Runs on every push and PR to `main` or `dev`
- **Docker Image** — Built and pushed to `ghcr.io` on push (not PRs)
- **Tagging** — Branch name, SHA, and `latest` for default branch

Images available at:
```
ghcr.io/<username>/heartbeat-server:<tag>
```

---

## Triggers

| Event | Build | Test | Docker Push |
|-------|-------|------|-------------|
| Push to `main` | ✓ | ✓ | ✓ |
| Push to `dev` | ✓ | ✓ | ✓ |
| PR to `main`/`dev` | ✓ | ✓ | ✗ |

---

## Local Docker Build

```bash
docker build -f server/Dockerfile -t heartbeat-server:local .
docker run -p 5166:5166 heartbeat-server:local
curl http://localhost:5166/health
```

---

## Deployment

The `deploy-staging` and `deploy-production` jobs are **placeholders**.

To enable:
1. Set up your infrastructure (k8s, Cloud Run, ECS, etc.)
2. Add secrets in `Settings > Secrets and variables > Actions`
3. Replace placeholder commands in `.github/workflows/ci-cd.yml`

Production uses GitHub Environments for approval gates (`Settings > Environments`).
