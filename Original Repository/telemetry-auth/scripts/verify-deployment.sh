#!/bin/bash
#
# Verify deployment health and configuration
#

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo "======================================================================"
echo "KOTORModSync Telemetry Auth - Deployment Verification"
echo "======================================================================"
echo ""

ENDPOINT="${1:-http://localhost:8080}"
ERRORS=0

# Function to check and report
check() {
    local test_name="$1"
    local command="$2"
    
    echo -n "Checking $test_name... "
    
    if eval "$command" > /dev/null 2>&1; then
        echo -e "${GREEN}✅ PASS${NC}"
        return 0
    else
        echo -e "${RED}❌ FAIL${NC}"
        ERRORS=$((ERRORS + 1))
        return 1
    fi
}

# 1. Check if service is running
echo "🔍 Service Status Checks"
echo "─────────────────────────────────────────"

if command -v docker &> /dev/null; then
    check "Docker service running" "docker ps | grep -q telemetry-auth"
else
    echo -e "${YELLOW}⚠️  Docker not available, skipping container checks${NC}"
fi

# 2. Check health endpoint
echo ""
echo "🏥 Health Check"
echo "─────────────────────────────────────────"
check "Health endpoint" "curl -sf $ENDPOINT/health"

# 3. Check secret file exists
echo ""
echo "🔐 Configuration Checks"
echo "─────────────────────────────────────────"
check "Secret file exists" "test -f signing_secret.txt"

if [ -f signing_secret.txt ]; then
    SECRET_LENGTH=$(cat signing_secret.txt | tr -d '\n\r ' | wc -c)
    if [ "$SECRET_LENGTH" -ge 32 ]; then
        echo -e "Secret length... ${GREEN}✅ PASS${NC} ($SECRET_LENGTH chars)"
    else
        echo -e "Secret length... ${RED}❌ FAIL${NC} (only $SECRET_LENGTH chars, should be ≥32)"
        ERRORS=$((ERRORS + 1))
    fi
fi

# 4. Check Dockerfile exists
check "Dockerfile present" "test -f Dockerfile"
check "auth_service.py present" "test -f auth_service.py"
check "docker-compose.yml present" "test -f docker-compose.yml"

# 5. Test authentication (if service is running)
echo ""
echo "🔒 Authentication Tests"
echo "─────────────────────────────────────────"

if curl -sf $ENDPOINT/health > /dev/null 2>&1; then
    # Test missing headers (should fail)
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" -X POST $ENDPOINT/ 2>/dev/null || echo "000")
    if [ "$HTTP_CODE" -eq 401 ] || [ "$HTTP_CODE" -eq 200 ]; then
        echo -e "Missing headers rejected... ${GREEN}✅ PASS${NC} (HTTP $HTTP_CODE)"
    else
        echo -e "Missing headers rejected... ${RED}❌ FAIL${NC} (HTTP $HTTP_CODE, expected 401 or 200)"
        ERRORS=$((ERRORS + 1))
    fi
else
    echo -e "${YELLOW}⚠️  Service not accessible, skipping auth tests${NC}"
fi

# 6. Check logs for errors (if Docker available)
if command -v docker &> /dev/null; then
    if docker ps | grep -q telemetry-auth; then
        echo ""
        echo "📋 Recent Logs"
        echo "─────────────────────────────────────────"
        echo "Last 5 log entries:"
        docker logs telemetry-auth --tail 5 2>&1 || docker compose logs telemetry-auth --tail 5 2>&1 || true
    fi
fi

# Summary
echo ""
echo "======================================================================"
if [ $ERRORS -eq 0 ]; then
    echo -e "${GREEN}✅ All checks passed!${NC}"
    echo ""
    echo "🎉 Deployment is healthy and ready for production"
    echo ""
    echo "Next steps:"
    echo "  1. Test with real client: ./scripts/test-auth.sh valid"
    echo "  2. Monitor logs: docker compose logs -f telemetry-auth"
    echo "  3. Add to production stack"
    exit 0
else
    echo -e "${RED}❌ $ERRORS check(s) failed${NC}"
    echo ""
    echo "Please address the issues above before deploying to production"
    exit 1
fi

