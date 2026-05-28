# KOTORModSync Client Integration Guide

## Secure Telemetry with HMAC Signing

This guide shows how to integrate HMAC-signed telemetry in KOTORModSync without exposing secrets in your public GitHub repository.

## Security Architecture

```
KOTORModSync Client
    |
    | 1. Compute HMAC-SHA256 signature
    | 2. Add signature to headers
    |
    v
https://otlp.bolabaden.org (Traefik)
    |
    | 3. ForwardAuth to kotormodsync-auth service
    |
    v
Authentication Service
    |
    | 4. Verify signature & timestamp
    |
    v (if valid)
OpenTelemetry Collector → Prometheus
```

**Key Security Features:**

- ✅ Signing secret NEVER in source code
- ✅ Unsigned requests rejected at the edge
- ✅ Replay attack prevention (timestamp validation)
- ✅ Rate limiting (10 req/s per IP)
- ✅ Graceful degradation (works without secret)
- ✅ Secret rotation supported

## Implementation in KOTORModSync

### 1. Add HMAC Signing Code

**C# Implementation Example:**

```csharp
// File: KOTORModSync/Telemetry/TelemetryAuthenticator.cs

using System;
using System.Security.Cryptography;
using System.Text;

namespace KOTORModSync.Telemetry
{
    /// <summary>
    /// Handles HMAC-SHA256 signing for telemetry requests to bolabaden.org
    /// </summary>
    public class TelemetryAuthenticator
    {
        private readonly string _signingSecret;
        private readonly string _sessionId;

        public TelemetryAuthenticator(string signingSecret, string sessionId)
        {
            _signingSecret = signingSecret;
            _sessionId = sessionId;
        }

        /// <summary>
        /// Computes HMAC-SHA256 signature for a telemetry request
        /// </summary>
        /// <param name="requestPath">Request path (e.g., "/v1/metrics")</param>
        /// <param name="timestamp">Unix timestamp in seconds</param>
        /// <returns>Hex-encoded HMAC-SHA256 signature</returns>
        public string ComputeSignature(string requestPath, long timestamp)
        {
            if (string.IsNullOrEmpty(_signingSecret))
            {
                return null; // No secret available, telemetry will be disabled
            }

            // Message format: "POST|{path}|{timestamp}|{session_id}"
            string message = $"POST|{requestPath}|{timestamp}|{_sessionId}";

            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_signingSecret)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// Gets the current Unix timestamp in seconds
        /// </summary>
        public static long GetUnixTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
```

### 2. Modify OTLP Exporter Configuration

```csharp
// File: KOTORModSync/Telemetry/TelemetryService.cs

using System;
using System.Collections.Generic;
using System.Net.Http;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

namespace KOTORModSync.Telemetry
{
    public class TelemetryService
    {
        private readonly TelemetryAuthenticator _authenticator;
        private readonly string _sessionId;
        private readonly string _otlpEndpoint;
        private TracerProvider _tracerProvider;
        private MeterProvider _meterProvider;

        public TelemetryService()
        {
            // Load signing secret from config or environment
            string signingSecret = LoadSigningSecret();
            
            // Generate session ID (unique per application run)
            _sessionId = Guid.NewGuid().ToString();
            
            // Initialize authenticator
            _authenticator = new TelemetryAuthenticator(signingSecret, _sessionId);
            
            // OTLP endpoint
            _otlpEndpoint = "https://otlp.bolabaden.org";
            
            // Only initialize telemetry if we have a signing secret
            if (!string.IsNullOrEmpty(signingSecret))
            {
                InitializeTelemetry();
            }
            else
            {
                Log.Warning("Telemetry disabled: No signing secret found");
            }
        }

        private string LoadSigningSecret()
        {
            // Priority order:
            // 1. Environment variable (highest priority)
            // 2. Config file in user's AppData (not in repo)
            // 3. Embedded in official builds (GitHub Actions secret)
            
            // Try environment variable first
            string secret = Environment.GetEnvironmentVariable("KOTORMODSYNC_SIGNING_SECRET");
            if (!string.IsNullOrEmpty(secret))
            {
                return secret;
            }

            // Try config file in user's local AppData
            string configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "KOTORModSync",
                "telemetry.key"
            );

            if (File.Exists(configPath))
            {
                try
                {
                    return File.ReadAllText(configPath).Trim();
                }
                catch (Exception ex)
                {
                    Log.Warning($"Could not read telemetry key from {configPath}: {ex.Message}");
                }
            }

            // Try embedded secret (only present in official builds)
            #if OFFICIAL_BUILD
            return EmbeddedSecrets.TELEMETRY_SIGNING_KEY;
            #else
            return null;
            #endif
        }

        private void InitializeTelemetry()
        {
            // Configure resource attributes
            var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService("kotormodsync")
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("session.id", _sessionId),
                    new KeyValuePair<string, object>("app.version", GetAppVersion()),
                    new KeyValuePair<string, object>("platform", GetPlatform())
                });

            // Configure OTLP exporter with custom headers
            var otlpOptions = new OtlpExporterOptions
            {
                Endpoint = new Uri(_otlpEndpoint),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Headers = GetAuthHeaders("/v1/metrics")
            };

            // Initialize tracing
            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = otlpOptions.Endpoint;
                    opt.Protocol = otlpOptions.Protocol;
                    opt.Headers = GetAuthHeaders("/v1/traces");
                })
                .Build();

            // Initialize metrics
            _meterProvider = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddOtlpExporter((exporterOptions, metricReaderOptions) =>
                {
                    exporterOptions.Endpoint = otlpOptions.Endpoint;
                    exporterOptions.Protocol = otlpOptions.Protocol;
                    exporterOptions.Headers = GetAuthHeaders("/v1/metrics");
                })
                .Build();

            Log.Info($"Telemetry enabled: {_otlpEndpoint}");
        }

        /// <summary>
        /// Generates authentication headers for OTLP requests
        /// </summary>
        private string GetAuthHeaders(string requestPath)
        {
            long timestamp = TelemetryAuthenticator.GetUnixTimestamp();
            string signature = _authenticator.ComputeSignature(requestPath, timestamp);

            if (string.IsNullOrEmpty(signature))
            {
                return string.Empty; // No auth headers if no secret
            }

            // Format: "key1=value1,key2=value2"
            return $"X-KMS-Signature={signature}," +
                   $"X-KMS-Timestamp={timestamp}," +
                   $"X-KMS-Session-ID={_sessionId}," +
                   $"X-KMS-Client-Version={GetAppVersion()}";
        }

        private string GetAppVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version.ToString();
        }

        private string GetPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "OSX";
            return "Unknown";
        }

        public void Dispose()
        {
            _tracerProvider?.Dispose();
            _meterProvider?.Dispose();
        }
    }
}
```

### 3. Handle Secret in Source Control

**Add to `.gitignore`:**

```gitignore
# Telemetry signing secrets (NEVER commit these!)
**/telemetry.key
**/EmbeddedSecrets.cs
src/KOTORModSync/Telemetry/EmbeddedSecrets.cs
```

**Create placeholder file (committed to repo):**

```csharp
// File: KOTORModSync/Telemetry/EmbeddedSecrets.cs.example
// 
// This is a TEMPLATE file. The actual EmbeddedSecrets.cs is generated
// during official builds via GitHub Actions and is NOT committed to git.
//
// For local development:
// 1. Copy this file to EmbeddedSecrets.cs
// 2. Contact the maintainer for a development signing key
// 3. Telemetry will gracefully fail if no key is present

namespace KOTORModSync.Telemetry
{
    internal static class EmbeddedSecrets
    {
        // This constant is replaced during official builds
        internal const string TELEMETRY_SIGNING_KEY = "";
    }
}
```

### 4. GitHub Actions CI/CD Integration

**`.github/workflows/build.yml`:**

```yaml
name: Build KOTORModSync

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  release:
    types: [published]

env:
  DOTNET_VERSION: '6.0.x'

jobs:
  build:
    runs-on: windows-latest
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      
      - name: Inject Telemetry Secret (Official Builds Only)
        if: github.event_name == 'release' || github.ref == 'refs/heads/main'
        env:
          TELEMETRY_SECRET: ${{ secrets.KOTORMODSYNC_SIGNING_SECRET }}
        shell: pwsh
        run: |
          $secretFile = "src/KOTORModSync/Telemetry/EmbeddedSecrets.cs"
          $content = @"
          // AUTO-GENERATED - DO NOT COMMIT
          namespace KOTORModSync.Telemetry
          {
              internal static class EmbeddedSecrets
              {
                  internal const string TELEMETRY_SIGNING_KEY = "$env:TELEMETRY_SECRET";
              }
          }
          "@
          $content | Out-File -FilePath $secretFile -Encoding UTF8
          Write-Host "Telemetry secret injected for official build"
      
      - name: Build (Official)
        if: github.event_name == 'release' || github.ref == 'refs/heads/main'
        run: dotnet build -c Release /p:DefineConstants="OFFICIAL_BUILD"
      
      - name: Build (Development)
        if: github.event_name != 'release' && github.ref != 'refs/heads/main'
        run: dotnet build -c Release
      
      - name: Run Tests
        run: dotnet test -c Release --no-build
      
      - name: Publish
        if: github.event_name == 'release'
        run: dotnet publish -c Release -o ./publish
      
      - name: Upload Artifact
        if: github.event_name == 'release'
        uses: actions/upload-artifact@v3
        with:
          name: KOTORModSync-Release
          path: ./publish
```

**Set GitHub Secret:**

1. Go to your repo: Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Name: `KOTORMODSYNC_SIGNING_SECRET`
4. Value: (paste the signing secret from bolabaden.org)
5. Click "Add secret"

## Secret Management

### On bolabaden.org Server

**Generate a new signing secret:**

```bash
# Generate a cryptographically secure random secret (64 characters)
openssl rand -hex 32 > /home/ubuntu/my-media-stack/volumes/kotormodsync_signing_secret.txt

# Set permissions
chmod 600 /home/ubuntu/my-media-stack/volumes/kotormodsync_signing_secret.txt

# View the secret (copy this for GitHub Actions)
cat /home/ubuntu/my-media-stack/volumes/kotormodsync_signing_secret.txt
```

### For Local Development (Optional)

Developers who want telemetry in their local builds:

**Option 1: Environment Variable**

```bash
# Windows (PowerShell)
$env:KOTORMODSYNC_SIGNING_SECRET = "dev-secret-key-here"

# Linux/Mac
export KOTORMODSYNC_SIGNING_SECRET="dev-secret-key-here"
```

**Option 2: Config File**

```bash
# Windows
mkdir "$env:APPDATA\KOTORModSync"
echo "dev-secret-key-here" > "$env:APPDATA\KOTORModSync\telemetry.key"

# Linux/Mac
mkdir -p ~/.config/kotormodsync
echo "dev-secret-key-here" > ~/.config/kotormodsync/telemetry.key
```

**Recommendation:** Use a **different** dev secret for local development, not the production secret. This way, you can filter dev telemetry in Grafana.

## Testing

### Test with Valid Signature

```bash
# Compute signature (example in bash)
SIGNING_SECRET="your-secret-here"
SESSION_ID="test-session-123"
TIMESTAMP=$(date +%s)
PATH="/v1/metrics"
MESSAGE="POST|${PATH}|${TIMESTAMP}|${SESSION_ID}"

SIGNATURE=$(echo -n "$MESSAGE" | openssl dgst -sha256 -hmac "$SIGNING_SECRET" | cut -d' ' -f2)

# Send request
curl -X POST https://otlp.bolabaden.org/v1/metrics \
  -H "Content-Type: application/json" \
  -H "X-KMS-Signature: $SIGNATURE" \
  -H "X-KMS-Timestamp: $TIMESTAMP" \
  -H "X-KMS-Session-ID: $SESSION_ID" \
  -H "X-KMS-Client-Version: test-1.0.0" \
  -d '{"resourceMetrics":[]}'

# Expected: HTTP 200
```

### Test without Signature

```bash
curl -X POST https://otlp.bolabaden.org/v1/metrics \
  -H "Content-Type: application/json" \
  -d '{"resourceMetrics":[]}'

# Expected: HTTP 401 Unauthorized
```

### Test with Invalid Signature

```bash
curl -X POST https://otlp.bolabaden.org/v1/metrics \
  -H "Content-Type: application/json" \
  -H "X-KMS-Signature: invalid-signature" \
  -H "X-KMS-Timestamp: $(date +%s)" \
  -H "X-KMS-Session-ID: test-session" \
  -d '{"resourceMetrics":[]}'

# Expected: HTTP 403 Forbidden
```

## Secret Rotation

If the secret is compromised or you want to rotate it:

1. **Generate new secret on server:**

   ```bash
   openssl rand -hex 32 > /home/ubuntu/my-media-stack/volumes/kotormodsync_signing_secret.txt
   ```

2. **Update GitHub Actions secret:**
   - Go to repo Settings → Secrets → Edit `KOTORMODSYNC_SIGNING_SECRET`
   - Paste new secret

3. **Restart auth service:**

   ```bash
   docker compose restart kotormodsync-auth
   ```

4. **Publish new KOTORModSync release:**
   - Old versions will stop sending telemetry (expected)
   - New versions will use new secret

5. **Grace period (optional):**
   - Temporarily accept both old and new secrets
   - Modify auth service to check multiple secrets
   - Remove old secret after 30 days

## Troubleshooting

### Issue: "Missing authentication headers"

**Cause:** Client not sending required headers.

**Fix:** Ensure `X-KMS-Signature` and `X-KMS-Timestamp` headers are present.

### Issue: "Invalid signature"

**Cause:** Signature computation mismatch.

**Fix:**

1. Verify message format: `POST|{path}|{timestamp}|{session_id}`
2. Check secret matches server
3. Ensure timestamp is current Unix seconds (not milliseconds)

### Issue: "Request timestamp too old or in future"

**Cause:** System clock skew > 5 minutes.

**Fix:**

1. Sync system clock: `w32tm /resync` (Windows) or `ntpdate` (Linux)
2. Check user's timezone settings
3. Increase `MAX_TIMESTAMP_DRIFT` on server (not recommended)

### Issue: Rate limit (HTTP 429)

**Cause:** Sending > 20 requests per second.

**Fix:**

1. Add backoff/retry logic
2. Batch metrics more aggressively
3. Contact admin to increase rate limit

## Benefits of This Approach

✅ **No secrets in source code** - Public repo stays clean
✅ **Works without secret** - Dev builds still compile and run
✅ **Prevents abuse** - Unsigned requests rejected
✅ **Replay protection** - Timestamp validation
✅ **Easy rotation** - No code changes needed
✅ **CI/CD friendly** - Automated injection
✅ **Graceful degradation** - Telemetry disabled != broken app

## Questions?

- **Server setup:** See `docs/KOTORMODSYNC_TELEMETRY_SETUP.md`
- **Quick start:** See `docs/OTLP_QUICKSTART.md`
- **Auth service logs:** `docker compose logs -f kotormodsync-auth`
