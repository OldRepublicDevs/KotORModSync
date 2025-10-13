# KOTORModSync Secure Telemetry Setup Guide

## Overview

This guide explains how KOTORModSync implements secure telemetry using HMAC-SHA256 signing to prevent unauthorized metric submissions while keeping the source code public.

## Security Architecture

```
KOTORModSync Client (your machine)
    |
    | 1. Compute HMAC-SHA256 signature
    | 2. Add signature to OTLP request headers
    |
    v
https://otlp.bolabaden.org (Traefik)
    |
    | 3. ForwardAuth to kotormodsync-auth service
    |
    v
Authentication Service (validates signature)
    |
    | 4. Verify signature & timestamp
    | 5. Check replay protection
    |
    v (if valid)
OpenTelemetry Collector → Prometheus
```

**Key Security Features:**

- ✅ **No secrets in source code** - Signing key never committed to git
- ✅ **Unsigned requests rejected** - HMAC validation prevents fake metrics
- ✅ **Replay attack prevention** - Timestamp validation (5 minute window)
- ✅ **Rate limiting** - 10 req/s per IP address
- ✅ **Graceful degradation** - App works without telemetry secret
- ✅ **Secret rotation supported** - No code changes needed

## How It Works

### 1. Secret Loading (Priority Order)

KOTORModSync loads the signing secret from three sources:

1. **Environment Variable** (highest priority)
   - Variable: `KOTORMODSYNC_SIGNING_SECRET`
   - Use case: CI/CD pipelines, developer testing

2. **Local Config File**
   - Windows: `%AppData%\KOTORModSync\telemetry.key`
   - Linux/Mac: `~/.config/kotormodsync/telemetry.key`
   - Use case: Developer local testing

3. **Embedded Secret** (only in official builds)
   - File: `KOTORModSync.Core/Services/EmbeddedSecrets.cs`
   - Generated during GitHub Actions builds
   - Use case: Official releases distributed to users

### 2. HMAC-SHA256 Signing

Each telemetry request is signed with:

```
Message: "POST|{path}|{timestamp}|{session_id}"
Signature: HMAC-SHA256(secret, message)
```

Example:
```
Message: "POST|/v1/metrics|1729000000|abc-123-def"
Signature: 6ea4413f4db73407b07c3faccac817031f5210f80bde02e94b61c512de6b9d90
```

### 3. HTTP Headers

The following headers are added to every OTLP request:

- `X-KMS-Signature` - HMAC-SHA256 signature (hex-encoded)
- `X-KMS-Timestamp` - Unix timestamp in seconds
- `X-KMS-Session-ID` - Unique session ID (changes per app run)
- `X-KMS-Client-Version` - KOTORModSync version

## Setup Instructions

### For Users (No Setup Required)

Official releases of KOTORModSync have the signing secret embedded automatically. Users don't need to do anything.

### For Developers (Local Development)

Developers can build and run KOTORModSync **without any telemetry setup**. The app will:

- Build successfully without errors
- Run normally with all features working
- Log a warning: "No signing secret found - telemetry will be disabled"
- Continue functioning without sending telemetry

**To test telemetry locally** (optional):

#### Option 1: Environment Variable

**Windows (PowerShell):**
```powershell
$env:KOTORMODSYNC_SIGNING_SECRET = "dev-secret-key-here"
```

**Linux/Mac:**
```bash
export KOTORMODSYNC_SIGNING_SECRET="dev-secret-key-here"
```

#### Option 2: Config File

**Windows:**
```powershell
mkdir "$env:APPDATA\KOTORModSync"
echo "dev-secret-key-here" > "$env:APPDATA\KOTORModSync\telemetry.key"
```

**Linux/Mac:**
```bash
mkdir -p ~/.config/kotormodsync
echo "dev-secret-key-here" > ~/.config/kotormodsync/telemetry.key
```

**Important:** Use a **different** dev secret, not the production secret. This allows filtering dev telemetry in Grafana.

### For Repository Maintainers (GitHub Actions)

#### Step 1: Set Up GitHub Secret

1. Go to your repository: **Settings** → **Secrets and variables** → **Actions**
2. Click **"New repository secret"**
3. Name: `KOTORMODSYNC_SIGNING_SECRET`
4. Value: (paste the signing secret from bolabaden.org)
5. Click **"Add secret"**

The signing secret from bolabaden.org is:
```
6ea4413f4db73407b07c3faccac817031f5210f80bde02e94b61c512de6b9d90
```

#### Step 2: Update GitHub Actions Workflow

Create or update `.github/workflows/build.yml`:

```yaml
name: Build KOTORModSync

on:
  push:
    branches: [main, master]
  pull_request:
    branches: [main, master]
  release:
    types: [published]

env:
  DOTNET_VERSION: '8.0.x'  # Adjust to your .NET version

jobs:
  build:
    runs-on: windows-latest
    
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      
      # Inject telemetry secret ONLY for official builds
      - name: Inject Telemetry Secret (Official Builds Only)
        if: github.event_name == 'release' || (github.event_name == 'push' && github.ref == 'refs/heads/main')
        env:
          TELEMETRY_SECRET: ${{ secrets.KOTORMODSYNC_SIGNING_SECRET }}
        shell: pwsh
        run: |
          $secretFile = "KOTORModSync.Core/Services/EmbeddedSecrets.cs"
          $content = @"
          // Copyright 2021-2025 KOTORModSync
          // Licensed under the Business Source License 1.1 (BSL 1.1).
          // See LICENSE.txt file in the project root for full license information.
          
          // AUTO-GENERATED FILE - DO NOT COMMIT
          // This file is generated during GitHub Actions builds only.
          
          namespace KOTORModSync.Core.Services
          {
              internal static class EmbeddedSecrets
              {
                  internal const string TELEMETRY_SIGNING_KEY = "$env:TELEMETRY_SECRET";
              }
          }
          "@
          $content | Out-File -FilePath $secretFile -Encoding UTF8
          Write-Host "✅ Telemetry secret injected for official build"
      
      # Build with OFFICIAL_BUILD flag for releases
      - name: Build (Official Release)
        if: github.event_name == 'release' || (github.event_name == 'push' && github.ref == 'refs/heads/main')
        run: dotnet build -c Release /p:DefineConstants="OFFICIAL_BUILD"
      
      # Build without OFFICIAL_BUILD flag for PRs and dev branches
      - name: Build (Development)
        if: github.event_name == 'pull_request' || (github.event_name == 'push' && github.ref != 'refs/heads/main')
        run: dotnet build -c Release
      
      - name: Run Tests
        run: dotnet test -c Release --no-build
      
      - name: Publish (Release Only)
        if: github.event_name == 'release'
        run: dotnet publish -c Release -o ./publish
      
      - name: Upload Artifact
        if: github.event_name == 'release'
        uses: actions/upload-artifact@v4
        with:
          name: KOTORModSync-Release
          path: ./publish
```

**Key Points:**

- Secret injection happens **only** for official builds (releases and main branch pushes)
- Pull requests and development branches build **without** the secret
- The `OFFICIAL_BUILD` preprocessor directive controls secret embedding
- `EmbeddedSecrets.cs` is auto-generated and never committed to git

## Testing

### Test Local Build Without Secret

```bash
# Clone and build
git clone https://github.com/your-username/KOTORModSync.git
cd KOTORModSync
dotnet build

# Run - should see warning but work fine
dotnet run --project KOTORModSync.GUI
```

Expected log output:
```
[Telemetry] No signing secret found - telemetry will be disabled
```

### Test With Valid Secret

**Windows:**
```powershell
$env:KOTORMODSYNC_SIGNING_SECRET = "6ea4413f4db73407b07c3faccac817031f5210f80bde02e94b61c512de6b9d90"
dotnet run --project KOTORModSync.GUI
```

Expected log output:
```
[Telemetry] Signing secret loaded from environment variable
[Telemetry] Telemetry service initialized successfully
```

### Test Authentication (Manual cURL)

```bash
# Compute signature (bash example)
SIGNING_SECRET="6ea4413f4db73407b07c3faccac817031f5210f80bde02e94b61c512de6b9d90"
SESSION_ID="test-session-123"
TIMESTAMP=$(date +%s)
PATH="/v1/metrics"
MESSAGE="POST|${PATH}|${TIMESTAMP}|${SESSION_ID}"

SIGNATURE=$(echo -n "$MESSAGE" | openssl dgst -sha256 -hmac "$SIGNING_SECRET" | cut -d' ' -f2)

# Send authenticated request
curl -X POST https://otlp.bolabaden.org/v1/metrics \
  -H "Content-Type: application/json" \
  -H "X-KMS-Signature: $SIGNATURE" \
  -H "X-KMS-Timestamp: $TIMESTAMP" \
  -H "X-KMS-Session-ID: $SESSION_ID" \
  -H "X-KMS-Client-Version: 1.0.0" \
  -d '{"resourceMetrics":[]}'

# Expected: HTTP 200 OK
```

## Security Best Practices

### ✅ DO:

- Use GitHub Actions secrets for production key
- Use different keys for development and production
- Rotate keys every 90 days
- Monitor authentication failures in Grafana
- Keep `.gitignore` updated to exclude secret files

### ❌ DON'T:

- Never commit `EmbeddedSecrets.cs` to git
- Never commit `telemetry.key` files
- Never share the production secret publicly
- Never hardcode secrets in source files
- Don't bypass signature validation for testing

## Secret Rotation

If the secret is compromised or you want to rotate it:

### Step 1: Generate New Secret on Server

```bash
ssh user@bolabaden.org
cd /home/ubuntu/my-media-stack
openssl rand -hex 32 > volumes/kotormodsync_signing_secret.txt
chmod 600 volumes/kotormodsync_signing_secret.txt
cat volumes/kotormodsync_signing_secret.txt
```

### Step 2: Update GitHub Actions Secret

1. Go to repo **Settings** → **Secrets** → **Actions**
2. Edit `KOTORMODSYNC_SIGNING_SECRET`
3. Paste new secret
4. Save

### Step 3: Restart Auth Service

```bash
ssh user@bolabaden.org
cd /home/ubuntu/my-media-stack
docker compose restart kotormodsync-auth
```

### Step 4: Publish New Release

1. Create new release on GitHub
2. GitHub Actions will automatically build with new secret
3. Old versions will stop sending telemetry (expected behavior)
4. New versions will use new secret

### Optional: Grace Period

To support both old and new secrets temporarily:

1. Modify `kotormodsync-auth` service to accept multiple secrets
2. Keep both secrets active for 30 days
3. Remove old secret after users have upgraded

## Troubleshooting

### Issue: "Missing authentication headers"

**Cause:** Client not sending required headers.

**Fix:** Ensure `X-KMS-Signature` and `X-KMS-Timestamp` headers are present.

### Issue: "Invalid signature"

**Cause:** Signature computation mismatch.

**Fix:**
1. Verify message format: `POST|{path}|{timestamp}|{session_id}`
2. Check secret matches server
3. Ensure timestamp is Unix seconds (not milliseconds)

### Issue: "Request timestamp too old or in future"

**Cause:** System clock skew > 5 minutes.

**Fix:**
1. Sync system clock:
   - Windows: `w32tm /resync`
   - Linux: `sudo ntpdate -s time.nist.gov`
2. Check timezone settings
3. Contact admin to increase `MAX_TIMESTAMP_DRIFT` on server (not recommended)

### Issue: Build fails with "EmbeddedSecrets not found"

**Cause:** Trying to build with `OFFICIAL_BUILD` flag without generating `EmbeddedSecrets.cs`.

**Fix:**
- Remove `/p:DefineConstants="OFFICIAL_BUILD"` from build command
- OR generate `EmbeddedSecrets.cs` manually (for testing only)

### Issue: Rate limit (HTTP 429)

**Cause:** Sending > 10 requests per second.

**Fix:**
1. Add backoff/retry logic in code
2. Batch metrics more aggressively
3. Contact admin to increase rate limit

## Implementation Details

### Files Created

1. **`KOTORModSync.Core/Services/TelemetryAuthenticator.cs`**
   - HMAC-SHA256 signing implementation
   - Timestamp generation
   - Signature validation

2. **`KOTORModSync.Core/Services/EmbeddedSecrets.cs.example`**
   - Template file (committed to git)
   - Shows structure of auto-generated file
   - Contains usage instructions

### Files Modified

1. **`KOTORModSync.Core/Services/TelemetryConfiguration.cs`**
   - Added `LoadSigningSecret()` method
   - Loads from env var, config file, or embedded secret
   - Added `SigningSecret` property

2. **`KOTORModSync.Core/Services/TelemetryService.cs`**
   - Added `TelemetryAuthenticator` field
   - Added `GetAuthHeaders()` method
   - Modified OTLP exporter to include auth headers

3. **`.gitignore`**
   - Added `**/telemetry.key`
   - Added `**/EmbeddedSecrets.cs`

## Benefits of This Approach

✅ **No secrets in source code** - Public repo stays clean and secure
✅ **Works without secret** - Developers can build without any setup
✅ **Prevents abuse** - Unsigned requests are rejected at the edge
✅ **Replay protection** - Timestamp validation prevents replay attacks
✅ **Easy rotation** - No code changes needed to rotate secrets
✅ **CI/CD friendly** - Automated injection via GitHub Actions
✅ **Graceful degradation** - Telemetry disabled ≠ broken app

## Questions?

- **Server setup:** See `BOLABADEN_TELEMETRY_SETUP.md`
- **Client integration:** See `KOTORModSync_Client_Integration_Guide.md`
- **Quick start:** See OTLP setup guide
- **Auth service logs:** `docker compose logs -f kotormodsync-auth`

## License

This telemetry implementation is part of KOTORModSync and is licensed under the Business Source License 1.1 (BSL 1.1).

