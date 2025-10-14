#!/usr/bin/env python3
"""
HMAC Authentication Service for KOTORModSync Telemetry

This service validates HMAC-SHA256 signatures in the X-KMS-Signature header
to ensure only authentic KOTORModSync clients can send telemetry data.

Used with Traefik ForwardAuth middleware.
"""

import hmac
import hashlib
import os
import time
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs


# Load signing secret from environment or file
SIGNING_SECRET = os.getenv('KOTORMODSYNC_SIGNING_SECRET')
if not SIGNING_SECRET:
    secret_file = os.getenv('KOTORMODSYNC_SECRET_FILE', '/run/secrets/kotormodsync_signing_secret')
    try:
        with open(secret_file, 'r') as f:
            SIGNING_SECRET = f.read().strip()
    except FileNotFoundError:
        print(f"WARNING: No signing secret found. Set KOTORMODSYNC_SIGNING_SECRET env var or create {secret_file}")
        SIGNING_SECRET = None

# Configuration
MAX_TIMESTAMP_DRIFT = 300  # 5 minutes
PORT = int(os.getenv('AUTH_SERVICE_PORT', '8080'))
REQUIRE_AUTH = os.getenv('REQUIRE_AUTH', 'true').lower() == 'true'


class AuthHandler(BaseHTTPRequestHandler):
    """
    Traefik ForwardAuth handler that validates HMAC signatures.
    
    Expected headers from KOTORModSync:
    - X-KMS-Signature: HMAC-SHA256 hex digest of request
    - X-KMS-Timestamp: Unix timestamp in seconds
    - X-KMS-Client-Version: KOTORModSync version
    - X-KMS-Session-ID: Session identifier
    
    Signature is computed as:
    HMAC-SHA256(secret, "POST|/v1/metrics|{timestamp}|{session_id}")
    """
    
    def do_GET(self):
        """Health check endpoint"""
        if self.path == '/health':
            self.send_response(200)
            self.send_header('Content-Type', 'text/plain')
            self.end_headers()
            self.wfile.write(b'OK')
            return
        
        # All other GET requests denied
        self.send_error(405, "Method Not Allowed")
    
    def do_POST(self):
        """Validate authentication for POST requests"""
        
        # If authentication is disabled, allow all requests
        if not REQUIRE_AUTH or not SIGNING_SECRET:
            self.send_response(200)
            self.end_headers()
            return
        
        # Extract headers
        signature = self.headers.get('X-KMS-Signature')
        timestamp = self.headers.get('X-KMS-Timestamp')
        session_id = self.headers.get('X-KMS-Session-ID', '')
        client_version = self.headers.get('X-KMS-Client-Version', 'unknown')
        forwarded_uri = self.headers.get('X-Forwarded-Uri', self.path)
        
        # Check required headers present
        if not signature or not timestamp:
            self.log_auth_failure('missing_headers', client_version, session_id)
            self.send_error(401, "Missing authentication headers")
            return
        
        # Validate timestamp (prevent replay attacks)
        try:
            request_time = int(timestamp)
            current_time = int(time.time())
            
            if abs(current_time - request_time) > MAX_TIMESTAMP_DRIFT:
                self.log_auth_failure('timestamp_drift', client_version, session_id, 
                                     f"drift={current_time - request_time}s")
                self.send_error(401, "Request timestamp too old or in future")
                return
        except ValueError:
            self.log_auth_failure('invalid_timestamp', client_version, session_id)
            self.send_error(401, "Invalid timestamp format")
            return
        
        # Compute expected signature
        # Format: "POST|{path}|{timestamp}|{session_id}"
        path = urlparse(forwarded_uri).path
        message = f"POST|{path}|{timestamp}|{session_id}"
        expected_signature = hmac.new(
            SIGNING_SECRET.encode('utf-8'),
            message.encode('utf-8'),
            hashlib.sha256
        ).hexdigest()
        
        # Compare signatures (constant-time comparison to prevent timing attacks)
        if not hmac.compare_digest(signature.lower(), expected_signature.lower()):
            self.log_auth_failure('invalid_signature', client_version, session_id)
            self.send_error(403, "Invalid signature")
            return
        
        # Authentication successful
        self.log_auth_success(client_version, session_id)
        self.send_response(200)
        self.send_header('X-Auth-Status', 'valid')
        self.end_headers()
    
    def log_auth_success(self, version, session_id):
        """Log successful authentication"""
        print(f"[AUTH_SUCCESS] version={version} session={session_id[:8]}... ip={self.client_address[0]}")
    
    def log_auth_failure(self, reason, version, session_id, extra=''):
        """Log failed authentication attempt"""
        session_short = session_id[:8] + '...' if session_id else 'none'
        print(f"[AUTH_FAILED] reason={reason} version={version} session={session_short} ip={self.client_address[0]} {extra}")
    
    def log_message(self, format, *args):
        """Override to customize logging format"""
        print(f"[{self.log_date_time_string()}] {format % args}")


def main():
    """Start the authentication service"""
    if not SIGNING_SECRET:
        print("=" * 70)
        print("WARNING: Running without signature verification!")
        print("Set KOTORMODSYNC_SIGNING_SECRET or provide secret file")
        print("=" * 70)
    else:
        print(f"HMAC authentication service starting on port {PORT}")
        print(f"Timestamp drift tolerance: {MAX_TIMESTAMP_DRIFT}s")
        print(f"Authentication required: {REQUIRE_AUTH}")
    
    server = HTTPServer(('0.0.0.0', PORT), AuthHandler)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down...")
        server.server_close()


if __name__ == '__main__':
    main()

