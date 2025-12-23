# Heartbeat Mobile

React Native app for tracking daily activity and streaks.

## Setup

### Prerequisites

- Node.js 20+
- [React Native environment](https://reactnative.dev/docs/set-up-your-environment)
- Server running on `http://localhost:5166`

### Install & Run

```sh
npm install
npm start          # Start Metro bundler

# In another terminal:
npm run android    # or: npm run ios
```

### iOS Additional Setup

```sh
bundle install
bundle exec pod install
```

## API Configuration

| Platform | Default URL |
|----------|-------------|
| Android Emulator | `http://10.0.2.2:5166` |
| iOS Simulator | `http://localhost:5166` |
| Physical Device | Your machine's IP |

Override with `API_BASE_URL` environment variable.

## API Client

Generated from the server's OpenAPI spec.

### Regenerate After Server Changes

```sh
npm run api:regenerate
```

### Usage

```typescript
import { apiClient } from './src/api/client';

// Check server health
const health = await apiClient.checkHealth();

// Register device (creates/updates daily activity, returns unique user code)
const { pairCode: userCode } = await apiClient.register({ deviceId: "..." });
```

### Sync Scripts

```sh
npm run api:check       # Check if client matches server
npm run api:regenerate  # Regenerate client from server
npm run api:sync        # Check and regenerate if needed
```

Generated files output to `contracts/generated/mobile/` (gitignored).
