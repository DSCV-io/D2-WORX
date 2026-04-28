import { z } from "zod";

/**
 * Zod schemas for contact input validation.
 *
 * These match the constraints in Geo's EF Core entity configurations
 * (ContactConfig.cs, LocationConfig.cs) and FluentValidation rules
 * (ContactToCreateValidator.cs). Any service creating or updating
 * contacts via Geo should use these schemas as the single source of truth.
 */

export const contactMethodsSchema = z.object({
  emails: z
    .array(
      z.object({
        value: z.string().email().max(254),
        labels: z.array(z.string().max(50)).max(10).optional(),
      }),
    )
    .max(20)
    .optional(),
  phoneNumbers: z
    .array(
      z.object({
        value: z.string().min(1).max(20),
        labels: z.array(z.string().max(50)).max(10).optional(),
      }),
    )
    .max(20)
    .optional(),
});

export const personalDetailsSchema = z.object({
  title: z.string().max(20).optional(),
  firstName: z.string().max(255).optional(),
  preferredName: z.string().max(255).optional(),
  middleName: z.string().max(255).optional(),
  lastName: z.string().max(255).optional(),
  generationalSuffix: z.string().max(10).optional(),
  professionalCredentials: z.array(z.string().max(50)).max(20).optional(),
  dateOfBirth: z.string().max(10).optional(),
  biologicalSex: z.string().max(20).optional(),
});

export const professionalDetailsSchema = z.object({
  companyName: z.string().max(255).optional(),
  jobTitle: z.string().max(255).optional(),
  department: z.string().max(255).optional(),
  companyWebsite: z.string().max(2048).optional(),
});

export const locationInputSchema = z.object({
  coordinates: z.object({ latitude: z.number(), longitude: z.number() }).optional(),
  address: z
    .object({
      line1: z.string().max(255).optional(),
      line2: z.string().max(255).optional(),
      line3: z.string().max(255).optional(),
    })
    .optional(),
  city: z.string().max(255).optional(),
  postalCode: z.string().max(16).optional(),
  subdivisionIso31662Code: z.string().max(6).optional(),
  countryIso31661Alpha2Code: z.string().max(2).optional(),
});

/**
 * Composite schema for contact input — all sub-objects optional.
 * Use this when accepting user-provided contact details for Geo creation/update.
 */
export const contactInputSchema = z.object({
  contactMethods: contactMethodsSchema.optional(),
  personalDetails: personalDetailsSchema.optional(),
  professionalDetails: professionalDetailsSchema.optional(),
  location: locationInputSchema.optional(),
  ietfBcp47Tag: z.string().min(1).max(35).optional(),
  ianaIdentifier: z.string().min(1).max(64).optional(),
});
