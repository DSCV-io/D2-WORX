import { readFileSync, writeFileSync } from "node:fs";

function replaceUsingBlock(text, newUsings) {
  const lines = text.split(/\r?\n/);
  let start = -1;
  let end = -1;

  for (let i = 0; i < lines.length; i++) {
    const t = lines[i].trim();

    if (t.startsWith("using ") && t.endsWith(";")) {
      if (start < 0) start = i;
      end = i;
    } else if (start >= 0) {
      break;
    }
  }

  if (start < 0) throw new Error("no usings");

  return [...lines.slice(0, start), ...newUsings, ...lines.slice(end + 1)].join(
    "\n",
  );
}

function sortUsings(usings) {
  return [...usings].sort((a, b) => {
    const na = a
      .trim()
      .replace(/^using\s+/, "")
      .replace(/;$/, "");
    const nb = b
      .trim()
      .replace(/^using\s+/, "")
      .replace(/;$/, "");
    const ga = na.includes(" = ")
      ? 3
      : na.startsWith("static ")
        ? 2
        : na.startsWith("System")
          ? 0
          : 1;
    const gb = nb.includes(" = ")
      ? 3
      : nb.startsWith("static ")
        ? 2
        : nb.startsWith("System")
          ? 0
          : 1;

    return ga - gb || na.localeCompare(nb);
  });
}

const fixed = {
  "private/services/edge/key-custodian/client/Keyring/KeyringServiceCollectionExtensions.cs":
    [
      "using System.Linq;",
      "using DcsvIo.D2.Auth.Events;",
      "using DcsvIo.D2.Encryption;",
      "using DcsvIo.D2.Messaging;",
      "using Microsoft.Extensions.DependencyInjection;",
      "using Microsoft.Extensions.DependencyInjection.Extensions;",
      "using Microsoft.Extensions.Logging;",
      "using KeyringClientStub = global::D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianKeyring.KeyCustodianKeyringClient;",
    ],
  "private/services/edge/key-custodian/client/Sealing/KeyringBackedPayloadOpener.cs":
    [
      "using System.Collections.Concurrent;",
      "using System.Linq;",
      "using DcsvIo.D2.Encryption;",
      "using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;",
      "using DcsvIo.D2.Resilience.Retry;",
      "using Microsoft.Extensions.Logging;",
    ],
  "private/packages/dotnet/tests/Unit/Encryption/ProductEncryptionDomainBootstrapTests.cs":
    [
      "using System;",
      "using System.Collections.Concurrent;",
      "using System.Threading.Tasks;",
      "using AwesomeAssertions;",
      "using DcsvIo.D2.Encryption;",
      "using DcsvIo.D2.Private.Encryption;",
      "using Xunit;",
    ],
  "private/services/audit/api/Composition/AuditEndpointRouteBuilderExtensions.cs":
    [
      "using DcsvIo.D2.Auth.Grpc.Endpoints;",
      "using DcsvIo.D2.Private.Audit.Api.Grpc;",
      "using DcsvIo.D2.Private.Audit.Api.Kestrel;",
      "using DcsvIo.D2.Private.Auth;",
      "using DcsvIo.D2.ServiceDefaults;",
      "using Microsoft.AspNetCore.Builder;",
      "using Microsoft.AspNetCore.Routing;",
    ],
};

for (const [file, usings] of Object.entries(fixed)) {
  const o = readFileSync(file, "utf8");
  writeFileSync(file, replaceUsingBlock(o, usings));
  console.log("patched", file);
}

for (const file of [
  "private/services/edge/key-custodian/client/Sealing/KeyringBackedPayloadSealer.cs",
  "private/services/audit/api/Composition/AuditHostServiceCollectionExtensions.cs",
]) {
  const o = readFileSync(file, "utf8");
  const usings = o
    .split(/\r?\n/)
    .filter((l) => l.trim().startsWith("using ") && l.trim().endsWith(";"));
  writeFileSync(file, replaceUsingBlock(o, sortUsings(usings)));
  console.log("sorted", file, sortUsings(usings));
}

// Restore domain WorkloadIdentity type declaration
const wi =
  "private/services/edge/key-custodian/domain/ValueObjects/WorkloadIdentity.cs";
let w = readFileSync(wi, "utf8");
w = w.replaceAll(
  "public sealed record global::DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects.WorkloadIdentity",
  "public sealed record WorkloadIdentity",
);
// Inside the defining type, prefer simple name for self-refs
w = w.replaceAll(
  "global::DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects.WorkloadIdentity",
  "WorkloadIdentity",
);
writeFileSync(wi, w);
console.log("restored WorkloadIdentity.cs");
