// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { ChannelCredentials } from "@grpc/grpc-js";

/**
 * Inputs for {@link buildMutualTlsCredentials} — the assembled trust bundle
 * (SERVER-side validation) plus the LOCAL leaf chain + private key the client
 * presents (CLIENT-side authentication).
 */
export interface MutualTlsCredentialsInput {
  /** Root + intermediate PEM the client pins to validate the server. Public. */
  readonly caBundlePem: string;
  /** Leaf → intermediate PEM the client presents on the handshake. Public. */
  readonly certChainPem: string;
  /** PKCS#8 PEM private key certifying the presented leaf. SECRET — never logged. */
  readonly privateKeyPem: string;
}

/**
 * Build mutual-TLS `ChannelCredentials` presenting the workload's leaf chain +
 * private key and pinning the fetched CA bundle. This is NET-NEW TS-side: the
 * shared `@d2/grpc-client` channel is server-TLS only
 * (`ChannelCredentials.createSsl()` with no client material) — the workload-leaf
 * client presents a CLIENT certificate, which requires the private-key +
 * cert-chain overload here.
 *
 * The private key stays in-process — it is passed as a Buffer straight into
 * grpc-js and is never logged or serialized elsewhere.
 *
 * @param input - The trust bundle + client leaf chain + private key (PEM).
 * @returns Mutual-TLS channel credentials.
 */
export function buildMutualTlsCredentials(
  input: MutualTlsCredentialsInput,
): ChannelCredentials {
  return ChannelCredentials.createSsl(
    Buffer.from(input.caBundlePem, "utf8"),
    Buffer.from(input.privateKeyPem, "utf8"),
    Buffer.from(input.certChainPem, "utf8"),
  );
}
