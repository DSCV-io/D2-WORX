/**
 * Composable for expandable address line fields.
 *
 * Toggles visibility of extra street address lines and
 * clears their values when hidden.
 */
/**
 * Minimal form shape — uses `any` for the store property to avoid
 * SuperForm generic invariance issues.
 */
interface FormLike {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  form: any;
}

export interface UseAddressLinesOptions {
  form: FormLike;
  /** Extra field names to toggle. @default ["street2", "street3"] */
  extraFields?: string[];
}

export interface UseAddressLinesReturn {
  /** Whether extra address lines are visible. */
  readonly showExtraLines: boolean;
  /** Toggle visibility; clears extra fields when hiding. */
  toggleExtraLines: () => void;
}

export function useAddressLines(options: UseAddressLinesOptions): UseAddressLinesReturn {
  const { form, extraFields = ["street2", "street3"] } = options;

  let showExtraLines = $state(false);

  function toggleExtraLines() {
    showExtraLines = !showExtraLines;
    if (!showExtraLines) {
      form.form.update((data: Record<string, string>) => {
        for (const field of extraFields) {
          data[field] = "";
        }
        return data;
      });
    }
  }

  return {
    get showExtraLines() {
      return showExtraLines;
    },
    toggleExtraLines,
  };
}
