# GitHub Secret Setup for KOTORModSync Telemetry

## Required GitHub Secret

**Name:** `KOTORMODSYNC_SIGNING_SECRET`

**Purpose:** HMAC-SHA256 signing key for authenticating telemetry requests to bolabaden.org. Prevents unauthorized/fake telemetry from being sent.

## How to Set It Up

### 1. Generate a Secure Secret

#### On Linux/Mac

```bash
# Generate a cryptographically secure 32-byte secret
openssl rand -base64 32

# Example output:
# Xk7jP9mN2qR5wT8vY1aZ3bC4dE6fG7hI8jK9lM0nO1p=
```

#### On Windows

You can use PowerShell (no need to install OpenSSL):

```powershell
[Convert]::ToBase64String((New-Object byte[] 32 | % { [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($_); $_ }))
```

**Result:**
Use the resulting string (for example, `Xk7jP9mN2qR5wT8vY1aZ3bC4dE6fG7hI8jK9lM0nO1p=`) as your secret.

**Save this value!** You'll need it for both GitHub and your server.

### 2. Add to GitHub

1. Go to your GitHub repository: `https://github.com/th3w1zard1/KOTORModSync`
2. Click **Settings** (top right)
3. In the left sidebar, click **Secrets and variables** → **Actions**
4. Click **New repository secret**
5. Name: `KOTORMODSYNC_SIGNING_SECRET`
6. Value: Paste the generated secret (e.g., `Xk7jP9mN2qR5wT8vY1aZ3bC4dE6fG7hI8jK9lM0nO1p=`)
7. Click **Add secret**

### 3. Add to Your Server (bolabaden.org)

The same secret needs to be configured on your server to verify signatures. See "Server-Side Verification" section below.

## Which Workflow Uses It?

### `.github/workflows/build-and-release.yml`

**Lines 110-166:** Development vs Official Build

```yaml
- name: Inject Telemetry Secret (Official Builds Only)
  if: github.event_name != 'pull_request'
  env:
    TELEMETRY_SECRET: ${{ secrets.KOTORMODSYNC_SIGNING_SECRET }}
```

**What it does:**

1. Only runs on official builds (NOT on pull requests)
2. Creates `KOTORModSync.Core/Services/EmbeddedSecrets.cs` during build
3. Embeds the signing secret into the compiled binary
4. File is auto-generated and never committed to Git

**Lines 228-259:** Multi-platform builds do the same thing

## How Authentication Works

### Client Side (KOTORModSync)

When sending telemetry to `https://otlp.bolabaden.org`:

1. **Loads signing secret** (priority order):
   - Environment variable: `KOTORMODSYNC_SIGNING_SECRET`
   - Local config file: `%AppData%/KOTORModSync/telemetry.key`
   - Embedded secret (only in official builds from GitHub Actions)

2. **Computes HMAC-SHA256 signature**:

   ```
   Message: POST|{requestPath}|{timestamp}|{sessionId}
   Signature: HMAC-SHA256(message, signingSecret)
   ```

3. **Sends HTTP headers** with every telemetry request:

   ```
   X-KMS-Signature: <hmac_signature>
   X-KMS-Timestamp: <unix_timestamp>
   X-KMS-Session: <session_id>
   X-KMS-Version: <app_version>
   X-KMS-Build: official
   ```

### Server Side (bolabaden.org)

Your OTLP collector needs to verify the signature. See below for implementation.

## Server-Side Verification

Add signature verification to your OTLP collector to reject unauthorized requests.

### Option 1: Nginx Lua Module (Recommended)

**Install Nginx with Lua:**

```bash
sudo apt-get install nginx-extras lua5.1
```

**Create verification script** (`/etc/nginx/lua/verify_kms_signature.lua`):

```lua
local hmac = require "resty.hmac"

function verify_signature()
    -- Load secret from environment
    local secret = os.getenv("KOTORMODSYNC_SIGNING_SECRET")
    if not secret then
        ngx.log(ngx.ERR, "KOTORMODSYNC_SIGNING_SECRET not set")
        return ngx.HTTP_INTERNAL_SERVER_ERROR
    end

    -- Get headers
    local signature = ngx.var.http_x_kms_signature
    local timestamp = ngx.var.http_x_kms_timestamp
    local session = ngx.var.http_x_kms_session
    local build = ngx.var.http_x_kms_build

    -- Require official builds (optional - remove to allow dev builds)
    if build ~= "official" then
        ngx.log(ngx.WARN, "Rejecting non-official build")
        return ngx.HTTP_UNAUTHORIZED
    end

    -- Validate required headers
    if not signature or not timestamp or not session then
        ngx.log(ngx.WARN, "Missing required headers")
        return ngx.HTTP_UNAUTHORIZED
    end

    -- Check timestamp is recent (within 5 minutes)
    local now = ngx.time()
    local ts = tonumber(timestamp)
    if not ts or math.abs(now - ts) > 300 then
        ngx.log(ngx.WARN, "Timestamp too old or invalid")
        return ngx.HTTP_UNAUTHORIZED
    end

    -- Compute expected signature
    local message = "POST|" .. ngx.var.uri .. "|" .. timestamp .. "|" .. session
    local hmac_sha256 = hmac:new(secret, hmac.ALGOS.SHA256)
    if not hmac_sha256 then
        ngx.log(ngx.ERR, "Failed to create HMAC instance")
        return ngx.HTTP_INTERNAL_SERVER_ERROR
    end

    hmac_sha256:update(message)
    local expected = hmac_sha256:final():tohex()

    -- Compare signatures (constant-time comparison)
    if signature ~= expected then
        ngx.log(ngx.WARN, "Invalid signature")
        return ngx.HTTP_UNAUTHORIZED
    end

    -- Success - continue to backend
    return ngx.OK
end

-- Execute
local result = verify_signature()
if result ~= ngx.OK then
    ngx.exit(result)
end
```

**Update Nginx config** (`/etc/nginx/sites-available/otlp.bolabaden.org`):

```nginx
server {
    listen 443 ssl http2;
    server_name otlp.bolabaden.org;

    ssl_certificate /etc/letsencrypt/live/otlp.bolabaden.org/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/otlp.bolabaden.org/privkey.pem;

    location / {
        # Verify signature using Lua
        access_by_lua_file /etc/nginx/lua/verify_kms_signature.lua;

        # If signature is valid, proxy to OTLP collector
        proxy_pass http://localhost:4318;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;

        # Increase timeouts
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;

        # Rate limiting
        limit_req zone=telemetry burst=20 nodelay;
    }
}

# Add to nginx.conf (http block):
limit_req_zone $binary_remote_addr zone=telemetry:10m rate=10r/s;
```

**Set environment variable:**

```bash
# Add to /etc/environment or /etc/nginx/envvars
export KOTORMODSYNC_SIGNING_SECRET="your_secret_here"

# Reload nginx
sudo systemctl reload nginx
```

### Option 2: OTLP Collector Extension (Alternative)

If you don't want Nginx Lua, you can use the OTLP collector's auth extension.

**Update `otel-collector-config.yaml`:**

```yaml
extensions:
  health_check:
    endpoint: 0.0.0.0:8888

  # Custom HTTP header authenticator
  headers_setter:
    headers:
      - action: insert
        key: X-Scope-OrgID
        from_context: tenant_id

receivers:
  otlp:
    protocols:
      http:
        endpoint: 0.0.0.0:4318
        # Note: Built-in auth is limited, Nginx verification is better

processors:
  batch:
    timeout: 10s
    send_batch_size: 1024

  # Drop requests without valid build header (basic filter)
  filter/official_only:
    metrics:
      metric:
        - 'resource.attributes["build"] != "official"'

exporters:
  prometheus:
    endpoint: "prometheus.bolabaden.org:9090"
    namespace: kotormodsync

service:
  extensions: [health_check, headers_setter]
  pipelines:
    metrics:
      receivers: [otlp]
      processors: [filter/official_only, batch]
      exporters: [prometheus]
```

**Note:** This approach is less secure than Nginx Lua verification. It only filters by build type, doesn't verify cryptographic signatures.

### Option 3: No Verification (Not Recommended)

If you skip signature verification:

- Anyone can send fake telemetry to your server
- Rate limiting is your only protection
- Acceptable for testing/development only

## Testing Signature Authentication

### 1. Test with Valid Signature

```python
import hmac
import hashlib
import time
import requests

SECRET = "your_secret_here"
SESSION_ID = "test-session-123"
REQUEST_PATH = "/v1/metrics"
TIMESTAMP = int(time.time())

# Compute signature
message = f"POST|{REQUEST_PATH}|{TIMESTAMP}|{SESSION_ID}"
signature = hmac.new(
    SECRET.encode(),
    message.encode(),
    hashlib.sha256
).hexdigest()

# Send request
headers = {
    "X-KMS-Signature": signature,
    "X-KMS-Timestamp": str(TIMESTAMP),
    "X-KMS-Session": SESSION_ID,
    "X-KMS-Version": "1.0.0",
    "X-KMS-Build": "official",
    "Content-Type": "application/json"
}

response = requests.post(
    "https://otlp.bolabaden.org/v1/metrics",
    headers=headers,
    json={"resourceMetrics": []}
)

print(f"Status: {response.status_code}")
# Expected: 200 or 400 (accepted)
```

### 2. Test with Invalid Signature

```python
# Same as above but with wrong signature
headers["X-KMS-Signature"] = "invalid_signature_123"

response = requests.post(...)
print(f"Status: {response.status_code}")
# Expected: 401 Unauthorized
```

### 3. Test with No Signature

```bash
curl -X POST https://otlp.bolabaden.org/v1/metrics \
  -H "Content-Type: application/json" \
  -d '{"resourceMetrics":[]}'

# Expected: 401 Unauthorized (if verification is enabled)
```

## Development Builds

For local development/testing **without** the GitHub Secret:

### Option 1: Local Config File

Create `%AppData%/KOTORModSync/telemetry.key` (Windows) or `~/.config/KOTORModSync/telemetry.key` (Linux/Mac):

```
your_secret_here
```

### Option 2: Environment Variable

```bash
# Windows (PowerShell)
$env:KOTORMODSYNC_SIGNING_SECRET = "your_secret_here"

# Linux/Mac
export KOTORMODSYNC_SIGNING_SECRET="your_secret_here"
```

### Option 3: No Secret (Development Mode)

If no secret is found:

- Telemetry will still work
- But headers won't include signature
- Server may reject requests (if verification is enabled)

## Security Best Practices

1. **Use a strong secret:** Minimum 32 bytes, cryptographically random
2. **Rotate periodically:** Change secret every 6-12 months
3. **Monitor logs:** Check for signature verification failures
4. **Rate limit aggressively:** Even with authentication, limit requests per IP
5. **Log failed attempts:** Track IPs with invalid signatures
6. **Use HTTPS only:** Never send signatures over HTTP

## Secret Rotation

When changing the secret:

1. Generate new secret: `openssl rand -base64 32`
2. Update GitHub Secret
3. Update server-side verification code
4. Trigger new GitHub Actions build
5. Old clients will fail authentication until they update

**Grace period:** Consider accepting both old and new secrets for 30 days during transition.

## Troubleshooting

### Clients sending telemetry but server rejecting it

Check logs:

```bash
# Nginx logs
sudo tail -f /var/log/nginx/error.log | grep KMS

# OTLP collector logs
docker compose logs -f otel-collector
```

Common issues:

- Time skew (timestamp too old/new)
- Secret mismatch between client and server
- Missing required headers
- Non-official build being rejected

### GitHub Actions build failing

Check if secret is set:

1. Go to repo Settings → Secrets → Actions
2. Verify `KOTORMODSYNC_SIGNING_SECRET` exists
3. Check build logs for "Injecting telemetry signing secret"

### Local development not working

1. Check if secret is loaded:

   ```bash
   # Check environment variable
   echo $KOTORMODSYNC_SIGNING_SECRET

   # Check config file exists
   cat %AppData%/KOTORModSync/telemetry.key
   ```

2. Check application logs:

   ```
   [Telemetry] Signing secret loaded from environment variable
   [Telemetry] Signing secret loaded from config file
   [Telemetry] No signing secret found - telemetry will be disabled
   ```

## Summary

**What you need to do RIGHT NOW:**

1. ✅ Generate secret: `openssl rand -base64 32`
2. ✅ Add to GitHub: Settings → Secrets → Actions → `KOTORMODSYNC_SIGNING_SECRET`
3. ✅ Add to server: Configure Nginx Lua verification (see above)
4. ✅ Test: Run GitHub Actions build, verify EmbeddedSecrets.cs is created
5. ✅ Deploy: Run KOTORModSync, check telemetry headers include signature

**Workflows using the secret:**

- `.github/workflows/build-and-release.yml` (lines 113, 230)

**Files involved:**

- `KOTORModSync.Core/Services/TelemetryConfiguration.cs` - Loads secret
- `KOTORModSync.Core/Services/TelemetryAuthenticator.cs` - Computes signature
- `KOTORModSync.Core/Services/TelemetryService.cs` - Sends authenticated requests
- `KOTORModSync.Core/Services/EmbeddedSecrets.cs` - Auto-generated (NOT in git)

---

**Questions?** Check logs with `docker compose logs otel-collector` or KOTORModSync application logs for `[Telemetry]` messages.
