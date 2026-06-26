// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import { getCatalogIdentity } from "../src/catalog-identity.js";
import type {
  MultiCatalogIdentity,
  NestedCatalogIdentity,
} from "../src/catalog-identity.js";

// ---------------------------------------------------------------------------
// Registered flat catalogs — correct idField + arrayProp
// ---------------------------------------------------------------------------

describe("getCatalogIdentity — registered catalogs resolve correctly", () => {
  it("keycustodian-error-codes.spec.json resolves to errorCodes/code", () => {
    const id = getCatalogIdentity(
      "contracts/keycustodian-error-codes/keycustodian-error-codes.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("errorCodes");
      expect(id.idField).toBe("code");
    }
  });

  it("auth-error-codes.spec.json resolves to errorCodes/code", () => {
    const id = getCatalogIdentity(
      "contracts/auth-error-codes/auth-error-codes.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("errorCodes");
      expect(id.idField).toBe("code");
    }
  });

  it("error-codes.spec.json resolves to errorCodes/code", () => {
    const id = getCatalogIdentity(
      "contracts/error-codes/error-codes.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("errorCodes");
      expect(id.idField).toBe("code");
    }
  });

  it("scopes.spec.json resolves to scopes/name", () => {
    const id = getCatalogIdentity("contracts/auth-scopes/scopes.spec.json");
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("scopes");
      expect(id.idField).toBe("name");
    }
  });

  it("headers.spec.json resolves to headers/name", () => {
    const id = getCatalogIdentity("contracts/headers/headers.spec.json");
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("headers");
      expect(id.idField).toBe("name");
    }
  });

  it("audiences.spec.json resolves to audiences/name", () => {
    const id = getCatalogIdentity(
      "contracts/auth-audiences/audiences.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("audiences");
      expect(id.idField).toBe("name");
    }
  });

  it("advisory-locks.spec.json resolves to locks/constName (NOT name)", () => {
    // Regression: the identity field is `constName` (e.g. "MIGRATOR"), not `name`.
    // The advisory-locks catalog has no `name` field — entries are identified by
    // constName (the generated constant identifier) + a numeric PG advisory-lock key.
    const id = getCatalogIdentity(
      "contracts/advisory-locks/advisory-locks.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("locks");
      expect(id.idField).toBe("constName");
    }
  });

  // ---------------------------------------------------------------------------
  // constName-identity catalogs — regression coverage
  // ---------------------------------------------------------------------------

  it("jwt-claims.spec.json resolves to claims/constName (NOT name)", () => {
    const id = getCatalogIdentity("contracts/jwt-claims/jwt-claims.spec.json");
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("claims");
      expect(id.idField).toBe("constName");
    }
  });

  it("grpc-trailers.spec.json resolves to trailers/constName (NOT name)", () => {
    const id = getCatalogIdentity(
      "contracts/grpc-trailers/grpc-trailers.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("trailers");
      expect(id.idField).toBe("constName");
    }
  });

  it("otel-messaging-tags.spec.json resolves to tags/constName (NOT name)", () => {
    const id = getCatalogIdentity(
      "contracts/otel-messaging-tags/otel-messaging-tags.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("tags");
      expect(id.idField).toBe("constName");
    }
  });

  it("encryption-domains.spec.json resolves to domains/constName (NOT name)", () => {
    const id = getCatalogIdentity(
      "contracts/encryption-domains/encryption-domains.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("domains");
      expect(id.idField).toBe("constName");
    }
  });

  it("keys.spec.json (in-process-keys) resolves to keys/constName (NOT name)", () => {
    const id = getCatalogIdentity("contracts/in-process-keys/keys.spec.json");
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("keys");
      expect(id.idField).toBe("constName");
    }
  });

  it("dlq-failure-metadata.spec.json resolves to multi-catalog (fields + causes, NOT flat fields alone)", () => {
    // dlq-failure-metadata carries two independently-gated wire surfaces: fields[] and causes[].
    // The registration is now a multi-catalog identity — verify the correct kind is returned.
    const id = getCatalogIdentity(
      "contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json",
    );
    expect(id.kind).toBe("multi");
  });

  it("encryption-frame.spec.json resolves to fields/constName (NOT name)", () => {
    const id = getCatalogIdentity(
      "contracts/encryption-frame/encryption-frame.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("fields");
      expect(id.idField).toBe("constName");
    }
  });

  it("d2result-envelope.spec.json resolves to fields/constName (NOT name)", () => {
    const id = getCatalogIdentity(
      "contracts/d2result-envelope/d2result-envelope.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("fields");
      expect(id.idField).toBe("constName");
    }
  });

  it("input-error.spec.json resolves to properties/constName (NOT fields/name)", () => {
    const id = getCatalogIdentity(
      "contracts/input-error/input-error.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("properties");
      expect(id.idField).toBe("constName");
    }
  });

  it("tk-message.spec.json resolves to properties/constName (NOT fields/name)", () => {
    const id = getCatalogIdentity("contracts/tk-message/tk-message.spec.json");
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("properties");
      expect(id.idField).toBe("constName");
    }
  });

  it("problem-details.spec.json resolves to multi-catalog (extensionKeys + titles, NOT flat extensionKeys alone)", () => {
    // problem-details carries two independently-gated wire surfaces: extensionKeys[] and titles[].
    // The registration is now a multi-catalog identity — this verifies the correct kind is returned.
    const id = getCatalogIdentity(
      "contracts/problem-details/problem-details.spec.json",
    );
    expect(id.kind).toBe("multi");
  });

  it("error-category.spec.json resolves to categories/wire (NOT categories/name)", () => {
    const id = getCatalogIdentity(
      "contracts/error-category/error-category.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("categories");
      expect(id.idField).toBe("wire");
    }
  });

  it("mq-messages.spec.json resolves to messages/constant (NOT messages/name)", () => {
    const id = getCatalogIdentity(
      "contracts/mq-messages/mq-messages.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("messages");
      expect(id.idField).toBe("constant");
    }
  });

  it("mq-subscriptions.spec.json resolves to subscriptions/constant (NOT subscriptions/name)", () => {
    const id = getCatalogIdentity(
      "contracts/mq-subscriptions/mq-subscriptions.spec.json",
    );
    expect(id.kind).toBe("flat");
    if (id.kind === "flat") {
      expect(id.arrayProp).toBe("subscriptions");
      expect(id.idField).toBe("constant");
    }
  });
});

// ---------------------------------------------------------------------------
// Multi-catalog identities (field-constraints, problem-details)
// ---------------------------------------------------------------------------

describe("getCatalogIdentity — field-constraints multi-catalog (constraints + enums)", () => {
  it("field-constraints.spec.json resolves to multi with constraints/name and enums nested members/name", () => {
    const id = getCatalogIdentity(
      "contracts/validation/field-constraints.spec.json",
    );
    expect(id.kind).toBe("multi");

    if (id.kind === "multi") {
      const multi = id as MultiCatalogIdentity;
      expect(multi.parts).toHaveLength(2);

      const constraintsPart = multi.parts[0];
      expect(constraintsPart?.kind).toBe("flat");

      if (constraintsPart?.kind === "flat") {
        expect(constraintsPart.arrayProp).toBe("constraints");
        expect(constraintsPart.idField).toBe("name");
      }

      const enumsPart = multi.parts[1];
      expect(enumsPart?.kind).toBe("nested");

      if (enumsPart?.kind === "nested") {
        const nestedEnums = enumsPart as NestedCatalogIdentity;
        expect(nestedEnums.arrayProp).toBe("enums");
        expect(nestedEnums.idField).toBe("name");
        expect(nestedEnums.nested.arrayProp).toBe("members");
        expect(nestedEnums.nested.idField).toBe("name");
      }
    }
  });
});

describe("getCatalogIdentity — dlq-failure-metadata multi-catalog (fields + causes)", () => {
  it("dlq-failure-metadata.spec.json resolves to multi with fields/constName and causes/constName", () => {
    const id = getCatalogIdentity(
      "contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json",
    );
    expect(id.kind).toBe("multi");

    if (id.kind === "multi") {
      const multi = id as MultiCatalogIdentity;
      expect(multi.parts).toHaveLength(2);

      const fieldsPart = multi.parts[0];
      expect(fieldsPart?.kind).toBe("flat");

      if (fieldsPart?.kind === "flat") {
        expect(fieldsPart.arrayProp).toBe("fields");
        expect(fieldsPart.idField).toBe("constName");
      }

      const causesPart = multi.parts[1];
      expect(causesPart?.kind).toBe("flat");

      if (causesPart?.kind === "flat") {
        expect(causesPart.arrayProp).toBe("causes");
        expect(causesPart.idField).toBe("constName");
      }
    }
  });
});

describe("getCatalogIdentity — problem-details multi-catalog (extensionKeys + titles)", () => {
  it("problem-details.spec.json resolves to multi with extensionKeys/constName and titles/constName", () => {
    const id = getCatalogIdentity(
      "contracts/problem-details/problem-details.spec.json",
    );
    expect(id.kind).toBe("multi");

    if (id.kind === "multi") {
      const multi = id as MultiCatalogIdentity;
      expect(multi.parts).toHaveLength(2);

      const extKeysPart = multi.parts[0];
      expect(extKeysPart?.kind).toBe("flat");

      if (extKeysPart?.kind === "flat") {
        expect(extKeysPart.arrayProp).toBe("extensionKeys");
        expect(extKeysPart.idField).toBe("constName");
      }

      const titlesPart = multi.parts[1];
      expect(titlesPart?.kind).toBe("flat");

      if (titlesPart?.kind === "flat") {
        expect(titlesPart.arrayProp).toBe("titles");
        expect(titlesPart.idField).toBe("constName");
      }
    }
  });
});

// ---------------------------------------------------------------------------
// Auth/request context interface specs — nested catalog (sections → properties)
// ---------------------------------------------------------------------------

describe("getCatalogIdentity — IAuthContext + IRequestContext nested catalog", () => {
  const contextSpecs = [
    "contracts/auth-context/IAuthContext.spec.json",
    "contracts/request-context/IRequestContext.spec.json",
  ];

  for (const specPath of contextSpecs) {
    it(`${specPath} resolves to nested sections/name → properties/name`, () => {
      const id = getCatalogIdentity(specPath);
      expect(id.kind).toBe("nested");
      if (id.kind === "nested") {
        expect(id.arrayProp).toBe("sections");
        expect(id.idField).toBe("name");
        expect(id.nested.arrayProp).toBe("properties");
        expect(id.nested.idField).toBe("name");
      }
    });
  }
});

// ---------------------------------------------------------------------------
// Telemetry nested catalog
// ---------------------------------------------------------------------------

describe("getCatalogIdentity — telemetry nested catalog", () => {
  it("telemetry.spec.json resolves to nested identity with meters/meter", () => {
    const id = getCatalogIdentity("contracts/telemetry/telemetry.spec.json");
    expect(id.kind).toBe("nested");
    if (id.kind === "nested") {
      expect(id.arrayProp).toBe("meters");
      expect(id.idField).toBe("meter");
      expect(id.nested.arrayProp).toBe("instruments");
      expect(id.nested.idField).toBe("name");
      expect(id.nested.nested).toBeDefined();
      if (id.nested.nested !== undefined) {
        expect(id.nested.nested.arrayProp).toBe("tags");
        expect(id.nested.nested.idField).toBe("name");
        expect(id.nested.nested.valuesArrayProp).toBe("values");
      }
    }
  });
});

// ---------------------------------------------------------------------------
// Geo $generated specs → EXEMPT
// ---------------------------------------------------------------------------

describe("getCatalogIdentity — geo $generated specs are EXEMPT", () => {
  const geoSpecs = [
    "contracts/geo/languages.spec.json",
    "contracts/geo/countries.spec.json",
    "contracts/geo/currencies.spec.json",
    "contracts/geo/timezones.spec.json",
    "contracts/geo/locales.spec.json",
    "contracts/geo/subdivisions.spec.json",
    "contracts/geo/geopolitical-entities.spec.json",
  ];

  for (const specPath of geoSpecs) {
    it(`${specPath} resolves to exempt`, () => {
      const id = getCatalogIdentity(specPath);
      expect(id.kind).toBe("exempt");
    });
  }

  it("geo src-data specs are also exempt", () => {
    const id = getCatalogIdentity("contracts/geo/src-data/languages.spec.json");
    expect(id.kind).toBe("exempt");
  });

  it("geo overlay specs are also exempt", () => {
    const id = getCatalogIdentity(
      "contracts/geo/overlays/countries.overlays.spec.json",
    );
    expect(id.kind).toBe("exempt");
  });
});

// ---------------------------------------------------------------------------
// Unregistered catalog → fail-loud
// ---------------------------------------------------------------------------

describe("getCatalogIdentity — unregistered catalog fails loud", () => {
  it("throws for an unknown spec file path", () => {
    expect(() =>
      getCatalogIdentity("contracts/unknown-domain/some-new.spec.json"),
    ).toThrow(/unregistered spec catalog/);
  });

  it("error message instructs to add an entry to REGISTRY", () => {
    let errorMessage = "";

    try {
      getCatalogIdentity("contracts/new-thing/thing.spec.json");
    } catch (err) {
      errorMessage = String(err);
    }

    expect(errorMessage).toContain("catalog-identity.ts");
    expect(errorMessage).toContain("REGISTRY");
  });

  it("backslash paths are normalized to forward slashes before matching", () => {
    // Windows paths use backslashes — the registry should normalize them.
    const id = getCatalogIdentity("contracts\\headers\\headers.spec.json");
    expect(id.kind).toBe("flat");
  });
});
