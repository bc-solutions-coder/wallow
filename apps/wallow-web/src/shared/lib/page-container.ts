/**
 * The ONE container rule every dashboard route page applies to its own root
 * (Wallow-lrlm.5.1).
 *
 * Before this constant existed each page picked its own width: the list pages
 * took `max-w-5xl`, the settings/register/inquiry-detail pages took `max-w-2xl`,
 * and the two detail pages disagreed with each other. Nothing named the rule, so
 * a new page had to guess which precedent it belonged to.
 *
 * The width lives here and nowhere else. A dashboard route page spreads this
 * constant onto its root element and writes no `max-w-*` utility of its own;
 * `page-shell.test.ts` pins both halves of that, and `style-contract`'s
 * `expectPageContainer` pins it again on the rendered DOM.
 *
 * This is a class string, not a layout component: the outer shell (nav, main
 * column, padding) is already `DashboardLayout`'s, and F5.T1 deliberately does
 * not add a second wrapper component underneath it.
 */
export const PAGE_CONTAINER: string = "max-w-5xl mx-auto";
