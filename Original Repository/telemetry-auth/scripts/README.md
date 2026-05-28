# Scripts Directory

This directory contains utility scripts for managing the KOTORModSync Telemetry Authentication Service.

## Available Scripts

### `generate-secret.sh`
**Purpose:** Generate a cryptographically secure signing secret

**Usage:**
```bash
./scripts/generate-secret.sh [output_file]
```

**Default:** `signing_secret.txt`

**Example:**
```bash
# Generate default secret
./scripts/generate-secret.sh

# Generate to custom location
./scripts/generate-secret.sh /path/to/my_secret.txt
```

---

### `setup.sh`
**Purpose:** Initial setup script for deploying the service

**Usage:**
```bash
./scripts/setup.sh
```

**What it does:**
1. Generates signing secret (if needed)
2. Builds Docker image
3. Starts the service
4. Displays setup instructions

**Interactive:** Prompts before overwriting existing secrets

---

### `test-auth.sh`
**Purpose:** Test authentication functionality

**Usage:**
```bash
./scripts/test-auth.sh [test_type]
```

**Test types:**
- `valid` - Send request with valid signature (should succeed)
- `invalid` - Send request with invalid signature (should fail 403)
- `missing` - Send request without signature (should fail 401)
- `replay` - Send request with old timestamp (should fail 401)
- `ratelimit` - Test rate limiting (25 requests)
- `all` - Run all tests

**Examples:**
```bash
# Test with valid signature
./scripts/test-auth.sh valid

# Test invalid signature
./scripts/test-auth.sh invalid

# Run all tests
./scripts/test-auth.sh all

# Test against production endpoint
OTLP_ENDPOINT=https://otlp.example.com ./scripts/test-auth.sh valid
```

**Environment variables:**
- `OTLP_ENDPOINT` - Endpoint to test (default: `https://otlp.bolabaden.org`)
- `SECRET_FILE` - Path to secret file (default: `../signing_secret.txt`)

---

### `rotate-secret.sh`
**Purpose:** Rotate the signing secret

**Usage:**
```bash
./scripts/rotate-secret.sh [secret_file]
```

**Default:** `signing_secret.txt`

**What it does:**
1. Backs up current secret with timestamp
2. Generates new secret
3. Restarts service (if running)
4. Provides instructions for updating GitHub Actions

**Example:**
```bash
# Rotate default secret
./scripts/rotate-secret.sh

# Rotate custom secret
./scripts/rotate-secret.sh /path/to/secret.txt
```

**⚠️ Important:** After rotating, you MUST:
1. Update GitHub Actions secret
2. Publish new KOTORModSync release
3. Notify users to update

---

### `verify-deployment.sh`
**Purpose:** Verify deployment health and configuration

**Usage:**
```bash
./scripts/verify-deployment.sh [endpoint]
```

**Default:** `http://localhost:8080`

**What it checks:**
- ✅ Docker service running
- ✅ Health endpoint accessible
- ✅ Secret file exists and has proper length
- ✅ Required files present (Dockerfile, auth_service.py, etc.)
- ✅ Authentication working correctly
- ✅ Recent logs for errors

**Example:**
```bash
# Verify local deployment
./scripts/verify-deployment.sh

# Verify production deployment
./scripts/verify-deployment.sh https://otlp.example.com
```

**Exit codes:**
- `0` - All checks passed
- `1` - One or more checks failed

---

## Common Workflows

### Initial Setup
```bash
# 1. Generate secret
./scripts/generate-secret.sh

# 2. Run setup
./scripts/setup.sh

# 3. Verify deployment
./scripts/verify-deployment.sh

# 4. Test authentication
./scripts/test-auth.sh all
```

### Testing
```bash
# Test locally
docker compose up -d
./scripts/test-auth.sh valid

# Test production
OTLP_ENDPOINT=https://otlp.example.com \
SECRET_FILE=./production_secret.txt \
./scripts/test-auth.sh valid
```

### Secret Rotation
```bash
# 1. Rotate secret
./scripts/rotate-secret.sh

# 2. Test new secret
./scripts/test-auth.sh valid

# 3. Update GitHub Actions
# (Manual step - see script output)

# 4. Verify in production
./scripts/verify-deployment.sh https://otlp.example.com
```

### Troubleshooting
```bash
# Verify deployment status
./scripts/verify-deployment.sh

# Check authentication
./scripts/test-auth.sh missing  # Should fail with 401
./scripts/test-auth.sh invalid  # Should fail with 403
./scripts/test-auth.sh valid    # Should succeed with 200

# View logs
docker compose logs -f telemetry-auth

# Restart service
docker compose restart telemetry-auth
```

## Development

### Adding New Scripts

When adding new scripts:

1. **Make it executable:**
   ```bash
   chmod +x scripts/new-script.sh
   ```

2. **Add shebang:**
   ```bash
   #!/bin/bash
   set -e  # Exit on error
   ```

3. **Document in this README:**
   - Purpose
   - Usage
   - Examples
   - Environment variables

4. **Follow naming convention:**
   - Use lowercase with hyphens
   - Descriptive names (e.g., `verify-deployment.sh` not `check.sh`)

### Testing Scripts

```bash
# Syntax check
bash -n scripts/your-script.sh

# Run with debug
bash -x scripts/your-script.sh

# Test in clean environment
docker run --rm -v $(pwd):/app -w /app alpine:latest sh -c "apk add bash && ./scripts/your-script.sh"
```

## CI/CD Integration

These scripts are designed to be used in CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Setup and test
  run: |
    cd telemetry-auth
    ./scripts/generate-secret.sh
    ./scripts/setup.sh
    ./scripts/verify-deployment.sh
    ./scripts/test-auth.sh all
```

## Security Notes

⚠️ **Never commit:**
- Actual signing secrets
- Production credentials
- Sensitive log files

✅ **Always:**
- Use `.gitignore` for secret files
- Set proper file permissions (`chmod 600`)
- Rotate secrets regularly
- Test after changes

## Support

For issues with scripts:
1. Check script output for error messages
2. Verify dependencies installed (bash, curl, openssl, docker)
3. Run with debug: `bash -x scripts/script-name.sh`
4. Open issue on GitHub

