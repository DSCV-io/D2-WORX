import type { IRequestContext, OrgType } from "@d2/handler";

/**
 * Mutable implementation of IRequestContext.
 * Progressively populated by middleware pipeline:
 * 1. Request enrichment → network/fingerprint/WhoIs fields
 * 2. Service key → isTrustedService
 * 3. Auth/scope → identity/org/emulation fields
 *
 * Mirrors D2.Shared.RequestEnrichment.Default.MutableRequestContext in .NET.
 */
export class MutableRequestContext implements IRequestContext {
  // Tracing
  traceId?: string;
  requestId?: string;
  requestPath?: string;

  // User / Identity
  isAuthenticated: boolean | null;
  userId?: string;
  email?: string;
  username?: string;

  // Agent Organization
  agentOrgId?: string;
  agentOrgName?: string;
  agentOrgType?: OrgType;
  agentOrgRole?: string;

  // Target Organization
  targetOrgId?: string;
  targetOrgName?: string;
  targetOrgType?: OrgType;
  targetOrgRole?: string;

  // Org Emulation
  isOrgEmulating: boolean | null;

  // User Impersonation
  impersonatedBy?: string;
  impersonatingEmail?: string;
  impersonatingUsername?: string;
  isUserImpersonating: boolean | null;

  // Network / Enrichment
  readonly clientIp?: string;
  readonly serverFingerprint?: string;
  readonly clientFingerprint?: string;
  readonly deviceFingerprint?: string;
  readonly whoIsHashId?: string;
  readonly city?: string;
  readonly countryCode?: string;
  readonly subdivisionCode?: string;
  readonly isVpn?: boolean;
  readonly isProxy?: boolean;
  readonly isTor?: boolean;
  readonly isHosting?: boolean;

  // Trust
  isTrustedService: boolean | null;

  // Computed helpers — wire format is lowercase; matches DB + .NET OrgTypeValues.
  get isAgentStaff(): boolean {
    const t = this.agentOrgType;
    return t === "admin" || t === "support";
  }

  get isAgentAdmin(): boolean {
    return this.agentOrgType === "admin";
  }

  get isTargetingStaff(): boolean {
    const t = this.targetOrgType;
    return t === "admin" || t === "support";
  }

  get isTargetingAdmin(): boolean {
    return this.targetOrgType === "admin";
  }

  constructor(params: {
    clientIp: string;
    serverFingerprint: string;
    clientFingerprint?: string;
    deviceFingerprint: string;
    whoIsHashId?: string;
    city?: string;
    countryCode?: string;
    subdivisionCode?: string;
    isVpn?: boolean;
    isProxy?: boolean;
    isTor?: boolean;
    isHosting?: boolean;
  }) {
    this.clientIp = params.clientIp;
    this.serverFingerprint = params.serverFingerprint;
    this.clientFingerprint = params.clientFingerprint;
    this.deviceFingerprint = params.deviceFingerprint;
    this.whoIsHashId = params.whoIsHashId;
    this.city = params.city;
    this.countryCode = params.countryCode;
    this.subdivisionCode = params.subdivisionCode;
    this.isVpn = params.isVpn;
    this.isProxy = params.isProxy;
    this.isTor = params.isTor;
    this.isHosting = params.isHosting;
    this.isAuthenticated = null;
    this.isTrustedService = null;
    this.isOrgEmulating = null;
    this.isUserImpersonating = null;
    this.traceId = crypto.randomUUID();
  }
}
