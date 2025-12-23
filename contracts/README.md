# Contracts

API contracts and generated TypeScript client.

## Structure

```
contracts/
├── dotnet/           # .NET contract definitions (DTOs)
├── spec/             # OpenAPI specs (for offline generation)
└── generated/        # Generated clients (gitignored)
    └── mobile/       # TypeScript fetch client
```

## Usage

```typescript
import { apiClient } from './src/api/client';

const health = await apiClient.checkHealth();
const { pairCode } = await apiClient.register({ deviceId: '...' });
```

## Regenerate Client

```bash
cd mobile
npm run api:regenerate
```

Requires server running at `http://localhost:5166`.
