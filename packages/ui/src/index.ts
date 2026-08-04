// The root barrel re-exports every folder under src/components — and only
// folders — one line per folder, ordered by folder name so it reads against
// `ls src/components`. PUBLIC_RUNTIME_EXPORTS in src/index.test.ts is the exact
// set, in both directions; keep the two in lockstep.
//
// Components and their prop types only: each component's CVA recipe stays on
// its own subpath (@bc-solutions-coder/ui/<name>), so styling internals do not
// widen the package's headline surface.

export {
  Accordion,
  type AccordionHeaderProps,
  type AccordionItemProps,
  type AccordionPanelProps,
  type AccordionRootProps,
  type AccordionTriggerProps,
} from "./components/accordion";
export {
  AlertDialog,
  type AlertDialogBackdropProps,
  type AlertDialogCloseProps,
  type AlertDialogDescriptionProps,
  type AlertDialogPopupProps,
  type AlertDialogPortalProps,
  type AlertDialogRootProps,
  type AlertDialogTitleProps,
  type AlertDialogTriggerProps,
  type AlertDialogViewportProps,
} from "./components/alert-dialog";
export {
  Autocomplete,
  type AutocompleteArrowProps,
  type AutocompleteBackdropProps,
  type AutocompleteClearProps,
  type AutocompleteCollectionProps,
  type AutocompleteEmptyProps,
  type AutocompleteGroupLabelProps,
  type AutocompleteGroupProps,
  type AutocompleteIconProps,
  type AutocompleteInputGroupProps,
  type AutocompleteInputProps,
  type AutocompleteItemProps,
  type AutocompleteListProps,
  type AutocompletePopupProps,
  type AutocompletePortalProps,
  type AutocompletePositionerProps,
  type AutocompleteRootProps,
  type AutocompleteRowProps,
  type AutocompleteSeparatorProps,
  type AutocompleteStatusProps,
  type AutocompleteTriggerProps,
  type AutocompleteValueProps,
} from "./components/autocomplete";
export {
  Avatar,
  type AvatarFallbackProps,
  type AvatarImageProps,
  type AvatarRootProps,
} from "./components/avatar";
export { Badge, type BadgeProps } from "./components/badge";
export { Button, type ButtonProps, type ButtonVariant } from "./components/button";
export {
  Card,
  CardHeader,
  type CardHeaderProps,
  type CardProps,
  CardTitle,
  type CardTitleProps,
} from "./components/card";
export {
  CenteredCardLayout,
  type CenteredCardLayoutProps,
} from "./components/centered-card-layout";
export {
  Checkbox,
  type CheckboxIndicatorProps,
  type CheckboxRootProps,
} from "./components/checkbox";
export { CheckboxGroup, type CheckboxGroupProps } from "./components/checkbox-group";
export {
  Collapsible,
  type CollapsiblePanelProps,
  type CollapsibleRootProps,
  type CollapsibleTriggerProps,
} from "./components/collapsible";
export {
  Combobox,
  type ComboboxArrowProps,
  type ComboboxBackdropProps,
  type ComboboxChipProps,
  type ComboboxChipRemoveProps,
  type ComboboxChipsProps,
  type ComboboxClearProps,
  type ComboboxCollectionProps,
  type ComboboxEmptyProps,
  type ComboboxGroupLabelProps,
  type ComboboxGroupProps,
  type ComboboxIconProps,
  type ComboboxInputGroupProps,
  type ComboboxInputProps,
  type ComboboxItemIndicatorProps,
  type ComboboxItemProps,
  type ComboboxLabelProps,
  type ComboboxListProps,
  type ComboboxPopupProps,
  type ComboboxPortalProps,
  type ComboboxPositionerProps,
  type ComboboxRootProps,
  type ComboboxRowProps,
  type ComboboxSeparatorProps,
  type ComboboxStatusProps,
  type ComboboxTriggerProps,
  type ComboboxValueProps,
} from "./components/combobox";
export {
  ContextMenu,
  type ContextMenuArrowProps,
  type ContextMenuBackdropProps,
  type ContextMenuCheckboxItemIndicatorProps,
  type ContextMenuCheckboxItemProps,
  type ContextMenuGroupLabelProps,
  type ContextMenuGroupProps,
  type ContextMenuItemProps,
  type ContextMenuLinkItemProps,
  type ContextMenuPopupProps,
  type ContextMenuPortalProps,
  type ContextMenuPositionerProps,
  type ContextMenuRadioGroupProps,
  type ContextMenuRadioItemIndicatorProps,
  type ContextMenuRadioItemProps,
  type ContextMenuRootProps,
  type ContextMenuSeparatorProps,
  type ContextMenuSubmenuRootProps,
  type ContextMenuSubmenuTriggerProps,
  type ContextMenuTriggerProps,
} from "./components/context-menu";
export {
  Dialog,
  type DialogBackdropProps,
  type DialogCloseProps,
  type DialogDescriptionProps,
  type DialogPopupProps,
  type DialogPortalProps,
  type DialogRootProps,
  type DialogTitleProps,
  type DialogTriggerProps,
  type DialogViewportProps,
} from "./components/dialog";
export { DocumentStyles, type DocumentStylesProps } from "./components/document-styles";
export {
  Drawer,
  type DrawerBackdropProps,
  type DrawerCloseProps,
  type DrawerContentProps,
  type DrawerDescriptionProps,
  type DrawerIndentBackgroundProps,
  type DrawerIndentProps,
  type DrawerPopupProps,
  type DrawerPortalProps,
  type DrawerProviderProps,
  type DrawerRootProps,
  type DrawerSwipeAreaProps,
  type DrawerTitleProps,
  type DrawerTriggerProps,
  type DrawerViewportProps,
  type DrawerVirtualKeyboardProviderProps,
} from "./components/drawer";
export { EmptyState, type EmptyStateProps } from "./components/empty-state";
export { ErrorBanner, type ErrorBannerProps } from "./components/error-banner";
export {
  Field,
  type FieldControlProps,
  type FieldDescriptionProps,
  type FieldErrorProps,
  type FieldItemProps,
  type FieldLabelProps,
  type FieldProps,
  type FieldRootProps,
  type FieldValidityProps,
} from "./components/field";
export {
  Fieldset,
  type FieldsetLegendProps,
  type FieldsetProps,
  type FieldsetRootProps,
} from "./components/fieldset";
export { FocusOnNavigate, MAIN_HEADING_SELECTOR } from "./components/focus-on-navigate";
export { ForkAttribution, type ForkAttributionProps } from "./components/fork-attribution";
export {
  Form,
  type FormActions,
  type FormErrors,
  type FormProps,
  type FormSubmitEventDetails,
  type FormValidationMode,
} from "./components/form";
export { Input, type InputProps } from "./components/input";
export { Label, type LabelProps } from "./components/label";
export { ListCard, type ListCardProps } from "./components/list-card";
export { ListRow, type ListRowProps } from "./components/list-row";
export {
  Menu,
  type MenuArrowProps,
  type MenuBackdropProps,
  type MenuCheckboxItemIndicatorProps,
  type MenuCheckboxItemProps,
  type MenuGroupLabelProps,
  type MenuGroupProps,
  type MenuItemProps,
  type MenuLinkItemProps,
  type MenuPopupProps,
  type MenuPortalProps,
  type MenuPositionerProps,
  type MenuRadioGroupProps,
  type MenuRadioItemIndicatorProps,
  type MenuRadioItemProps,
  type MenuRootProps,
  type MenuSeparatorProps,
  type MenuSubmenuRootProps,
  type MenuSubmenuTriggerProps,
  type MenuTriggerProps,
  type MenuViewportProps,
} from "./components/menu";
export { Menubar, type MenubarProps } from "./components/menubar";
export {
  Meter,
  type MeterIndicatorProps,
  type MeterLabelProps,
  type MeterRootProps,
  type MeterTrackProps,
  type MeterValueProps,
} from "./components/meter";
export { MutedText, type MutedTextProps } from "./components/muted-text";
export {
  NavigationMenu,
  type NavigationMenuArrowProps,
  type NavigationMenuBackdropProps,
  type NavigationMenuContentProps,
  type NavigationMenuIconProps,
  type NavigationMenuItemProps,
  type NavigationMenuLinkProps,
  type NavigationMenuListProps,
  type NavigationMenuPopupProps,
  type NavigationMenuPortalProps,
  type NavigationMenuPositionerProps,
  type NavigationMenuRootProps,
  type NavigationMenuTriggerProps,
  type NavigationMenuViewportProps,
} from "./components/navigation-menu";
export { NoticeBanner, type NoticeBannerProps } from "./components/notice-banner";
export {
  NumberField,
  type NumberFieldDecrementProps,
  type NumberFieldGroupProps,
  type NumberFieldIncrementProps,
  type NumberFieldInputProps,
  type NumberFieldRootProps,
  type NumberFieldScrubAreaCursorProps,
  type NumberFieldScrubAreaProps,
} from "./components/number-field";
export {
  OTPField,
  type OTPFieldInputProps,
  type OTPFieldRootProps,
  type OTPFieldSeparatorProps,
} from "./components/otp-field";
export { PageContainer, type PageContainerProps } from "./components/page-container";
export { PageHeader, type PageHeaderProps } from "./components/page-header";
export {
  Popover,
  type PopoverArrowProps,
  type PopoverBackdropProps,
  type PopoverCloseProps,
  type PopoverDescriptionProps,
  type PopoverPopupProps,
  type PopoverPortalProps,
  type PopoverPositionerProps,
  type PopoverRootProps,
  type PopoverTitleProps,
  type PopoverTriggerProps,
  type PopoverViewportProps,
} from "./components/popover";
export {
  PreviewCard,
  type PreviewCardArrowProps,
  type PreviewCardBackdropProps,
  type PreviewCardPopupProps,
  type PreviewCardPortalProps,
  type PreviewCardPositionerProps,
  type PreviewCardRootProps,
  type PreviewCardTriggerProps,
  type PreviewCardViewportProps,
} from "./components/preview-card";
export {
  Progress,
  type ProgressIndicatorProps,
  type ProgressLabelProps,
  type ProgressRootProps,
  type ProgressStatus,
  type ProgressTrackProps,
  type ProgressValueProps,
} from "./components/progress";
export { QuietLink, type QuietLinkProps } from "./components/quiet-link";
export { Radio, type RadioIndicatorProps, type RadioRootProps } from "./components/radio";
export {
  RadioGroup,
  type RadioGroupOrientation,
  type RadioGroupProps,
} from "./components/radio-group";
export { READY_ATTRIBUTE, ReadyIndicator } from "./components/ready-indicator";
export {
  ScrollArea,
  type ScrollAreaContentProps,
  type ScrollAreaCornerProps,
  type ScrollAreaRootProps,
  type ScrollAreaScrollbarProps,
  type ScrollAreaThumbProps,
  type ScrollAreaViewportProps,
} from "./components/scroll-area";
export {
  Select,
  type SelectArrowProps,
  type SelectBackdropProps,
  type SelectGroupLabelProps,
  type SelectGroupProps,
  type SelectIconProps,
  type SelectItemIndicatorProps,
  type SelectItemProps,
  type SelectItemTextProps,
  type SelectLabelProps,
  type SelectListProps,
  type SelectPopupProps,
  type SelectPortalProps,
  type SelectPositionerProps,
  type SelectRootProps,
  type SelectScrollDownArrowProps,
  type SelectScrollUpArrowProps,
  type SelectSeparatorProps,
  type SelectTriggerProps,
  type SelectValueProps,
} from "./components/select";
export { Separator, type SeparatorProps } from "./components/separator";
export {
  SimpleSelect,
  type SimpleSelectOption,
  type SimpleSelectProps,
} from "./components/simple-select";
export {
  Slider,
  type SliderControlProps,
  type SliderIndicatorProps,
  type SliderLabelProps,
  type SliderRootProps,
  type SliderThumbProps,
  type SliderTrackProps,
  type SliderValueProps,
} from "./components/slider";
export { Switch, type SwitchRootProps, type SwitchThumbProps } from "./components/switch";
export {
  Tabs,
  type TabsIndicatorProps,
  type TabsListProps,
  type TabsPanelProps,
  type TabsRootProps,
  type TabsTabProps,
} from "./components/tabs";
export { Text, type TextProps } from "./components/text";
export { Textarea, type TextareaProps } from "./components/textarea";
export {
  resolveThemeMode,
  THEME_STORAGE_KEY,
  type ThemeContextValue,
  type ThemeMode,
  type ThemePreference,
  ThemeProvider,
  type ThemeProviderProps,
  type ThemeResolutionInput,
  ThemeScript,
  type ThemeScriptProps,
  themeInitScript,
  useTheme,
} from "./components/theme-provider";
export {
  THEME_PREFERENCE_CYCLE,
  ThemeToggle,
  type ThemeToggleProps,
} from "./components/theme-toggle";
export {
  createToastManager,
  Toast,
  type ToastActionProps,
  type ToastArrowProps,
  type ToastCloseProps,
  type ToastContentProps,
  type ToastDescriptionProps,
  type ToastManager,
  type ToastManagerAddOptions,
  type ToastManagerPromiseOptions,
  type ToastManagerUpdateOptions,
  type ToastNamespace,
  type ToastObject,
  type ToastPortalProps,
  type ToastPositionerProps,
  type ToastProviderProps,
  type ToastRootProps,
  type ToastTitleProps,
  type ToastViewportProps,
  useToastManager,
  type UseToastManagerReturnValue,
} from "./components/toast";
export { Toggle, type ToggleProps } from "./components/toggle";
export { ToggleGroup, type ToggleGroupProps } from "./components/toggle-group";
export {
  Toolbar,
  type ToolbarButtonProps,
  type ToolbarGroupProps,
  type ToolbarInputProps,
  type ToolbarLinkProps,
  type ToolbarRootProps,
  type ToolbarSeparatorProps,
} from "./components/toolbar";
export {
  Tooltip,
  type TooltipArrowProps,
  type TooltipPopupProps,
  type TooltipPortalProps,
  type TooltipPositionerProps,
  type TooltipProviderProps,
  type TooltipRootProps,
  type TooltipTriggerProps,
  type TooltipViewportProps,
} from "./components/tooltip";
