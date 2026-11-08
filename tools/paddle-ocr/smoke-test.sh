#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${1:-http://127.0.0.1:5099}"
INVOICE="${2:?usage: smoke-test.sh [base-url] /path/to/invoice.png}"

if [ ! -f "$INVOICE" ]; then
  echo "invoice not found: $INVOICE" >&2
  exit 1
fi

RESP="$(curl -sS -X POST "${BASE_URL}/ocr" -F "file=@${INVOICE}")"
python3 -c '
import json, sys
data = json.loads(sys.argv[1])
text = data.get("rawText") or ""
if data.get("error"):
    raise SystemExit("OCR service error: " + data["error"])
if len(text) < 10:
    raise SystemExit("OCR returned empty/short text")
print("OCR OK, text length:", len(text))
print(text[:500])
' "$RESP"
