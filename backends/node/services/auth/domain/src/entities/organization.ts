import { cleanStr, cleanDisplayStr, generateUuidV7 } from "@d2/utilities";
import type { OrgType } from "../enums/org-type.js";
import { isValidOrgType } from "../enums/org-type.js";
import { AuthValidationError } from "../exceptions/auth-validation-error.js";

export interface Organization {
  readonly id: string;
  readonly name: string;
  readonly slug: string;
  readonly orgType: OrgType;
  readonly logo?: string;
  readonly metadata?: string;
  readonly createdAt: Date;
  readonly updatedAt: Date;
}

export interface CreateOrganizationInput {
  readonly name: string;
  readonly slug: string;
  readonly orgType: OrgType;
  readonly id?: string;
  readonly logo?: string;
  readonly metadata?: string;
}

export interface UpdateOrganizationInput {
  readonly name?: string;
  readonly logo?: string;
  readonly metadata?: string;
}

export function createOrganization(input: CreateOrganizationInput): Organization {
  const name = cleanDisplayStr(input.name);
  if (!name) {
    throw new AuthValidationError("Organization", "name", input.name, "is required.");
  }

  const slug = cleanStr(input.slug)?.toLowerCase();
  if (!slug) {
    throw new AuthValidationError("Organization", "slug", input.slug, "is required.");
  }

  if (!isValidOrgType(input.orgType)) {
    throw new AuthValidationError(
      "Organization",
      "orgType",
      input.orgType,
      "is not a valid organization type.",
    );
  }

  return {
    id: input.id ?? generateUuidV7(),
    name,
    slug,
    orgType: input.orgType,
    logo: input.logo,
    metadata: input.metadata,
    createdAt: new Date(),
    updatedAt: new Date(),
  };
}

/**
 * Updates mutable organization fields. Slug and orgType are immutable after creation.
 */
export function updateOrganization(
  org: Organization,
  updates: UpdateOrganizationInput,
): Organization {
  let name = org.name;
  if (updates.name !== undefined) {
    const cleaned = cleanDisplayStr(updates.name);
    if (!cleaned) {
      throw new AuthValidationError("Organization", "name", updates.name, "is required.");
    }
    name = cleaned;
  }

  return {
    ...org,
    name,
    logo: updates.logo !== undefined ? updates.logo : org.logo,
    metadata: updates.metadata !== undefined ? updates.metadata : org.metadata,
    updatedAt: new Date(),
  };
}
