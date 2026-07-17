#!/usr/bin/env bash
# -----------------------------------------------------------------------
# Copyright (c) DCSV. All rights reserved.
# -----------------------------------------------------------------------

# =============================================================================
# run-mtls-proof.sh — run the real-socket mutual-TLS harness proof on Linux
#
# The MutualTlsSignerHarnessTests bind a real Kestrel HTTPS endpoint on a
# loopback ephemeral port and drive the SHIPPED AddD2MutualTls require-and-
# validate path over a genuine TLS handshake. The six client-cert-PRESENTING
# cases (ValidLeaf_DirectPresentation, ShippedClient_FullHandshake, WrongCaLeaf,
# ExpiredLeaf, ForeignTrustDomainSan, UnknownWorkload) skip on Windows: Schannel
# cannot build an SslStreamCertificateContext for a leaf chaining to a private
# CA without installing the root into the OS store, which the harness refuses to
# do (a clean-box property, not a harness defect). The deployment target is
# Linux/OpenSSL, where those cases EXECUTE — presenting the full leaf ->
# intermediate chain on the wire and validating it over a real socket.
#
# This script builds a small Linux SDK image (server/ + contracts/ only; the
# repo `.dockerignore` excludes obj/bin so the Windows host's build artifacts
# never seed the Linux build) and runs the harness filter inside it. The
# container is removed on exit (--rm). It does NOT touch the host build tree
# and needs no Postgres / Redis / RabbitMQ — the harness is self-contained
# loopback.
#
# Usage (from anywhere):
#   bash tools/scripts/run-mtls-proof.sh
#
# Exit code: 0 = all harness cases passed in the container; non-zero = failure.
# =============================================================================

set -euo pipefail

ROOT_DIR=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
cd "$ROOT_DIR"

IMAGE_TAG="d2-mtls-proof:local"
DOCKERFILE="infra/docker/Dockerfile.mtls-proof"

echo "─── Building the Linux mTLS-proof image (${IMAGE_TAG}) ───"
echo "    context: repo root (server/ + contracts/; obj/bin excluded via .dockerignore)"
docker build -f "$DOCKERFILE" -t "$IMAGE_TAG" .

echo ""
echo "─── Running the real-socket mTLS harness on Linux/OpenSSL ───"
echo "    filter: --filter-class *MutualTlsSignerHarnessTests* (xunit.v3 / MTP native)"
echo "    (all 7 cases execute here — the 6 cert-presenting cases skip only on Windows)"
echo ""
docker run --rm "$IMAGE_TAG"
