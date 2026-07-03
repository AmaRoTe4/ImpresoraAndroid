#!/usr/bin/env bash
set -euo pipefail

# ──────────────────────────────────────────────
# PrintAgent Android — Release Keystore Generator
# ──────────────────────────────────────────────
# Generates a keystore for APK signing and prints
# the BASE64-encoded value + secrets to configure.
#
# Usage:
#   chmod +x scripts/setup-keystore.sh
#   ./scripts/setup-keystore.sh
#
# Then copy-paste the output values into
# GitHub → repo → Settings → Secrets and variables → Actions
# ──────────────────────────────────────────────

KEYSTORE_FILE="release.keystore"
ALIAS="printagent"
VALIDITY_DAYS=9125  # 25 years
KEY_ALG="RSA"
KEY_SIZE=2048

echo ""
echo "=== PrintAgent Android — Keystore Setup ==="
echo ""

read -rsp "Keystore password: " STORE_PASS
echo ""
read -rsp "Confirm password: " CONFIRM_PASS
echo ""

if [ "$STORE_PASS" != "$CONFIRM_PASS" ]; then
  echo "ERROR: Passwords do not match."
  exit 1
fi

KEY_PASS="$STORE_PASS"  # same as store pass (simpler)

read -rp "Your name (CN): " CN
read -rp "Org unit (OU) [Press Enter for default]: " OU
read -rp "Org name (O) [Press Enter for default]: " O
read -rp "City (L) [Press Enter for default]: " L
read -rp "Country code (C) [Press Enter for default]: " C

OU="${OU:-PrintAgent}"
O="${O:-PrintAgent}"
L="${L:-Buenos Aires}"
C="${C:-AR}"

keytool -genkey -v \
  -keystore "$KEYSTORE_FILE" \
  -alias "$ALIAS" \
  -keyalg "$KEY_ALG" \
  -keysize "$KEY_SIZE" \
  -validity "$VALIDITY_DAYS" \
  -storepass "$STORE_PASS" \
  -keypass "$KEY_PASS" \
  -dname "CN=$CN, OU=$OU, O=$O, L=$L, C=$C"

echo ""
echo "=== Keystore created: $KEYSTORE_FILE ==="
echo ""

BASE64=$(base64 -w0 "$KEYSTORE_FILE")

echo "=========================================="
echo "  ADD THESE TO GITHUB SECRETS"
echo "=========================================="
echo ""
echo "  ANDROID_KEYSTORE_BASE64 (copy the full value below):"
echo ""
echo "$BASE64"
echo ""
echo "=========================================="
echo "  ANDROID_KEYSTORE_PASSWORD: $STORE_PASS"
echo "  ANDROID_KEY_ALIAS:         $ALIAS"
echo "  ANDROID_KEY_PASSWORD:      $KEY_PASS"
echo "=========================================="
echo ""
echo "Keystore file '$KEYSTORE_FILE' was created locally."
echo "Keep it safe — you'll need it for future updates."
echo "Without it, you CANNOT upload a new version to the Play Store."
