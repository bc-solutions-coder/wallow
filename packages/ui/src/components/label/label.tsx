import { Field, type FieldLabelProps } from "../field/field";

/**
 * A Field.Label alias. Must be rendered inside Field; associates with that field's control
 * unless htmlFor explicitly names another control.
 */
export const Label = Field.Label;

/**
 * Props for Field.Label, also accepted by the Label alias.
 */
export type LabelProps = FieldLabelProps;
