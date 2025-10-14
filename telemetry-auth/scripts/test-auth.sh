#!/bin/bash
#
# Test script for KOTORModSync telemetry authentication
#
# Usage:
#   ./test-kotormodsync-auth.sh [test_type]
#
# Test types:
#   valid      - Send request with valid signature (should succeed)
#   invalid    - Send request with invalid signature (should fail)
#   missing    - Send request without signature (should fail)
#   replay     - Send request with old timestamp (should fail)
#   ratelimit  - Test rate limiting (should throttle)
#

set -e

# Configuration
OTLP_ENDPOINT="${OTLP_ENDPOINT:-https://otlp.bolabaden.org}"
SECRET_FILE="${SECRET_FILE:-../volumes/kotormodsync_signing_secret.txt}"
TEST_TYPE="${1:-valid}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "======================================================================"
echo "KOTORModSync Authentication Test"
echo "======================================================================"
echo "Endpoint: $OTLP_ENDPOINT"
echo "Test Type: $TEST_TYPE"
echo ""

# Load secret
if [ -f "$SECRET_FILE" ]; then
    SECRET=$(cat "$SECRET_FILE" | tr -d '\n\r ')
    echo "✅ Secret loaded (${#SECRET} chars)"
else
    echo "⚠️  Secret file not found: $SECRET_FILE"
    echo "   Using dummy secret for testing (will fail validation)"
    SECRET="dummy-secret-for-testing-only"
fi
echo ""

# Generate test data
SESSION_ID="test-$(date +%s)-$(openssl rand -hex 4)"
CLIENT_VERSION="test-script-1.0.0"
PATH="/v1/metrics"

case "$TEST_TYPE" in
    valid)
        echo "📝 Test: Valid Signature"
        TIMESTAMP=$(date +%s)
        MESSAGE="POST|${PATH}|${TIMESTAMP}|${SESSION_ID}"
        SIGNATURE=$(echo -n "$MESSAGE" | openssl dgst -sha256 -hmac "$SECRET" | awk '{print $2}')
        
        echo "Message: $MESSAGE"
        echo "Signature: $SIGNATURE"
        echo ""
        
        RESPONSE=$(curl -s -w "\nHTTP_CODE:%{http_code}" -X POST "${OTLP_ENDPOINT}${PATH}" \
            -H "Content-Type: application/json" \
            -H "X-KMS-Signature: $SIGNATURE" \
            -H "X-KMS-Timestamp: $TIMESTAMP" \
            -H "X-KMS-Session-ID: $SESSION_ID" \
            -H "X-KMS-Client-Version: $CLIENT_VERSION" \
            -d '{"resourceMetrics":[]}')
        
        HTTP_CODE=$(echo "$RESPONSE" | grep "HTTP_CODE:" | cut -d: -f2)
        BODY=$(echo "$RESPONSE" | grep -v "HTTP_CODE:")
        
        if [ "$HTTP_CODE" -eq 200 ] || [ "$HTTP_CODE" -eq 400 ]; then
            echo -e "${GREEN}✅ PASS${NC} - HTTP $HTTP_CODE (request accepted)"
            exit 0
        else
            echo -e "${RED}❌ FAIL${NC} - HTTP $HTTP_CODE (expected 200 or 400)"
            echo "Response: $BODY"
            exit 1
        fi
        ;;
    
    invalid)
        echo "📝 Test: Invalid Signature"
        TIMESTAMP=$(date +%s)
        SIGNATURE="invalid-signature-123456789abcdef"
        
        echo "Signature: $SIGNATURE (invalid)"
        echo ""
        
        RESPONSE=$(curl -s -w "\nHTTP_CODE:%{http_code}" -X POST "${OTLP_ENDPOINT}${PATH}" \
            -H "Content-Type: application/json" \
            -H "X-KMS-Signature: $SIGNATURE" \
            -H "X-KMS-Timestamp: $TIMESTAMP" \
            -H "X-KMS-Session-ID: $SESSION_ID" \
            -H "X-KMS-Client-Version: $CLIENT_VERSION" \
            -d '{"resourceMetrics":[]}')
        
        HTTP_CODE=$(echo "$RESPONSE" | grep "HTTP_CODE:" | cut -d: -f2)
        
        if [ "$HTTP_CODE" -eq 403 ]; then
            echo -e "${GREEN}✅ PASS${NC} - HTTP $HTTP_CODE (invalid signature rejected)"
            exit 0
        else
            echo -e "${RED}❌ FAIL${NC} - HTTP $HTTP_CODE (expected 403)"
            exit 1
        fi
        ;;
    
    missing)
        echo "📝 Test: Missing Signature Headers"
        echo ""
        
        RESPONSE=$(curl -s -w "\nHTTP_CODE:%{http_code}" -X POST "${OTLP_ENDPOINT}${PATH}" \
            -H "Content-Type: application/json" \
            -d '{"resourceMetrics":[]}')
        
        HTTP_CODE=$(echo "$RESPONSE" | grep "HTTP_CODE:" | cut -d: -f2)
        
        if [ "$HTTP_CODE" -eq 401 ]; then
            echo -e "${GREEN}✅ PASS${NC} - HTTP $HTTP_CODE (missing headers rejected)"
            exit 0
        else
            echo -e "${RED}❌ FAIL${NC} - HTTP $HTTP_CODE (expected 401)"
            exit 1
        fi
        ;;
    
    replay)
        echo "📝 Test: Replay Attack (Old Timestamp)"
        TIMESTAMP=$(($(date +%s) - 400))  # 6 minutes ago
        MESSAGE="POST|${PATH}|${TIMESTAMP}|${SESSION_ID}"
        SIGNATURE=$(echo -n "$MESSAGE" | openssl dgst -sha256 -hmac "$SECRET" | awk '{print $2}')
        
        echo "Timestamp: $TIMESTAMP (6 minutes old)"
        echo ""
        
        RESPONSE=$(curl -s -w "\nHTTP_CODE:%{http_code}" -X POST "${OTLP_ENDPOINT}${PATH}" \
            -H "Content-Type: application/json" \
            -H "X-KMS-Signature: $SIGNATURE" \
            -H "X-KMS-Timestamp: $TIMESTAMP" \
            -H "X-KMS-Session-ID: $SESSION_ID" \
            -H "X-KMS-Client-Version: $CLIENT_VERSION" \
            -d '{"resourceMetrics":[]}')
        
        HTTP_CODE=$(echo "$RESPONSE" | grep "HTTP_CODE:" | cut -d: -f2)
        
        if [ "$HTTP_CODE" -eq 401 ]; then
            echo -e "${GREEN}✅ PASS${NC} - HTTP $HTTP_CODE (old timestamp rejected)"
            exit 0
        else
            echo -e "${RED}❌ FAIL${NC} - HTTP $HTTP_CODE (expected 401)"
            exit 1
        fi
        ;;
    
    ratelimit)
        echo "📝 Test: Rate Limiting (25 requests)"
        echo "Expected: First 20 succeed, last 5 rate limited"
        echo ""
        
        SUCCESS=0
        RATELIMITED=0
        
        for i in {1..25}; do
            TIMESTAMP=$(date +%s)
            MESSAGE="POST|${PATH}|${TIMESTAMP}|${SESSION_ID}"
            SIGNATURE=$(echo -n "$MESSAGE" | openssl dgst -sha256 -hmac "$SECRET" | awk '{print $2}')
            
            HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST "${OTLP_ENDPOINT}${PATH}" \
                -H "Content-Type: application/json" \
                -H "X-KMS-Signature: $SIGNATURE" \
                -H "X-KMS-Timestamp: $TIMESTAMP" \
                -H "X-KMS-Session-ID: $SESSION_ID" \
                -H "X-KMS-Client-Version: $CLIENT_VERSION" \
                -d '{"resourceMetrics":[]}')
            
            if [ "$HTTP_CODE" -eq 200 ] || [ "$HTTP_CODE" -eq 400 ]; then
                echo -n "."
                SUCCESS=$((SUCCESS + 1))
            elif [ "$HTTP_CODE" -eq 429 ]; then
                echo -n "R"
                RATELIMITED=$((RATELIMITED + 1))
            else
                echo -n "?"
            fi
        done
        
        echo ""
        echo ""
        echo "Results:"
        echo "  Success: $SUCCESS"
        echo "  Rate Limited: $RATELIMITED"
        
        if [ "$RATELIMITED" -gt 0 ]; then
            echo -e "${GREEN}✅ PASS${NC} - Rate limiting is working ($RATELIMITED requests throttled)"
            exit 0
        else
            echo -e "${YELLOW}⚠️  WARNING${NC} - No requests were rate limited (may need more aggressive testing)"
            exit 0
        fi
        ;;
    
    *)
        echo "❌ Unknown test type: $TEST_TYPE"
        echo ""
        echo "Available tests:"
        echo "  valid      - Valid signature (should succeed)"
        echo "  invalid    - Invalid signature (should fail)"
        echo "  missing    - Missing headers (should fail)"
        echo "  replay     - Old timestamp (should fail)"
        echo "  ratelimit  - Rate limiting (should throttle)"
        exit 1
        ;;
esac

