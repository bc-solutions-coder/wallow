import { Field, type FieldLabelProps } from "../field/field";

/**
 * The shared field label — the compat name for `Field.Label`, and literally the
 * same component, so the two can never drift apart.
 *
 * Base UI ships NO standalone `label` part: a label only exists as a member of
 * the Field anatomy, because associating itself with the field's control is the
 * whole job. `Label` is therefore an alias rather than a wrapper, and it carries
 * one deliberate behaviour change from the pre-rebuild `<label>` it replaces:
 *
 *   IT MUST BE RENDERED INSIDE A `<Field>`. Outside one, Base UI throws
 *   "FieldRootContext is missing".
 *
 * That holds for every `<Label>` in wallow-auth and wallow-web already — all 12
 * sit inside a `<Field>` — and it is what lets `htmlFor` become optional: with
 * no `htmlFor` the label points at whatever control the field owns, and an
 * explicit `htmlFor` still wins.
 */
export const Label = Field.Label;

/** The pre-rebuild export name for the label's props, kept for compat. */
export type LabelProps = FieldLabelProps;
