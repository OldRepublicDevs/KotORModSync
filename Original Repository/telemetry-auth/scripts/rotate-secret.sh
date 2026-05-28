#!/bin/bash
#
# Rotate the signing secret
#
# This script:
# 1. Generates a new secret
# 2. Backs up the old secret
# 3. Updates the service
# 4. Provides instructions for updating GitHub Actions
#

set -e

SECRET_FILE="${1:-signing_secret.txt}"
BACKUP_FILE="${SECRET_FILE}.backup-$(date +%Y%m%d-%H%M%S)"

echo "======================================================================"
echo "KOTORModSync Signing Secret Rotation"
echo "======================================================================"
echo ""

# Check if current secret exists
if [ ! -f "$SECRET_FILE" ]; then
    echo "❌ Error: Current secret file not found: $SECRET_FILE"
    echo "   Run ./scripts/generate-secret.sh first"
    exit 1
fi

# Backup old secret
echo "💾 Backing up current secret..."
cp "$SECRET_FILE" "$BACKUP_FILE"
chmod 600 "$BACKUP_FILE"
echo "   Backup saved to: $BACKUP_FILE"
echo ""

# Generate new secret
echo "🔐 Generating new secret..."
openssl rand -hex 32 > "$SECRET_FILE"
chmod 600 "$SECRET_FILE"
echo "✅ New secret generated"
echo ""

# Display new secret
echo "📋 Your NEW signing secret:"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
cat "$SECRET_FILE"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Restart service if running
if command -v docker &> /dev/null; then
    if docker ps | grep -q telemetry-auth; then
        echo "🔄 Restarting service..."
        docker compose restart telemetry-auth || docker restart telemetry-auth || true
        echo "✅ Service restarted"
        echo ""
    fi
fi

echo "⚠️  CRITICAL NEXT STEPS:"
echo ""
echo "1. Update GitHub Actions secret:"
echo "   - Go to: https://github.com/YOUR_ORG/KOTORModSync/settings/secrets/actions"
echo "   - Edit: KOTORMODSYNC_SIGNING_SECRET"
echo "   - Paste the new secret above"
echo ""
echo "2. Publish new KOTORModSync release:"
echo "   - Create a new release on GitHub"
echo "   - This will embed the new secret in official builds"
echo ""
echo "3. Monitor authentication logs:"
echo "   docker compose logs -f telemetry-auth | grep AUTH_FAILED"
echo ""
echo "4. Notify users to update:"
echo "   - Old versions will stop sending telemetry (expected)"
echo "   - Users should update to the new version"
echo ""
echo "5. Secure backup:"
echo "   - Store backup in secure location: $BACKUP_FILE"
echo "   - Delete after 30 days grace period"
echo ""
echo "======================================================================"
echo ""
echo "📊 Expected behavior:"
echo "   - New clients (with new secret): ✅ Authentication succeeds"
echo "   - Old clients (with old secret): ❌ Authentication fails"
echo ""
echo "💡 Tip: Monitor failure rate for next 24 hours to ensure smooth transition"
echo ""

