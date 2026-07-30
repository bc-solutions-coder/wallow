/**
 * The dashboard's one composition of the catalog `Select` (Wallow-m5aq.5.3) —
 * used by every wallow-web form that offers a fixed list of options.
 *
 * This is a COMPOSITION, not a reimplementation: every element below comes from
 * `@bc-solutions-coder/ui`. It exists because a usable select is a seven-part
 * portal tree (Root > Trigger > Value/Icon, plus Portal > Positioner > Popup >
 * List > Item > ItemText) and all four call sites need exactly the same one —
 * `testId` on the TRIGGER, since that is the element the E2E suite and the
 * component specs click, the option's LABEL shown once it is chosen, and the
 * wire VALUE in form state. Spelling the tree out per call site would also blow
 * the repo's `react/jsx-max-depth` budget at each of them, which is why the
 * parts below are split into one component per nesting level.
 *
 * TWO TRANSLATIONS HAPPEN HERE, both at this boundary rather than in callers:
 *
 *   - "nothing chosen" is `""` on the app side (TanStack Form's default for a
 *     required select) and `null` in Base UI.
 *   - the trigger reports the LABEL rather than the value, which is what `items`
 *     buys: without it Base UI's `Select.Value` renders the raw value, so a
 *     `web-app` / "Web Application" pair would show the wire value to the user.
 *
 * `label` is REQUIRED (Wallow-lrlm.4.4): the trigger is a `role="combobox"`
 * button with no text content of its own (only the chosen option's label, which
 * is empty until something is picked), so without an explicit name a screen
 * reader announces it as unlabelled. `Field`/`Label` wrap the trigger the same
 * way every other form control in the catalog is named — there is no
 * lower-ceremony way to say "this select is for X" than the caller passing X.
 */
import { Field, Label, Select } from "@bc-solutions-coder/ui";

/** One option: `value` travels on the wire, `label` is what a user reads. */
export interface SelectControlOption {
  readonly value: string;
  readonly label: string;
}

/** One option row in the popup — `role="option"`, named by its label. */
function SelectOption(props: { option: SelectControlOption }) {
  return (
    <Select.Item value={props.option.value}>
      <Select.ItemText>{props.option.label}</Select.ItemText>
    </Select.Item>
  );
}

/** The listbox itself. */
function SelectOptionList(props: { options: readonly SelectControlOption[] }) {
  return (
    <Select.List>
      {props.options.map((option: SelectControlOption) => (
        <SelectOption key={option.value} option={option} />
      ))}
    </Select.List>
  );
}

/** The popup card the list sits on. */
function SelectPopupCard(props: { options: readonly SelectControlOption[] }) {
  return (
    <Select.Popup>
      <SelectOptionList options={props.options} />
    </Select.Popup>
  );
}

/**
 * The portalled half of the select. Nothing below this exists in the DOM while
 * the select is closed — Base UI mounts it on open and unmounts it on close.
 */
function SelectPopupLayer(props: { options: readonly SelectControlOption[] }) {
  return (
    <Select.Portal>
      <Select.Positioner>
        <SelectPopupCard options={props.options} />
      </Select.Positioner>
    </Select.Portal>
  );
}

/** The closed control: the chosen label (or the placeholder) and a chevron. */
function SelectControlTrigger(props: {
  testId: string;
  placeholder?: string | undefined;
  className?: string | undefined;
}) {
  return (
    <Select.Trigger data-testid={props.testId} className={props.className}>
      <Select.Value placeholder={props.placeholder} />
      <Select.Icon />
    </Select.Trigger>
  );
}

export function SelectControl(props: {
  /** Preserved from the pre-migration `<select>`; names the TRIGGER. */
  testId: string;
  /** The accessible name every catalog form control is given via `Label`. */
  label: string;
  value: string;
  options: readonly SelectControlOption[];
  onChange: (value: string) => void;
  /** Shown on the trigger while nothing is chosen. */
  placeholder?: string | undefined;
  className?: string | undefined;
}) {
  const { testId, label, value, options, onChange, placeholder, className } = props;
  return (
    <Field>
      <Label>{label}</Label>
      <Select.Root
        items={options}
        value={value === "" ? null : value}
        onValueChange={(next: string | null) => {
          onChange(next ?? "");
        }}
      >
        <SelectControlTrigger testId={testId} placeholder={placeholder} className={className} />
        <SelectPopupLayer options={options} />
      </Select.Root>
    </Field>
  );
}
