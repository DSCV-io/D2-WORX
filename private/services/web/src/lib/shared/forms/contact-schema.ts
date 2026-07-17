// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { z } from "zod";
import { postcodeValidator } from "postcode-validator";
import { nameField, emailField, phoneField, streetField } from "$lib/shared/forms/schemas.js";
import * as m from "$lib/paraglide/messages.js";

/**
 * Contact form schema factory — matches Geo proto StreetAddressDTO shape.
 *
 * Cross-field rules:
 * - street3 requires street2 (line_3 requires line_2)
 * - street2 requires street1 (line_2 requires line_1)
 * - State required when country has subdivisions
 * - Postal code validated against country format
 *
 * @param countriesWithSubdivisions - Set of ISO alpha-2 codes that have subdivisions.
 */
export function createContactSchema(countriesWithSubdivisions: Set<string>) {
  return z
    .object({
      firstName: nameField(),
      lastName: nameField(),
      email: emailField(),
      phone: phoneField(),
      country: z
        .string()
        .trim()
        .min(1, { error: () => m.webclient_forms_country_required() }),
      state: z.string().trim().optional().default(""),
      street1: streetField(),
      street2: z.string().trim().max(255).optional().default(""),
      street3: z.string().trim().max(255).optional().default(""),
      city: nameField(),
      postalCode: z
        .string()
        .trim()
        .min(1, { error: () => m.webclient_forms_required() })
        .max(16, { error: () => m.webclient_forms_postal_code_too_long() }),
    })
    .refine((d) => !d.street3 || d.street2, {
      error: () => m.webclient_forms_address_line2_required_when_line3(),
      path: ["street2"],
    })
    .refine((d) => !d.street2 || d.street1, {
      error: () => m.webclient_forms_street_required_when_lines(),
      path: ["street1"],
    })
    .superRefine((d, ctx) => {
      // State required when country has subdivisions
      if (d.country && countriesWithSubdivisions.has(d.country) && !d.state?.trim()) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["state"],
          message: m.webclient_forms_state_required_for_country(),
        });
      }

      // Postal code vs country format
      if (d.country && d.postalCode) {
        try {
          if (!postcodeValidator(d.postalCode, d.country)) {
            ctx.addIssue({
              code: z.ZodIssueCode.custom,
              path: ["postalCode"],
              message: m.webclient_forms_postal_code_invalid_for_country({ country: d.country }),
            });
          }
        } catch {
          // postcode-validator doesn't support all countries — skip validation
        }
      }
    });
}

export type ContactFormData = z.infer<ReturnType<typeof createContactSchema>>;
