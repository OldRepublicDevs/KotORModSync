# Deployment Guide

This document describes how to deploy the KOTORModSync Telemetry Authentication Service in various environments.

## Table of Contents

- [Quick Start](#quick-start)
- [Docker Compose](#docker-compose)
- [Kubernetes](#kubernetes)
- [Traefik Integration](#traefik-integration)
- [Production Considerations](#production-considerations)
- [Monitoring](#monitoring)
- [Troubleshooting](#troubleshooting)

## Quick Start

### Generate Signing Secret

```bash
# Generate a cryptographically secure secret
openssl rand -hex 32 > signing_secret.txt

# Secure the file
chmod 600 signing_secret.txt

# View the secret (copy for client configuration)
cat signing_secret.txt
```

### Run with Docker

```bash
# Pull the latest image
docker pull bolabaden/kotormodsync-telemetry-auth:latest

# Run with secret file
docker run -d \
  --name telemetry-auth \
  -p 8080:8080 \
  -v $(pwd)/signing_secret.txt:/run/secrets/signing_secret:ro \
  bolabaden/kotormodsync-telemetry-auth:latest

# Check health
curl http://localhost:8080/health
```

## Docker Compose

### Standalone Deployment

```yaml
version: '3.8'

services:
  telemetry-auth:
    image: bolabaden/kotormodsync-telemetry-auth:latest
    container_name: telemetry-auth
    ports:
      - "8080:8080"
    secrets:
      - signing_secret
    environment:
      AUTH_SERVICE_PORT: 8080
      KOTORMODSYNC_SECRET_FILE: /run/secrets/signing_secret
      REQUIRE_AUTH: "true"
      MAX_TIMESTAMP_DRIFT: "300"
    healthcheck:
      test: ["CMD-SHELL", "wget -qO- http://localhost:8080/health || exit 1"]
      interval: 30s
      timeout: 5s
      retries: 3
    restart: unless-stopped

secrets:
  signing_secret:
    file: ./signing_secret.txt
```

Deploy:
```bash
docker compose up -d
```

### With Traefik ForwardAuth

```yaml
version: '3.8'

services:
  traefik:
    image: traefik:v2.10
    command:
      - "--api.insecure=true"
      - "--providers.docker=true"
      - "--entrypoints.web.address=:80"
      - "--entrypoints.websecure.address=:443"
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock:ro

  telemetry-auth:
    image: bolabaden/kotormodsync-telemetry-auth:latest
    secrets:
      - signing_secret
    environment:
      KOTORMODSYNC_SECRET_FILE: /run/secrets/signing_secret
    labels:
      - "traefik.enable=false"  # Internal service only

  otel-collector:
    image: otel/opentelemetry-collector-contrib:latest
    ports:
      - "4318:4318"
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.otlp.rule=Host(`otlp.example.com`)"
      - "traefik.http.routers.otlp.entrypoints=websecure"
      - "traefik.http.routers.otlp.tls=true"
      # ForwardAuth middleware
      - "traefik.http.middlewares.telemetry-auth.forwardauth.address=http://telemetry-auth:8080"
      - "traefik.http.middlewares.telemetry-auth.forwardauth.trustForwardHeader=true"
      - "traefik.http.routers.otlp.middlewares=telemetry-auth@docker"
      - "traefik.http.services.otlp.loadbalancer.server.port=4318"

secrets:
  signing_secret:
    file: ./signing_secret.txt
```

## Kubernetes

### Deployment

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: kotormodsync-signing-secret
type: Opaque
stringData:
  signing-secret: "your-64-character-hex-secret-here"
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: telemetry-auth
  labels:
    app: telemetry-auth
spec:
  replicas: 2
  selector:
    matchLabels:
      app: telemetry-auth
  template:
    metadata:
      labels:
        app: telemetry-auth
    spec:
      containers:
      - name: telemetry-auth
        image: bolabaden/kotormodsync-telemetry-auth:latest
        ports:
        - containerPort: 8080
          name: http
        env:
        - name: AUTH_SERVICE_PORT
          value: "8080"
        - name: KOTORMODSYNC_SECRET_FILE
          value: /run/secrets/signing_secret
        - name: REQUIRE_AUTH
          value: "true"
        - name: MAX_TIMESTAMP_DRIFT
          value: "300"
        volumeMounts:
        - name: secret
          mountPath: /run/secrets
          readOnly: true
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
        resources:
          requests:
            memory: "32Mi"
            cpu: "100m"
          limits:
            memory: "128Mi"
            cpu: "500m"
      volumes:
      - name: secret
        secret:
          secretName: kotormodsync-signing-secret
          items:
          - key: signing-secret
            path: signing_secret
---
apiVersion: v1
kind: Service
metadata:
  name: telemetry-auth
spec:
  selector:
    app: telemetry-auth
  ports:
  - port: 8080
    targetPort: 8080
    name: http
  type: ClusterIP
```

Deploy:
```bash
kubectl apply -f k8s-deployment.yaml
```

### Ingress with ForwardAuth

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: otlp-ingress
  annotations:
    # Traefik ForwardAuth
    traefik.ingress.kubernetes.io/router.middlewares: default-telemetry-auth@kubernetescrd
spec:
  rules:
  - host: otlp.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: otel-collector
            port:
              number: 4318
---
apiVersion: traefik.containo.us/v1alpha1
kind: Middleware
metadata:
  name: telemetry-auth
spec:
  forwardAuth:
    address: http://telemetry-auth:8080
    trustForwardHeader: true
```

## Traefik Integration

### Dynamic Configuration

```yaml
# traefik-dynamic.yml
http:
  middlewares:
    telemetry-auth:
      forwardAuth:
        address: "http://telemetry-auth:8080"
        trustForwardHeader: true
        authResponseHeaders:
          - "X-Auth-Status"
    
    rate-limit:
      rateLimit:
        average: 10
        burst: 20
        period: 1s
  
  routers:
    otlp:
      rule: "Host(`otlp.example.com`)"
      service: otel-collector
      middlewares:
        - telemetry-auth
        - rate-limit
      tls:
        certResolver: letsencrypt
  
  services:
    otel-collector:
      loadBalancer:
        servers:
          - url: "http://otel-collector:4318"
```

## Production Considerations

### Security

1. **Use HTTPS/TLS:**
   - Always use TLS for external endpoints
   - Use trusted certificates (Let's Encrypt, etc.)

2. **Secret Management:**
   - Use Docker secrets or Kubernetes secrets
   - Never commit secrets to git
   - Rotate secrets every 90 days

3. **Network Isolation:**
   - Keep auth service internal (not publicly accessible)
   - Use private networks/VPCs
   - Implement firewall rules

4. **Rate Limiting:**
   - Implement at reverse proxy level
   - Start with 10 req/s per IP
   - Adjust based on legitimate traffic

### High Availability

1. **Multiple Replicas:**
   ```yaml
   replicas: 3  # Run at least 3 instances
   ```

2. **Health Checks:**
   ```yaml
   healthcheck:
     test: ["CMD-SHELL", "wget -qO- http://localhost:8080/health"]
     interval: 10s
     timeout: 3s
     retries: 3
     start_period: 5s
   ```

3. **Resource Limits:**
   ```yaml
   resources:
     requests:
       memory: "32Mi"
       cpu: "100m"
     limits:
       memory: "128Mi"
       cpu: "500m"
   ```

### Performance Tuning

1. **Horizontal Scaling:**
   - Service is stateless, scales horizontally
   - Each instance can handle 1000+ req/s
   - Scale based on CPU usage

2. **Connection Pooling:**
   - Use load balancer with connection pooling
   - Enable keep-alive connections

3. **Caching:**
   - Service doesn't require caching
   - Signature validation is fast (< 5ms)

## Monitoring

### Metrics (Future Enhancement)

```yaml
# Prometheus scrape config
scrape_configs:
  - job_name: 'telemetry-auth'
    static_configs:
      - targets: ['telemetry-auth:8080']
    metrics_path: '/metrics'
```

### Logs

```bash
# Docker Compose
docker compose logs -f telemetry-auth

# Kubernetes
kubectl logs -f deployment/telemetry-auth

# Filter for auth failures
docker compose logs telemetry-auth | grep AUTH_FAILED
```

### Alerts

```yaml
# Alertmanager rule
groups:
  - name: telemetry_auth
    rules:
      - alert: HighAuthFailureRate
        expr: |
          rate(telemetry_auth_failed_total[5m]) 
          / 
          rate(telemetry_auth_total[5m]) > 0.2
        for: 10m
        annotations:
          summary: "High authentication failure rate"
          description: "Over 20% of auth requests failing"
      
      - alert: TelemetryAuthDown
        expr: up{job="telemetry-auth"} == 0
        for: 5m
        annotations:
          summary: "Telemetry auth service is down"
```

## Troubleshooting

### Service Won't Start

**Check logs:**
```bash
docker compose logs telemetry-auth
```

**Common issues:**
- Secret file not mounted: Verify volume/secret mount
- Port already in use: Check `lsof -i :8080`
- Permission denied: Ensure secret file is readable

### Authentication Always Fails

**Check secret matches:**
```bash
# Server
cat signing_secret.txt

# Client
echo $KOTORMODSYNC_SIGNING_SECRET
```

**Verify signature computation:**
```bash
# Test signature manually
SECRET="your-secret-here"
MESSAGE="POST|/v1/metrics|$(date +%s)|test-session"
echo -n "$MESSAGE" | openssl dgst -sha256 -hmac "$SECRET"
```

### High Latency

**Check resource usage:**
```bash
docker stats telemetry-auth
```

**Solutions:**
- Increase CPU limits
- Add more replicas
- Check network latency to auth service

### Clock Skew Issues

**Symptoms:** Legitimate requests rejected with "timestamp too old"

**Solutions:**
1. Sync system clocks: `ntpdate` or `w32tm /resync`
2. Increase `MAX_TIMESTAMP_DRIFT` (not recommended)
3. Check client clock settings

## Backup & Disaster Recovery

### Secret Backup

```bash
# Backup secret securely
gpg --encrypt --recipient your@email.com signing_secret.txt

# Store encrypted backup in secure location
aws s3 cp signing_secret.txt.gpg s3://secure-backup-bucket/
```

### Service Recovery

```bash
# Restore from backup
aws s3 cp s3://secure-backup-bucket/signing_secret.txt.gpg .
gpg --decrypt signing_secret.txt.gpg > signing_secret.txt

# Restart service
docker compose up -d telemetry-auth
```

## Upgrading

### Rolling Update (Kubernetes)

```bash
# Update image
kubectl set image deployment/telemetry-auth \
  telemetry-auth=bolabaden/kotormodsync-telemetry-auth:v2.0.0

# Monitor rollout
kubectl rollout status deployment/telemetry-auth
```

### Zero-Downtime Update (Docker Compose)

```bash
# Pull new image
docker compose pull telemetry-auth

# Recreate with minimal downtime
docker compose up -d --no-deps --build telemetry-auth
```

## Support

- **Documentation:** https://github.com/YOUR_ORG/kotormodsync-telemetry-auth
- **Issues:** https://github.com/YOUR_ORG/kotormodsync-telemetry-auth/issues
- **Security:** security@bolabaden.org

