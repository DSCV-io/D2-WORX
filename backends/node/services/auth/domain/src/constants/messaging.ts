export const AUTH_MESSAGING = {
  // Direct queue: each Auth instance competes to resolve WhoIs for a sign-in
  // event (named queue + routing key = work-queue load balancing).
  WHOIS_RESOLUTION_QUEUE: "auth.whois-resolution",
  WHOIS_RESOLUTION_EXCHANGE: "auth.whois-resolution",
  WHOIS_RESOLUTION_EXCHANGE_TYPE: "direct" as const,

  // Fanout: any service that holds user-scoped data (Geo contacts, Comms
  // threads, Files objects) subscribes independently and anonymizes its own
  // refs when a user is permanently deleted. Fire-and-forget — auth doesn't
  // wait for downstream confirmation.
  USER_ANONYMIZE_EXCHANGE: "auth.user-anonymize",
  USER_ANONYMIZE_EXCHANGE_TYPE: "fanout" as const,
};
