FROM python:3.11-alpine

WORKDIR /app

# Copy auth service
COPY auth_service.py /app/

# Create directory for secret file mount
RUN mkdir -p /run/secrets

# Run as non-root user
RUN addgroup -g 1001 authservice && \
    adduser -D -u 1001 -G authservice authservice && \
    chown -R authservice:authservice /app

USER authservice

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

EXPOSE 8080

CMD ["python3", "-u", "auth_service.py"]

