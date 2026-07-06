// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// GrpcWorkloadCertificateIssuer — the adapter binding the issuer port to the
// EMITTED KeyCustodian TS gRPC client. Driven against a capturing fake of the
// generated client interface: DTO mapping (bytes pass-through, ISO date →
// Temporal.Instant), failure bubbling (typed failures survive verbatim), signal
// forwarding, and the structural pin that the wire request carries ONLY { csrDer }.

import { describe, expect, it } from "vitest";
import { Temporal } from "temporal-polyfill";
import { type D2Result, ok, forbidden, serviceUnavailable } from "@d2/result";
import { GrpcWorkloadCertificateIssuer } from "../src/grpc-issuer-adapter.js";
import type { KeyCustodianGrpcClient } from "../src/generated/key-custodian-grpc-client.g.js";
import type {
  IssueLeafInput,
  IssueLeafOutput,
} from "../src/generated/issue-leaf-dto.g.js";
import type {
  GetCaCertificateInput,
  GetCaCertificateOutput,
} from "../src/generated/get-ca-certificate-dto.g.js";

interface CapturedCall {
  readonly input: unknown;
  readonly opts: unknown;
}

function fakeClient(script: {
  issueLeaf?: (input: IssueLeafInput) => D2Result<IssueLeafOutput>;
  getCaCertificate?: (
    input: GetCaCertificateInput,
  ) => D2Result<GetCaCertificateOutput>;
}): { client: KeyCustodianGrpcClient; calls: CapturedCall[] } {
  const calls: CapturedCall[] = [];
  const client: KeyCustodianGrpcClient = {
    async sign() {
      throw new Error("not under test");
    },
    async getKeyring() {
      throw new Error("not under test");
    },
    async getOrLazyProvisionSealPublicKey() {
      throw new Error("not under test");
    },
    async getOrLazyProvisionOwnSealPrivateKey() {
      throw new Error("not under test");
    },
    async issueLeaf(input, opts) {
      calls.push({ input, opts });
      return script.issueLeaf?.(input) ?? serviceUnavailable<IssueLeafOutput>();
    },
    async getCaCertificate(input, opts) {
      calls.push({ input, opts });
      return (
        script.getCaCertificate?.(input) ??
        serviceUnavailable<GetCaCertificateOutput>()
      );
    },
  };
  return { client, calls };
}

describe("GrpcWorkloadCertificateIssuer.issueLeaf", () => {
  const leafOut: IssueLeafOutput = {
    certificateDer: new Uint8Array([1, 2, 3]),
    issuerCertificateDer: new Uint8Array([4, 5]),
    notBefore: "2026-07-03T10:00:00.000Z",
    notAfter: "2026-07-03T22:00:00.000Z",
  };

  it("maps the wire output to WorkloadLeafMaterial (bytes verbatim, ISO notAfter → Temporal.Instant)", async () => {
    const { client } = fakeClient({ issueLeaf: () => ok(leafOut) });
    const issuer = new GrpcWorkloadCertificateIssuer(client);

    const result = await issuer.issueLeaf(new Uint8Array([9, 9]));

    expect(result.success).toBe(true);
    expect(result.data!.certificateDer).toEqual(new Uint8Array([1, 2, 3]));
    expect(result.data!.issuerCertificateDer).toEqual(new Uint8Array([4, 5]));
    // The wire ISO-8601 string materializes to the equivalent Temporal.Instant.
    expect(
      Temporal.Instant.compare(
        result.data!.notAfter,
        Temporal.Instant.from("2026-07-03T22:00:00.000Z"),
      ),
    ).toBe(0);
  });

  it("sends a request carrying ONLY { csrDer } — the structural no-key-on-the-wire pin", async () => {
    const { client, calls } = fakeClient({ issueLeaf: () => ok(leafOut) });
    const issuer = new GrpcWorkloadCertificateIssuer(client);
    const csr = new Uint8Array([7, 7, 7]);

    await issuer.issueLeaf(csr);

    expect(calls).toHaveLength(1);
    const request = calls[0]!.input as Record<string, unknown>;
    expect(Object.keys(request)).toEqual(["csrDer"]);
    expect(request["csrDer"]).toBe(csr);
  });

  it("forwards the abort signal into the call options", async () => {
    const { client, calls } = fakeClient({ issueLeaf: () => ok(leafOut) });
    const issuer = new GrpcWorkloadCertificateIssuer(client);
    const controller = new AbortController();

    await issuer.issueLeaf(new Uint8Array([1]), controller.signal);

    expect((calls[0]!.opts as { signal?: AbortSignal }).signal).toBe(
      controller.signal,
    );
  });

  it("bubbles a typed wire failure verbatim (status + error code preserved)", async () => {
    const { client } = fakeClient({
      issueLeaf: () =>
        forbidden<IssueLeafOutput>({ errorCode: "KEYCUSTODIAN_INVALID_CSR" }),
    });
    const issuer = new GrpcWorkloadCertificateIssuer(client);

    const result = await issuer.issueLeaf(new Uint8Array([1]));

    expect(result.failed).toBe(true);
    expect(result.statusCode).toBe(403);
    expect(result.errorCode).toBe("KEYCUSTODIAN_INVALID_CSR");
  });

  it("maps a success-without-data envelope to a failure (never fabricates material)", async () => {
    const { client } = fakeClient({
      issueLeaf: () => ok<IssueLeafOutput>(undefined),
    });
    const issuer = new GrpcWorkloadCertificateIssuer(client);

    const result = await issuer.issueLeaf(new Uint8Array([1]));

    expect(result.failed).toBe(true);
  });
});

describe("GrpcWorkloadCertificateIssuer.getCaCertificate", () => {
  const chainOut: GetCaCertificateOutput = {
    rootCertificateDer: new Uint8Array([10, 11]),
    intermediateCertificateDer: new Uint8Array([12]),
  };

  it("maps the wire output to CaChainMaterial", async () => {
    const { client, calls } = fakeClient({
      getCaCertificate: () => ok(chainOut),
    });
    const issuer = new GrpcWorkloadCertificateIssuer(client);

    const result = await issuer.getCaCertificate();

    expect(result.success).toBe(true);
    expect(result.data!.rootCertificateDer).toEqual(new Uint8Array([10, 11]));
    expect(result.data!.intermediateCertificateDer).toEqual(
      new Uint8Array([12]),
    );
    // The op takes no parameters — the wire request is the empty input model.
    expect(Object.keys(calls[0]!.input as object)).toEqual([]);
  });

  it("bubbles a typed failure from the chain fetch", async () => {
    const { client } = fakeClient({});
    const issuer = new GrpcWorkloadCertificateIssuer(client);

    const result = await issuer.getCaCertificate();

    expect(result.failed).toBe(true);
    expect(result.statusCode).toBe(503);
  });

  it("maps a success-without-data chain envelope to a typed failure", async () => {
    const { client } = fakeClient({
      getCaCertificate: () => ok<GetCaCertificateOutput>(undefined),
    });
    const issuer = new GrpcWorkloadCertificateIssuer(client);

    const result = await issuer.getCaCertificate();

    expect(result.failed).toBe(true);
    expect(result.statusCode).toBe(503);
  });
});
