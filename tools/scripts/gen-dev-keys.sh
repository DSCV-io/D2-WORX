#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------

# =============================================================================
# gen-dev-keys.sh — Generate dev keys for D2-WORX local development
#
# Generates:
#   - secrets/auth/root.key            Root key (encrypts all other keys at rest in auth_db)
#   - secrets/auth/{domain}-{kid}.key  Per-domain message-payload encryption keys
#                                      (per V2.md §5.7 — JWKS-style keyring)
#   - secrets/keycustodian/root.key    Root key for the Edge KeyCustodian module
#
# All output goes into ./secrets/ which is gitignored AND Claude-deny-ruled
# (Read/Write/Edit blocked at the system level — see .claude/settings.json
# and V2.md §12).
#
# Re-running this script:
#   - Skips existing keys (idempotent — safe to run multiple times)
#   - Use --rotate <domain> to generate a new active kid for a domain
#   - Use --force to regenerate ALL keys (DESTRUCTIVE — invalidates all
#     existing encrypted data)
#
# Usage:
#   ./tools/scripts/gen-dev-keys.sh                    # generate any missing keys
#   ./tools/scripts/gen-dev-keys.sh --rotate audit     # rotate audit domain
#   ./tools/scripts/gen-dev-keys.sh --force            # regenerate everything
# =============================================================================

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SECRETS_DIR="${REPO_ROOT}/secrets"
AUTH_KEYS_DIR="${SECRETS_DIR}/auth"
KEYCUSTODIAN_KEYS_DIR="${SECRETS_DIR}/keycustodian"

# Encryption domains per V2.md §5.7 — add new domains here as services adopt them.
DOMAINS=("audit" "notifications" "courier")

# Quarter-based kid format per V2.md §5.4: {domain}-{yyyy}q{n}
current_kid_suffix() {
  local year quarter
  year=$(date +%Y)
  quarter=$(( ($(date +%-m) - 1) / 3 + 1 ))
  echo "${year}q${quarter}"
}

# Generate a 32-byte (256-bit) random key as hex.
gen_key() {
  openssl rand -hex 32
}

mkdir -p "${AUTH_KEYS_DIR}" "${KEYCUSTODIAN_KEYS_DIR}"
chmod 700 "${SECRETS_DIR}" "${AUTH_KEYS_DIR}" "${KEYCUSTODIAN_KEYS_DIR}"

# ---------- Root key --------------------------------------------------------
if [[ "${1:-}" == "--force" ]] || [[ ! -f "${AUTH_KEYS_DIR}/root.key" ]]; then
  gen_key > "${AUTH_KEYS_DIR}/root.key"
  chmod 600 "${AUTH_KEYS_DIR}/root.key"
  echo "✓ Generated root key: secrets/auth/root.key"
else
  echo "  Skipped root key (already exists)"
fi

# ---------- KeyCustodian root key ------------------------------------------
if [[ "${1:-}" == "--force" ]] || [[ ! -f "${KEYCUSTODIAN_KEYS_DIR}/root.key" ]]; then
  gen_key > "${KEYCUSTODIAN_KEYS_DIR}/root.key"
  chmod 600 "${KEYCUSTODIAN_KEYS_DIR}/root.key"
  echo "✓ Generated KeyCustodian root key: secrets/keycustodian/root.key"
else
  echo "  Skipped KeyCustodian root key (already exists)"
fi

# ---------- Per-domain message-payload keys --------------------------------
KID_SUFFIX="$(current_kid_suffix)"

for domain in "${DOMAINS[@]}"; do
  KID="${domain}-${KID_SUFFIX}"
  KEY_FILE="${AUTH_KEYS_DIR}/${KID}.key"

  if [[ "${1:-}" == "--rotate" && "${2:-}" == "${domain}" ]]; then
    # Forced rotation — generate a NEW kid even if current already exists.
    # Keep the old one in place (multi-key keyring per V2.md §5.4).
    NEW_KID="${domain}-$(date +%s)"
    NEW_FILE="${AUTH_KEYS_DIR}/${NEW_KID}.key"
    gen_key > "${NEW_FILE}"
    chmod 600 "${NEW_FILE}"
    echo "✓ Rotated ${domain}: new kid ${NEW_KID} (old kid ${KID} retained for grace window)"
    continue
  fi

  if [[ "${1:-}" == "--force" ]] || [[ ! -f "${KEY_FILE}" ]]; then
    gen_key > "${KEY_FILE}"
    chmod 600 "${KEY_FILE}"
    echo "✓ Generated ${KID}: secrets/auth/${KID}.key"
  else
    echo "  Skipped ${KID} (already exists)"
  fi
done

echo
echo "Dev keys ready under: ${SECRETS_DIR}/"
echo "These files are gitignored AND Claude-deny-ruled per V2.md §12."
