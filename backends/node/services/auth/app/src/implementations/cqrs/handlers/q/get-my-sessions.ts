import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { Complex } from "@d2/geo-client";
import type { WhoIsDTO } from "@d2/protos";
import type { IGetActiveSessionsByUserIdHandler } from "../../../../interfaces/repository/handlers/index.js";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Queries.GetMySessionsInput;
type Output = Queries.GetMySessionsOutput;
type EnrichedSession = Queries.EnrichedSession;

/**
 * Lists the user's active sessions, enriched with cross-service Geo WhoIs data
 * (city/country/ASN/network flags) for each session's `ipAddress`.
 *
 * geo-client `findWhoIs` is multi-tier cached (memory → Redis → DB), so calling
 * it once per unique IP per request is cheap. We dedupe by IP first to avoid
 * redundant calls when multiple sessions came from the same address.
 *
 * Each session is also flagged with `isCurrent` by comparing the row's `token`
 * against the caller-supplied current session cookie token.
 */
export class GetMySessions
  extends BaseHandler<Input, Output>
  implements Queries.IGetMySessionsHandler
{
  private readonly getActiveSessions: IGetActiveSessionsByUserIdHandler;
  private readonly findWhoIs: Complex.IFindWhoIsHandler;

  override get redaction() {
    return Queries.GET_MY_SESSIONS_REDACTION;
  }

  constructor(
    getActiveSessions: IGetActiveSessionsByUserIdHandler,
    findWhoIs: Complex.IFindWhoIsHandler,
    context: IHandlerContext,
  ) {
    super(context);
    this.getActiveSessions = getActiveSessions;
    this.findWhoIs = findWhoIs;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const sessionsResult = await this.getActiveSessions.handleAsync({ userId: input.userId });
    if (!sessionsResult.success) return D2Result.bubbleFail(sessionsResult);

    const sessions = sessionsResult.data?.sessions ?? [];
    if (sessions.length === 0) {
      return D2Result.ok({ data: { sessions: [] } });
    }

    const uniqueIps = Array.from(
      new Set(sessions.map((s) => s.ipAddress).filter((ip): ip is string => !!ip)),
    );

    const whoIsByIp = new Map<string, WhoIsDTO>();
    if (uniqueIps.length > 0) {
      // Resolve in parallel — geo-client dedupes/caches at the IP level.
      // Failures are silently dropped: the FE just falls back to the raw IP.
      const results = await Promise.all(
        uniqueIps.map((ip) =>
          this.findWhoIs
            .handleAsync({ ipAddress: ip })
            .then((r) => ({ ip, whoIs: r.success ? r.data?.whoIs : undefined }))
            .catch(() => ({ ip, whoIs: undefined as WhoIsDTO | undefined })),
        ),
      );
      for (const { ip, whoIs } of results) {
        if (whoIs) whoIsByIp.set(ip, whoIs);
      }
    }

    const enriched: EnrichedSession[] = sessions.map((s) => ({
      session: s,
      whoIs: s.ipAddress ? whoIsByIp.get(s.ipAddress) : undefined,
      isCurrent: !!input.currentSessionToken && s.token === input.currentSessionToken,
    }));

    return D2Result.ok({ data: { sessions: enriched } });
  }
}

export type {
  GetMySessionsInput,
  GetMySessionsOutput,
  EnrichedSession,
} from "../../../../interfaces/cqrs/handlers/q/get-my-sessions.js";
