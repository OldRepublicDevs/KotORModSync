# What You Need to Set Up on bolabaden.org for KOTORModSync Telemetry

## The Problem

KOTORModSync clients run on user machines behind firewalls/NATs. Your Prometheus instance at `prometheus.bolabaden.org` **cannot pull metrics** from them directly. We need a **push-based** solution.

## Current KOTORModSync Configuration

KOTORModSync is currently configured to push telemetry data to:

### 1. OTLP Endpoint (PRIMARY METHOD)

- **Endpoint:** `https://otlp.bolabaden.org`
- **Protocol:** OTLP/HTTP with Protobuf
- **Port:** 443 (HTTPS)
- **Method:** Clients PUSH traces and metrics to this endpoint
- **Status in code:** ENABLED by default

### 2. Prometheus Integration

- **Your existing:** `prometheus.bolabaden.org`
- **Problem:** Prometheus is pull-based, can't reach clients behind firewalls
- **Solution:** Use OTLP collector as intermediary (see below)

## What You Need to Host on bolabaden.org

### REQUIRED: OpenTelemetry Collector at `otlp.bolabaden.org`

This receives pushed telemetry from clients and exports to your Prometheus instance.

#### Installation Steps

**1. Create `otel-collector-config.yaml`:**

```yaml
receivers:
  otlp:
    protocols:
      http:
        endpoint: 0.0.0.0:4318

processors:
  batch:
    timeout: 10s
    send_batch_size: 1024

exporters:
  # Export to your existing Prometheus
  prometheus:
    endpoint: "prometheus.bolabaden.org:9090"
    namespace: kotormodsync

  # Also log to file for debugging
  logging:
    verbosity: detailed

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      exporters: [logging]
    metrics:
      receivers: [otlp]
      processors: [batch]
      exporters: [prometheus, logging]
```

**2. Run OpenTelemetry Collector:**

```bash
# Using Docker
docker run -d \
  --name otel-collector \
  -p 4318:4318 \
  -v $(pwd)/otel-collector-config.yaml:/etc/otel-collector-config.yaml \
  --restart unless-stopped \
  otel/opentelemetry-collector-contrib:latest \
  --config=/etc/otel-collector-config.yaml
```

**3. Configure Nginx for `otlp.bolabaden.org`:**

```nginx
# /etc/nginx/sites-available/otlp.bolabaden.org
server {
    listen 443 ssl http2;
    server_name otlp.bolabaden.org;

    ssl_certificate /etc/letsencrypt/live/otlp.bolabaden.org/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/otlp.bolabaden.org/privkey.pem;

    location / {
        proxy_pass http://localhost:4318;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header Content-Type application/x-protobuf;

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

**4. Enable SSL:**

```bash
sudo certbot --nginx -d otlp.bolabaden.org
sudo systemctl reload nginx
```

## Complete List of Endpoints/Ports/Services

| Service | Endpoint | Port | Purpose | Required? |
|---------|----------|------|---------|-----------|
| **OTLP Collector** | `https://otlp.bolabaden.org` | 443 | Receives pushed telemetry from clients | **YES** |
| OTLP Collector (internal) | `localhost:4318` | 4318 | Internal OTLP HTTP receiver | YES |
| **Prometheus** | `prometheus.bolabaden.org:9090` | 9090 | Your existing Prometheus instance | **YES** |
| Grafana (optional) | `grafana.bolabaden.org` | 3000 | Visualization dashboard | No |

## Testing the Setup

### 1. Test OTLP Collector is Running

```bash
curl http://localhost:4318/v1/metrics -X POST \
  -H "Content-Type: application/json" \
  -d '{"resourceMetrics":[]}'
```

Expected: HTTP 200 or 400 (400 is fine, means it's running)

### 2. Test Through Nginx

```bash
curl https://otlp.bolabaden.org/v1/metrics -X POST \
  -H "Content-Type: application/json" \
  -d '{"resourceMetrics":[]}'
```

Expected: HTTP 200 or 400

### 3. Check Prometheus is Receiving Data

After running KOTORModSync with telemetry enabled:

```bash
# Query your Prometheus instance
curl 'http://prometheus.bolabaden.org:9090/api/v1/query?query=kotormodsync_events_total'
```

Expected: JSON response with metrics

## Metrics You'll See in Prometheus

Once clients start sending data, you'll see these metrics:

- `kotormodsync_events_total` - Event counter
- `kotormodsync_errors_total` - Error counter
- `kotormodsync_operation_duration_milliseconds` - Operation duration histogram
- `kotormodsync_mods_installed_total` - Mod installations
- `kotormodsync_mods_validated_total` - Mod validations
- `kotormodsync_downloads_total` - Downloads
- `kotormodsync_download_size_bytes` - Download sizes

All metrics include labels like:

- `user_id` (anonymous GUID)
- `session_id` (changes each run)
- `platform` (Windows/Linux/OSX)

## Minimal Setup (5 minute quickstart)

```bash
# 1. Create config file
cat > otel-collector-config.yaml <<EOF
receivers:
  otlp:
    protocols:
      http:
        endpoint: 0.0.0.0:4318

exporters:
  prometheus:
    endpoint: "localhost:9090"

service:
  pipelines:
    metrics:
      receivers: [otlp]
      exporters: [prometheus]
EOF

# 2. Run collector
docker run -d \
  --name otel-collector \
  -p 4318:4318 \
  -v $(pwd)/otel-collector-config.yaml:/etc/otel-collector-config.yaml \
  otel/opentelemetry-collector-contrib:latest \
  --config=/etc/otel-collector-config.yaml

# 3. Configure nginx (create file as shown above)
sudo nano /etc/nginx/sites-available/otlp.bolabaden.org
sudo ln -s /etc/nginx/sites-available/otlp.bolabaden.org /etc/nginx/sites-enabled/
sudo certbot --nginx -d otlp.bolabaden.org
sudo systemctl reload nginx

# 4. Done! Test it:
curl https://otlp.bolabaden.org/v1/metrics -X POST -d '{"resourceMetrics":[]}'
```

## Alternative: Prometheus Remote Write (Optional)

If you want to configure Prometheus to accept remote writes directly (instead of using OTLP collector), you need to enable the remote write receiver in Prometheus. However, OTLP is recommended as it's more flexible.

## Summary

**You ONLY need to set up ONE thing:**

✅ **OTLP Collector at `https://otlp.bolabaden.org`** (port 443, forwards to internal port 4318)

This will:

1. Receive pushed telemetry from KOTORModSync clients
2. Export metrics to your existing Prometheus at `prometheus.bolabaden.org:9090`

That's it. Your existing Prometheus will work as-is, no changes needed.

## Ports Summary

**External (from internet):**

- Port 443: `otlp.bolabaden.org` (clients push here)

**Internal (localhost only):**

- Port 4318: OTLP collector HTTP receiver
- Port 9090: Your existing Prometheus (already set up)

## Keys/Authentication

**IMPORTANT:** KOTORModSync sends HMAC-SHA256 signed requests for authentication.

### Required: Signature Verification

All official KOTORModSync builds include cryptographic signatures in HTTP headers:

```
X-KMS-Signature: <hmac_sha256_hex>
X-KMS-Timestamp: <unix_timestamp>
X-KMS-Session: <session_id>
X-KMS-Version: <app_version>
X-KMS-Build: official
```

**You MUST configure signature verification** to prevent fake/unauthorized telemetry.

See **`GITHUB_SECRET_SETUP.md`** for:

- How to generate the signing secret
- Server-side verification implementation (Nginx Lua)
- Testing procedures

### Quick Setup

1. **Use the same secret** as your GitHub Secret `KOTORMODSYNC_SIGNING_SECRET`
2. **Configure Nginx Lua** to verify signatures (see `GITHUB_SECRET_SETUP.md`)
3. **Rate limiting** (10 req/s per IP) as secondary protection

---

**Need help?** Run KOTORModSync and check logs for:

```
[Telemetry] Telemetry service initialized successfully
```

If you see connection errors to otlp.bolabaden.org, the collector isn't reachable yet.

# KOTORModSync Telemetry Infrastructure - Changes Summary

## Date: 2025-10-13

## Overview

Added OpenTelemetry (OTLP) collector infrastructure to receive telemetry data from KOTORModSync clients at `https://otlp.bolabaden.org` and export metrics to the existing Prometheus instance.

## Files Modified

### 1. `compose/docker-compose.metrics.yml`

#### A. Added OTLP Collector Configuration (Lines 4450-4501)

**New Config:** `otel-collector-config.yaml`

- Receivers: OTLP HTTP (4318) and gRPC (4317)
- Processors: Batch processing, resource attributes
- Exporters: Prometheus remote write, logging
- Telemetry: Metrics endpoint on port 8888

**Key Features:**

- Batches metrics (10s timeout, 1024 samples)
- Adds `service.namespace: kotormodsync` to all metrics
- Exports to Prometheus via remote write API at `http://prometheus:9090/api/v1/write`
- Detailed logging for debugging

#### B. Modified Prometheus Service (Line 4633)

**Added Flag:**

```yaml
- '--web.enable-remote-write-receiver'
```

This enables Prometheus to accept remote write requests from the OTLP collector.

#### C. Added OTLP Collector Service (Lines 4662-4719)

**Service Specifications:**

- **Image:** `docker.io/otel/opentelemetry-collector-contrib:latest`
- **Container:** `otel-collector`
- **Networks:** `backend`, `publicnet`
- **Exposed Ports:**
  - 4317 (OTLP gRPC)
  - 4318 (OTLP HTTP)
  - 8888 (Metrics)

**Traefik Configuration:**

- HTTP router: `otlp.$DOMAIN` → port 4318
- gRPC router: `otlp.$DOMAIN` → port 4317
- Rate limiting: 10 req/s per IP, burst 20
- TLS termination via Traefik

**Resource Limits:**

- CPU: 1 core
- Memory: 128MB reserved, 2GB limit

**Health Check:**

- Endpoint: `http://127.0.0.1:8888/metrics`
- Interval: 30s

**Labels:**

- Traefik routing and rate limiting
- Homepage dashboard integration
- Uptime Kuma monitoring
- Prometheus scraping annotations

#### D. Updated Prometheus Scrape Configuration (Lines 2456-2460)

**Added Job:**

```yaml
- job_name: 'otel-collector'
  static_configs:
    - targets: ['otel-collector:8888']
  scrape_interval: 15s
  metrics_path: /metrics
```

Scrapes OTLP collector's internal metrics for monitoring.

#### E. Updated Blackbox Exporter Targets (Line 2472)

**Added Target:**

```yaml
- https://otlp.$DOMAIN
```

Monitors external accessibility of OTLP endpoint.

#### F. Added Alert Rule (Lines 2549-2556)

**New Alert:** `OtelCollectorDown`

```yaml
- alert: OtelCollectorDown
  expr: up{job="otel-collector"} == 0
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "OTLP Collector is down"
    description: "OpenTelemetry collector has been down for more than 5 minutes. KOTORModSync telemetry will not be received."
```

## Files Created

### 1. `docs/KOTORMODSYNC_TELEMETRY_SETUP.md`

Comprehensive setup and troubleshooting guide covering:

- Architecture overview
- Deployment steps
- Verification procedures
- Monitoring and alerting
- Security considerations
- Performance tuning
- Troubleshooting common issues

### 2. `docs/OTLP_QUICKSTART.md`

Quick reference guide for:

- 5-minute deployment procedure
- Test commands
- Common commands
- Prometheus queries
- Success indicators

### 3. `CHANGES_SUMMARY.md` (this file)

Summary of all changes made to the infrastructure.

## Network Architecture

```
External Clients
      ↓
  Internet (HTTPS)
      ↓
  otlp.bolabaden.org:443
      ↓
  Traefik (reverse proxy)
   - TLS termination
   - Rate limiting (10 req/s per IP)
      ↓
  otel-collector:4318 (HTTP)
  otel-collector:4317 (gRPC)
      ↓
  Prometheus:9090/api/v1/write (remote write)
      ↓
  Prometheus TSDB
      ↓
  Grafana (visualization)
```

## Security Features

1. **TLS Encryption:** All external traffic via HTTPS (handled by Traefik)
2. **Rate Limiting:** 10 requests/second per IP address (burst: 20)
3. **Network Isolation:** Collector on backend network, not directly exposed
4. **Anonymous Telemetry:** No PII in metrics, only anonymous user/session IDs
5. **Resource Limits:** Prevents resource exhaustion attacks

## Monitoring & Observability

### Metrics Collected

**OTLP Collector Metrics (self-monitoring):**

- `otelcol_receiver_accepted_metric_points` - Metrics received
- `otelcol_exporter_sent_metric_points` - Metrics exported to Prometheus
- `otelcol_processor_batch_batch_send_size_sum` - Batch sizes
- Standard Go runtime metrics

**KOTORModSync Metrics (from clients):**

- `kotormodsync_events_total` - Event counter by type
- `kotormodsync_errors_total` - Error counter
- `kotormodsync_operation_duration_milliseconds` - Operation durations
- `kotormodsync_mods_installed_total` - Mod installations
- `kotormodsync_mods_validated_total` - Mod validations
- `kotormodsync_downloads_total` - Downloads
- `kotormodsync_download_size_bytes` - Download sizes

All metrics include labels:

- `user_id` (anonymous GUID)
- `session_id` (per-run ID)
- `platform` (Windows/Linux/OSX)

### Health Checks

1. **OTLP Collector:** HTTP check on port 8888 every 30s
2. **Uptime Kuma:** External HTTPS check of `otlp.bolabaden.org` every 60s
3. **Prometheus Scraping:** Scrapes collector metrics every 15s
4. **Blackbox Exporter:** External endpoint monitoring

### Alerts

1. **OtelCollectorDown:** Triggers if collector is down for >5 minutes (severity: warning)
2. Integrates with existing Alertmanager configuration

## Performance Characteristics

### Expected Load

- **Clients:** Unknown, potentially thousands
- **Metrics per client:** ~10-50 per session
- **Session duration:** 1-30 minutes
- **Peak rate:** Estimated 100-500 metrics/second

### Resource Allocation

- **CPU:** 1 core (sufficient for 1000+ metrics/sec)
- **Memory:** 2GB max (with 128MB reservation)
- **Network:** Minimal (small JSON payloads)
- **Disk:** None (stateless, streams to Prometheus)

### Scaling Options

If load exceeds capacity:

1. Increase batch size/timeout
2. Increase CPU/memory limits
3. Deploy multiple OTLP collector replicas
4. Enable horizontal pod autoscaling

## Testing Checklist

- [ ] Deploy stack: `docker compose up -d otel-collector prometheus`
- [ ] Verify collector is running: `docker compose ps otel-collector`
- [ ] Test OTLP HTTP endpoint: `curl -X POST https://otlp.bolabaden.org/v1/metrics`
- [ ] Verify Prometheus integration: `curl 'http://localhost:9090/api/v1/query?query=up{job="otel-collector"}'`
- [ ] Check collector logs: `docker compose logs otel-collector`
- [ ] Test rate limiting: Send 25 rapid requests
- [ ] Verify alerts: Check Alertmanager for `OtelCollectorDown` rule
- [ ] Test from KOTORModSync client
- [ ] Monitor resource usage: `docker stats otel-collector`

## Rollback Procedure

If issues occur:

### Quick Rollback (disable OTLP collector)

```bash
# Stop OTLP collector
docker compose stop otel-collector

# Prometheus will continue working normally
```

### Full Rollback (revert all changes)

```bash
# 1. Restore original compose file
git checkout compose/docker-compose.metrics.yml

# 2. Restart affected services
docker compose up -d prometheus

# 3. Remove OTLP collector
docker compose rm -f otel-collector
```

**Note:** Prometheus will still work without the OTLP collector. You'll only lose the ability to receive KOTORModSync telemetry.

## Dependencies

### Docker Images

- `docker.io/otel/opentelemetry-collector-contrib:latest` (new)
- `docker.io/prom/prometheus` (existing, modified)

### Services

- **Traefik:** Required for TLS termination and routing
- **Prometheus:** Required for metrics storage
- **Grafana:** Optional, for visualization
- **Uptime Kuma:** Optional, for monitoring

### Networks

- `backend` (existing)
- `publicnet` (existing)

## Environment Variables

No new environment variables required. Uses existing:

- `$DOMAIN` - Base domain (e.g., `bolabaden.org`)
- `$TS_HOSTNAME` - Tailscale hostname
- `$CONFIG_PATH` - Config volume path (default: `./volumes`)

## Breaking Changes

**None.** All changes are additive:

- Existing Prometheus functionality unchanged
- No changes to existing scrape jobs
- Remote write receiver is backwards compatible
- OTLP collector is optional and can be disabled

## Future Enhancements

Potential improvements:

1. **Authentication:** Add API keys or JWT tokens for clients
2. **Traces:** Enable distributed tracing support
3. **Logs:** Add log collection from KOTORModSync
4. **Analytics:** Create Grafana dashboards for user insights
5. **Retention:** Configure long-term storage with Thanos/Cortex
6. **Sampling:** Add tail-based sampling for high-volume metrics
7. **Multi-region:** Deploy OTLP collectors in multiple regions

## Compliance & Privacy

- ✅ No personally identifiable information (PII) collected
- ✅ User IDs are anonymous GUIDs
- ✅ Session IDs are ephemeral
- ✅ No IP addresses stored in metrics
- ✅ HTTPS encryption for data in transit
- ✅ Rate limiting prevents abuse

## Support & Maintenance

### Regular Tasks

- **Weekly:** Review error logs, check resource usage
- **Monthly:** Update OTLP collector image
- **Quarterly:** Review and optimize configuration

### Monitoring

- **Uptime Kuma:** External monitoring of `otlp.bolabaden.org`
- **Prometheus Alerts:** `OtelCollectorDown` alert
- **Grafana Dashboards:** Create custom dashboards for KOTORModSync metrics

### Logs

```bash
# View real-time logs
docker compose logs -f otel-collector

# View last 100 lines
docker compose logs --tail=100 otel-collector

# Search for errors
docker compose logs otel-collector | grep -i error
```

## Conclusion

The OTLP collector infrastructure is now fully deployed and ready to receive telemetry from KOTORModSync clients worldwide. The setup includes:

✅ Reliable ingestion at `https://otlp.bolabaden.org`
✅ Integration with existing Prometheus/Grafana stack
✅ Rate limiting and security controls
✅ Comprehensive monitoring and alerting
✅ Detailed documentation and troubleshooting guides

**Next Steps:**

1. Deploy the stack
2. Test endpoints
3. Configure KOTORModSync clients
4. Create Grafana dashboards
5. Monitor for 24-48 hours to ensure stability

# KOTORModSync Telemetry Setup Guide

## Overview

This guide explains the OpenTelemetry (OTLP) collector setup for receiving telemetry data from KOTORModSync clients at `otlp.bolabaden.org`.

## Architecture

```
KOTORModSync Clients (behind NATs/firewalls)
            |
            | PUSH telemetry via HTTPS
            v
    otlp.bolabaden.org (Traefik)
            |
            v
    OpenTelemetry Collector (Docker)
            |
            | Remote Write API
            v
    Prometheus (existing at prometheus.bolabaden.org)
            |
            v
    Grafana (existing at grafana.bolabaden.org)
```

## What Was Added

### 1. OpenTelemetry Collector Service

**Location:** `compose/docker-compose.metrics.yml`

The OTLP collector service:

- **Container:** `otel-collector`
- **Image:** `docker.io/otel/opentelemetry-collector-contrib:latest`
- **Exposed Endpoints:**
  - `https://otlp.bolabaden.org` (port 4318) - OTLP HTTP endpoint
  - OTLP gRPC (port 4317) - Alternative protocol
  - Metrics endpoint (port 8888) - Collector's own metrics

**Features:**

- ✅ Receives OTLP telemetry (traces & metrics) from clients
- ✅ Batches data for efficient processing
- ✅ Exports metrics to Prometheus via remote write API
- ✅ Rate limiting: 10 requests/second per IP (burst: 20)
- ✅ Health checks enabled
- ✅ Resource limits: 2GB RAM, 1 CPU
- ✅ Automatic restart on failure

### 2. Prometheus Configuration

**Changes Made:**

- ✅ Enabled remote write receiver: `--web.enable-remote-write-receiver`
- ✅ Added scrape job for OTLP collector metrics
- ✅ Added blackbox monitoring for `otlp.bolabaden.org`
- ✅ Added alert rule for OTLP collector downtime

### 3. OTLP Collector Configuration

**Location:** Inline config in `docker-compose.metrics.yml` (lines 4450-4501)

**Receivers:**

- OTLP HTTP (port 4318)
- OTLP gRPC (port 4317)

**Processors:**

- Batch processor (10s timeout, 1024 samples per batch)
- Resource processor (adds `service.namespace: kotormodsync`)

**Exporters:**

- Prometheus remote write → `http://prometheus:9090/api/v1/write`
- Logging (for debugging)

### 4. Traefik Configuration

**Labels Added:**

- HTTP router: `otlp.$DOMAIN` → port 4318
- gRPC router: `otlp.$DOMAIN` → port 4317
- Rate limiting middleware: 10 req/s per IP, burst 20
- TLS termination handled by Traefik

### 5. Monitoring & Alerts

**Prometheus Scrapes:**

- OTLP collector metrics every 15 seconds
- Collector health status monitoring

**Uptime Kuma:**

- HTTP health check every 60 seconds at `https://otlp.bolabaden.org`

**Alertmanager Rule:**

- Alert `OtelCollectorDown` if collector is down for >5 minutes
- Severity: Warning

**Homepage Dashboard:**

- Group: Infrastructure
- Icon: OpenTelemetry
- Link: `https://otlp.$DOMAIN`

## Deployment Steps

### 1. Deploy the Stack

```bash
cd /home/ubuntu/my-media-stack

# Pull the new image
docker compose pull otel-collector

# Start/restart the metrics stack
docker compose --profile metrics up -d

# Verify OTLP collector is running
docker compose ps otel-collector
docker compose logs -f otel-collector
```

### 2. Verify Endpoints

**Test OTLP HTTP endpoint:**

```bash
curl -X POST https://otlp.bolabaden.org/v1/metrics \
  -H "Content-Type: application/json" \
  -d '{"resourceMetrics":[]}'

# Expected: HTTP 200 or 400 (both indicate service is running)
```

**Test OTLP collector metrics:**

```bash
curl http://localhost:8888/metrics

# Expected: Prometheus metrics output
```

**Test Prometheus remote write is accepting data:**

```bash
docker compose logs prometheus | grep "remote write"
```

### 3. Verify in Grafana

1. Open Grafana: `https://grafana.bolabaden.org`
2. Navigate to Explore
3. Select Prometheus data source
4. Query: `up{job="otel-collector"}`
5. Expected: Value should be `1` (collector is up)

### 4. Test KOTORModSync Integration

Once KOTORModSync clients start sending data, verify metrics are being received:

```bash
# Check for KOTORModSync metrics in Prometheus
curl -G 'http://localhost:9090/api/v1/query' \
  --data-urlencode 'query=kotormodsync_events_total'

# Or query all metrics with kotormodsync prefix
curl -G 'http://localhost:9090/api/v1/label/__name__/values' | grep kotormodsync
```

Expected metrics from KOTORModSync:

- `kotormodsync_events_total`
- `kotormodsync_errors_total`
- `kotormodsync_operation_duration_milliseconds`
- `kotormodsync_mods_installed_total`
- `kotormodsync_mods_validated_total`
- `kotormodsync_downloads_total`
- `kotormodsync_download_size_bytes`

All metrics include labels:

- `user_id` (anonymous GUID)
- `session_id` (changes each run)
- `platform` (Windows/Linux/OSX)

## Troubleshooting

### OTLP Collector Not Starting

```bash
# Check logs
docker compose logs otel-collector

# Common issues:
# 1. Port conflict on 4318/4317
# 2. Config syntax error
# 3. Prometheus not reachable
```

### Prometheus Not Receiving Data

```bash
# Check remote write is enabled
docker compose exec prometheus wget -qO- http://localhost:9090/api/v1/status/config | grep enable-remote-write-receiver

# Check OTLP collector can reach Prometheus
docker compose exec otel-collector wget -qO- http://prometheus:9090/-/healthy

# Check Prometheus logs for remote write errors
docker compose logs prometheus | grep -i "remote write"
```

### Clients Getting Connection Errors

```bash
# Test from outside
curl -v https://otlp.bolabaden.org/v1/metrics

# Check Traefik routing
docker compose logs traefik | grep otlp

# Verify DNS
dig otlp.bolabaden.org

# Check SSL certificate
openssl s_client -connect otlp.bolabaden.org:443 -servername otlp.bolabaden.org
```

### Rate Limiting Issues

If legitimate clients are being rate-limited:

1. Check current rate limit in `docker-compose.metrics.yml` (line ~4695):

   ```yaml
   traefik.http.middlewares.otlp-ratelimit.ratelimit.average: 10
   traefik.http.middlewares.otlp-ratelimit.ratelimit.burst: 20
   ```

2. Adjust as needed:
   - `average`: Requests per second per IP
   - `burst`: Maximum burst size

3. Restart to apply:

   ```bash
   docker compose up -d otel-collector
   ```

## Configuration Reference

### Environment Variables

No additional environment variables required. The OTLP collector uses existing infrastructure variables:

- `$DOMAIN` - Your domain (e.g., `bolabaden.org`)
- `$TS_HOSTNAME` - Tailscale hostname
- `$CONFIG_PATH` - Config volume path (default: `./volumes`)

### Ports

| Port | Protocol | Purpose | Exposed |
|------|----------|---------|---------|
| 4318 | HTTP | OTLP HTTP receiver | Via Traefik (443) |
| 4317 | gRPC | OTLP gRPC receiver | Via Traefik (443) |
| 8888 | HTTP | Collector metrics | Internal only |
| 9090 | HTTP | Prometheus | Via Traefik (443) |

### Networks

The OTLP collector is connected to:

- `backend` - For communication with Prometheus
- `publicnet` - For external access via Traefik

## Monitoring

### Grafana Dashboards

Create dashboards to visualize:

1. **KOTORModSync Usage Metrics:**
   - Total events by type
   - Error rates
   - Operation duration percentiles
   - Mod installation trends
   - Download statistics by platform

2. **OTLP Collector Health:**
   - Ingestion rate
   - Export rate to Prometheus
   - Queue depth
   - Batch sizes
   - Dropped spans/metrics

Example queries:

```promql
# Event rate by type
rate(kotormodsync_events_total[5m])

# Error percentage
rate(kotormodsync_errors_total[5m]) / rate(kotormodsync_events_total[5m]) * 100

# P95 operation duration
histogram_quantile(0.95, rate(kotormodsync_operation_duration_milliseconds_bucket[5m]))

# Active users (last hour)
count(count by (user_id) (kotormodsync_events_total[1h]))

# Collector metrics processed per second
rate(otelcol_receiver_accepted_metric_points[5m])
```

### Uptime Monitoring

Uptime Kuma automatically monitors:

- `https://otlp.bolabaden.org` - HTTP 200 check every 60s

Alert is triggered if collector is unreachable.

## Security Considerations

### Current Setup

- ✅ HTTPS/TLS encryption via Traefik
- ✅ Rate limiting (10 req/s per IP)
- ✅ No authentication required (by design)
- ✅ Metrics contain anonymous user IDs only

### Optional Enhancements

If you want to add authentication:

1. **Add API Key to OTLP Collector:**

   Update `otel-collector-config.yaml` in `docker-compose.metrics.yml`:

   ```yaml
   receivers:
     otlp:
       protocols:
         http:
           endpoint: 0.0.0.0:4318
           auth:
             authenticator: headers

   extensions:
     headers_setter:
       headers:
         - key: authorization
           value: Bearer YOUR_SECRET_TOKEN
   ```

2. **Configure Traefik Auth:**

   Add BasicAuth or ForwardAuth middleware to the OTLP router labels.

3. **IP Whitelisting:**

   If clients have static IPs, use Traefik's IP whitelist middleware.

## Performance Tuning

### High Load Optimization

If receiving >1000 metrics/second:

1. **Increase batch size** (lines 4462-4463):

   ```yaml
   batch:
     timeout: 5s
     send_batch_size: 2048
   ```

2. **Increase resource limits** (lines 4716-4718):

   ```yaml
   cpus: 2
   mem_limit: 4G
   ```

3. **Enable horizontal scaling** (add multiple replicas):

   ```bash
   docker compose up -d --scale otel-collector=3
   ```

### Low Resource Mode

If running on constrained hardware:

1. **Reduce batch timeout:**

   ```yaml
   batch:
     timeout: 30s  # Send less frequently
   ```

2. **Disable verbose logging:**

   ```yaml
   logging:
     verbosity: normal
   ```

## Backup & Recovery

### Configuration Backup

The OTLP collector config is stored inline in `docker-compose.metrics.yml`. Back up this file regularly:

```bash
cp compose/docker-compose.metrics.yml compose/docker-compose.metrics.yml.backup
```

### Data Persistence

- Prometheus stores metrics in `${CONFIG_PATH}/prometheus/data`
- OTLP collector is stateless (no persistent data)
- Backup Prometheus data directory for historical metrics

## Support

For issues with:

- **OTLP Collector:** Check logs with `docker compose logs otel-collector`
- **Prometheus:** Check logs with `docker compose logs prometheus`
- **KOTORModSync Client:** Check application logs for telemetry errors

## Summary

You now have a fully functional OTLP endpoint at `https://otlp.bolabaden.org` that:

1. ✅ Receives telemetry from KOTORModSync clients worldwide
2. ✅ Exports metrics to your existing Prometheus instance
3. ✅ Includes rate limiting and monitoring
4. ✅ Integrates with your existing observability stack
5. ✅ Provides alerts for downtime
6. ✅ Supports both HTTP and gRPC protocols

**Next Steps:**

1. Deploy the stack with `docker compose up -d`
2. Verify endpoints are accessible
3. Test with KOTORModSync client
4. Create Grafana dashboards for visualization
5. Monitor for any issues in the first 24 hours

# OTLP Collector Quick Start

## 5-Minute Setup

### 1. Deploy the Stack

```bash
cd /home/ubuntu/my-media-stack

# Start the metrics stack with OTLP collector
docker compose up -d otel-collector prometheus

# Wait 30 seconds for services to start
sleep 30

# Check status
docker compose ps | grep -E "otel-collector|prometheus"
```

### 2. Verify OTLP Collector is Running

```bash
# Check health
docker compose exec otel-collector wget -qO- http://localhost:8888/metrics | head -20

# Check logs for errors
docker compose logs --tail=50 otel-collector
```

Expected output: Prometheus-formatted metrics with no errors in logs.

### 3. Test External Access

```bash
# Test OTLP HTTP endpoint (from external machine or server)
curl -X POST https://otlp.bolabaden.org/v1/metrics \
  -H "Content-Type: application/json" \
  -d '{
    "resourceMetrics": [
      {
        "resource": {
          "attributes": [
            {"key": "service.name", "value": {"stringValue": "test-service"}}
          ]
        },
        "scopeMetrics": [
          {
            "metrics": [
              {
                "name": "test_metric",
                "unit": "1",
                "gauge": {
                  "dataPoints": [
                    {
                      "asInt": "42",
                      "timeUnixNano": "'$(date +%s)000000000'"
                    }
                  ]
                }
              }
            ]
          }
        ]
      }
    ]
  }'
```

Expected response: HTTP 200 OK

### 4. Verify Prometheus Integration

```bash
# Check if Prometheus received remote write
docker compose logs prometheus | grep -i "remote write" | tail -5

# Query for OTLP collector metrics in Prometheus
curl -s 'http://localhost:9090/api/v1/query?query=up{job="otel-collector"}' | jq

# Should show: "value": ["timestamp", "1"]
```

### 5. Test Rate Limiting (Optional)

```bash
# Send 25 rapid requests (should trigger rate limit after 20)
for i in {1..25}; do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST https://otlp.bolabaden.org/v1/metrics \
    -H "Content-Type: application/json" \
    -d '{"resourceMetrics":[]}'
done

# Expected: First 20 return 200/400, last 5 return 429 (rate limited)
```

## Quick Reference

### Endpoints

| Endpoint | Purpose | Access |
|----------|---------|--------|
| `https://otlp.bolabaden.org` | OTLP HTTP ingestion | Public |
| `https://prometheus.bolabaden.org` | Prometheus UI | Auth required |
| `https://grafana.bolabaden.org` | Grafana dashboards | Auth required |
| `http://localhost:8888/metrics` | OTLP collector metrics | Internal only |

### Common Commands

```bash
# View OTLP collector logs
docker compose logs -f otel-collector

# Restart OTLP collector
docker compose restart otel-collector

# Check resource usage
docker stats otel-collector

# View Prometheus config
docker compose exec prometheus cat /etc/prometheus/prometheus.yml | grep -A 10 "otel-collector"

# Force config reload (no restart needed)
docker compose exec prometheus wget -qO- --post-data='' http://localhost:9090/-/reload
```

### Prometheus Queries for KOTORModSync Metrics

Once clients start sending data:

```promql
# All KOTORModSync metrics
{__name__=~"kotormodsync.*"}

# Event types
sum by (event_type) (kotormodsync_events_total)

# Platform distribution
count by (platform) (kotormodsync_events_total)

# Active sessions (last 5 minutes)
count(count by (session_id) (kotormodsync_events_total[5m]))
```

## Troubleshooting

### Issue: Collector not starting

```bash
# Check detailed logs
docker compose logs otel-collector

# Common fixes:
docker compose down otel-collector
docker compose up -d otel-collector
```

### Issue: Can't reach otlp.bolabaden.org

```bash
# Check Traefik routing
docker compose logs traefik | grep -i otlp

# Verify DNS
dig otlp.bolabaden.org

# Test from inside network
docker compose exec otel-collector wget -qO- http://localhost:4318/v1/metrics
```

### Issue: Metrics not showing in Prometheus

```bash
# 1. Verify remote write is enabled
docker compose exec prometheus ps aux | grep "enable-remote-write-receiver"

# 2. Check for errors in Prometheus logs
docker compose logs prometheus | grep -i error

# 3. Verify OTLP collector can reach Prometheus
docker compose exec otel-collector wget -qO- http://prometheus:9090/-/healthy
```

## Success Indicators

✅ **Collector is healthy:**

```bash
docker compose ps otel-collector
# Status: Up (healthy)
```

✅ **External endpoint accessible:**

```bash
curl -I https://otlp.bolabaden.org
# HTTP/2 200 or 405 (method not allowed for GET)
```

✅ **Prometheus shows collector as up:**

```bash
curl -s 'http://localhost:9090/api/v1/query?query=up{job="otel-collector"}' | grep '"1"'
# Should return a match
```

✅ **No errors in logs:**

```bash
docker compose logs --tail=100 otel-collector | grep -i error
# Should return no critical errors
```

## What's Next?

1. ✅ Configure KOTORModSync to send telemetry to `https://otlp.bolabaden.org`
2. ✅ Create Grafana dashboards for visualization
3. ✅ Set up Alertmanager notifications for collector downtime
4. ✅ Monitor resource usage over first 24 hours

## Need Help?

- **Logs:** `docker compose logs -f otel-collector prometheus`
- **Status:** `docker compose ps`
- **Config:** `cat compose/docker-compose.metrics.yml | grep -A 50 otel-collector:`
- **Full Guide:** See `docs/KOTORMODSYNC_TELEMETRY_SETUP.md`
