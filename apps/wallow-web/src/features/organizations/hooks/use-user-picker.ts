/**
 * The add-member picker's whole state: the user directory it searches, the
 * narrowing the typed text does to it, and whether the popup is actually open.
 *
 * It exists because those three lived in two different components. The directory
 * read sat in `AddMemberForm` and was threaded down as a `users` prop, while the
 * open flag and the filter sat in `UserIdPicker` — so the rule that ties them
 * together (a popup is mounted only while it has something to show) was spelled
 * in one component out of state owned by two. Collapsing them means the picker
 * calls this hook itself and the prop is gone.
 *
 * Feature-local rather than `shared/hooks/`: one feature uses it.
 */

import { useQuery } from "@bc-solutions-coder/query";
import type { UserDto } from "@bc-solutions-coder/sdk";
import { useRouteContext } from "@tanstack/react-router";
import { useMemo, useState } from "react";

import { usersGetUsersOptions } from "../api";

/** What {@link useUserPicker} hands the control. */
export interface UserPicker {
  /**
   * The directory narrowed to `value`, and what the popup lists.
   *
   * Narrowed HERE rather than by Base UI's collator, because the input's text is
   * a user id once somebody has picked one: matching that id against the emails
   * on screen would re-open an empty popup over the submit button.
   */
  readonly matches: readonly UserDto[];
  /**
   * Whether the popup is mounted — the request AND something to show, never the
   * request alone. `aria-expanded` must not claim a list that would be empty,
   * and an invisible surface must not sit between the operator and `Add member`.
   */
  readonly open: boolean;
  /** Base UI's `onOpenChange`: what the control ASKS for, which {@link open} then gates. */
  readonly onOpenChange: (next: boolean) => void;
}

/**
 * @param value The control's current text — a partial email while the operator
 * is searching, a user id once they have picked or typed one in full.
 */
export function useUserPicker(value: string): UserPicker {
  const { sdk } = useRouteContext({ from: "__root__" });
  const [requestedOpen, setRequestedOpen] = useState(false);

  // `items` is what the paged endpoint returns. Held UNDEFAULTED so the memo has
  // a stable dependency — a `?? []` here would mint a fresh array on every render
  // and re-narrow the whole directory each time. The default moves inside.
  const directory = useQuery(usersGetUsersOptions({ client: sdk.client }));
  const users: readonly UserDto[] | undefined = directory.data?.items;

  const query: string = value.trim().toLowerCase();
  const matches: readonly UserDto[] = useMemo(
    // `?? []` also covers the read that has not landed yet, so the control is
    // usable before the directory is.
    () => (users ?? []).filter((user) => user.email.toLowerCase().includes(query)),
    [users, query],
  );

  return {
    matches,
    open: requestedOpen && matches.length > 0,
    onOpenChange: setRequestedOpen,
  };
}
