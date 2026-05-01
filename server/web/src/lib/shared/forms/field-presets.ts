/**
 * Field presets — reusable UI metadata (labels, placeholders, descriptions)
 * for common form fields.
 *
 * Presets use getters so Paraglide resolves messages at render time
 * (not import time), ensuring the active locale is respected.
 *
 * Usage:
 *   <FormInput {form} field="firstName" {...FIRST_NAME} />
 *   <FormCombobox {form} field="country" {...COUNTRY} options={countries} />
 */

import * as m from "$lib/paraglide/messages.js";

export interface FieldPreset {
  label: string;
  placeholder: string;
  type?: string;
  description?: string;
}

export interface ComboboxPreset extends FieldPreset {
  searchPlaceholder?: string;
  emptyMessage?: string;
}

// --- Input presets ---

export const FIRST_NAME: FieldPreset = {
  get label() {
    return m.webclient_forms_first_name_label();
  },
  get placeholder() {
    return m.webclient_forms_first_name_placeholder();
  },
};

export const LAST_NAME: FieldPreset = {
  get label() {
    return m.webclient_forms_last_name_label();
  },
  get placeholder() {
    return m.webclient_forms_last_name_placeholder();
  },
};

export const EMAIL: FieldPreset = {
  get label() {
    return m.webclient_forms_email_label();
  },
  get placeholder() {
    return m.webclient_forms_email_placeholder();
  },
  type: "email",
};

export const PHONE: FieldPreset = {
  get label() {
    return m.webclient_forms_phone_label();
  },
  get placeholder() {
    return m.webclient_forms_phone_placeholder();
  },
  get description() {
    return m.webclient_forms_phone_description();
  },
};

export const STREET1: FieldPreset = {
  get label() {
    return m.webclient_forms_street1_label();
  },
  get placeholder() {
    return m.webclient_forms_street1_placeholder();
  },
};

export const STREET2: FieldPreset = {
  get label() {
    return m.webclient_forms_street2_label();
  },
  get placeholder() {
    return m.webclient_forms_street2_placeholder();
  },
};

export const STREET3: FieldPreset = {
  get label() {
    return m.webclient_forms_street3_label();
  },
  get placeholder() {
    return m.webclient_forms_street3_placeholder();
  },
};

export const CITY: FieldPreset = {
  get label() {
    return m.webclient_forms_city_label();
  },
  get placeholder() {
    return m.webclient_forms_city_placeholder();
  },
};

export const POSTAL_CODE: FieldPreset = {
  get label() {
    return m.webclient_forms_postal_code_label();
  },
  get placeholder() {
    return m.webclient_forms_postal_code_placeholder();
  },
  get description() {
    return m.webclient_forms_postal_code_description();
  },
};

export const PASSWORD: FieldPreset = {
  get label() {
    return m.webclient_forms_password_label();
  },
  get placeholder() {
    return m.webclient_forms_password_placeholder();
  },
  type: "password",
};

export const CONFIRM_EMAIL: FieldPreset = {
  get label() {
    return m.webclient_forms_confirm_email_label();
  },
  get placeholder() {
    return m.webclient_forms_confirm_email_placeholder();
  },
  type: "email",
};

export const CONFIRM_PASSWORD: FieldPreset = {
  get label() {
    return m.webclient_forms_confirm_password_label();
  },
  get placeholder() {
    return m.webclient_forms_confirm_password_placeholder();
  },
  type: "password",
};

export const NEW_PASSWORD: FieldPreset = {
  get label() {
    return m.webclient_forms_new_password_label();
  },
  get placeholder() {
    return m.webclient_forms_new_password_placeholder();
  },
  type: "password",
};

export const CONFIRM_NEW_PASSWORD: FieldPreset = {
  get label() {
    return m.webclient_forms_confirm_new_password_label();
  },
  get placeholder() {
    return m.webclient_forms_confirm_new_password_placeholder();
  },
  type: "password",
};

// --- Combobox presets ---

export const COUNTRY: ComboboxPreset = {
  get label() {
    return m.webclient_forms_country_label();
  },
  get placeholder() {
    return m.webclient_forms_country_placeholder();
  },
  get searchPlaceholder() {
    return m.webclient_forms_country_search();
  },
  get emptyMessage() {
    return m.webclient_forms_country_empty();
  },
};

export const STATE: ComboboxPreset = {
  get label() {
    return m.webclient_forms_state_label();
  },
  get placeholder() {
    return m.webclient_forms_state_placeholder();
  },
  get searchPlaceholder() {
    return m.webclient_forms_state_search();
  },
  get emptyMessage() {
    return m.webclient_forms_state_empty();
  },
};
