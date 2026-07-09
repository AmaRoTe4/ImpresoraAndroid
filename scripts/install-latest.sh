#!/usr/bin/env bash
# Baja el último APK Debug compilado en CI y lo instala en el teléfono conectado (USB o adb wireless ya emparejado).
set -euo pipefail

REPO="AmaRoTe4/ImpresoraAndroid"
ARTIFACT="printagent-debug"
DEST="$(mktemp -d)"

echo "Buscando último build exitoso en main..."
RUN_ID=$(gh run list --repo "$REPO" --branch main --status success --limit 1 --json databaseId --jq '.[0].databaseId')

if [ -z "$RUN_ID" ]; then
  echo "No hay ningún build exitoso todavía." >&2
  exit 1
fi

echo "Run $RUN_ID -> bajando artifact '$ARTIFACT'..."
gh run download "$RUN_ID" --repo "$REPO" -n "$ARTIFACT" -D "$DEST"

APK=$(find "$DEST" -name "*.apk" | head -1)
if [ -z "$APK" ]; then
  echo "No se encontró ningún .apk en el artifact." >&2
  exit 1
fi

echo "Instalando $APK ..."
adb install -r "$APK"

echo "Listo."
