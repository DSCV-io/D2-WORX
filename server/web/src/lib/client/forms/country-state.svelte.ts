/**
 * Composable for country→state/province cascading logic.
 *
 * Manages state options, visibility, and clearing when country changes.
 * Revalidates postal code on country change (format may differ).
 */
import { fromStore } from "svelte/store";
import type { Readable } from "svelte/store";
import type { SubdivisionOption } from "$lib/shared/forms/geo-ref-data.js";

/**
 * Minimal form shape — uses `any` for store properties to avoid
 * SuperForm generic invariance issues. Internally cast via fromStore().
 */
interface FormLike {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  form: any;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  validate: (...args: any[]) => any;
}

export interface UseCountryStateOptions {
  form: FormLike;
  subdivisionsByCountry: Record<string, SubdivisionOption[]>;
  /** Form field name for country. @default "country" */
  countryField?: string;
  /** Form field name for state. @default "state" */
  stateField?: string;
  /** Form field name for postal code. @default "postalCode" */
  postalCodeField?: string;
}

export interface UseCountryStateReturn {
  /** Current subdivision options for the selected country. */
  readonly stateOptions: SubdivisionOption[];
  /** Whether the state field should be visible. */
  readonly showState: boolean;
  /** Call when country selection changes. */
  handleCountryChange: (countryCode: string) => void;
}

export function useCountryState(options: UseCountryStateOptions): UseCountryStateReturn {
  const {
    form,
    subdivisionsByCountry,
    countryField = "country",
    stateField = "state",
    postalCodeField = "postalCode",
  } = options;

  const formState = fromStore(form.form as Readable<Record<string, string>>);

  let stateOptions = $state<SubdivisionOption[]>([]);

  function handleCountryChange(countryCode: string) {
    form.form.update((data: Record<string, string>) => {
      data[stateField] = "";
      return data;
    });
    stateOptions = countryCode ? (subdivisionsByCountry[countryCode] ?? []) : [];

    if (formState.current[postalCodeField]) {
      form.validate(postalCodeField);
    }
  }

  // Hydrate state options from initial country value on mount
  $effect(() => {
    const country = formState.current[countryField];
    if (country) {
      stateOptions = subdivisionsByCountry[country] ?? [];
    }
  });

  return {
    get stateOptions() {
      return stateOptions;
    },
    get showState() {
      return stateOptions.length > 0;
    },
    handleCountryChange,
  };
}
