/**
 * Identifies infrastructure endpoints (health checks, metrics) that should
 * bypass request enrichment, rate limiting, and other business middleware.
 *
 * Mirrors the .NET `D2.Shared.RequestEnrichment.Default.InfrastructurePaths`
 * helper. Keep the path list in sync — divergence between the two stacks
 * means a probe that's free on .NET costs rate-limit budget on Node (or
 * vice-versa), which silently degrades observability under load.
 *
 * Used by:
 *   - rate-limit middleware (skip Redis sliding-window writes)
 *   - request-enrichment middleware (skip WhoIs lookup)
 *   - JWT auth middleware (don't gate health probes on a JWT)
 */
const INFRASTRUCTURE_PREFIXES = ["/health", "/alive", "/metrics", "/ready", "/api/health"];

/**
 * Returns `true` if the request path targets an infrastructure endpoint that
 * should be excluded from business middleware processing.
 *
 * Case-insensitive prefix match — matches both `/health` and `/health/db`.
 */
export function isInfrastructurePath(path: string): boolean {
  const lower = path.toLowerCase();
  for (const prefix of INFRASTRUCTURE_PREFIXES) {
    if (lower === prefix || lower.startsWith(`${prefix}/`)) {
      return true;
    }
  }
  return false;
}
