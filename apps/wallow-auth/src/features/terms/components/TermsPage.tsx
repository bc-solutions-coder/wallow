import type { ReactNode } from "react";

/**
 * The Terms of Service screen (Wallow-vec7.3.3), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Terms.razor`.
 *
 * A static document — NOT the ToS gate at `/accept-terms`, which is a different
 * screen owned by Wallow-vec7.3.10. This page renders prose and a way back: it
 * has no checkbox and no submit, because nothing here is accepted.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `terms-heading`, `terms-content`, `terms-back-button`.
 *
 * The prose is the oracle's, section for section and word for word — this is a
 * legal document, so porting it is transcription, not authorship. (`@@wallow.dev`
 * in the oracle is Razor's escape for a literal `@`.)
 *
 * On why the sections are data and why this file keeps its own copy of a layout
 * the sibling `PrivacyPage` also has, see that file's header.
 */

/** The oracle's `Last updated` line. */
const LAST_UPDATED = "March 20, 2026";

/** The nine numbered sections, in the oracle's order, with the oracle's prose. */
const SECTIONS: readonly { readonly heading: string; readonly body: string }[] = [
  {
    heading: "Acceptance of Terms",
    body: "By accessing or using Wallow, you agree to be bound by these Terms of Service. If you do not agree to these terms, please do not use the service.",
  },
  {
    heading: "Use of Service",
    body: "Wallow provides a platform for managing and organizing your work. You are responsible for maintaining the confidentiality of your account credentials and for all activities that occur under your account.",
  },
  {
    heading: "User Accounts",
    body: "You must provide accurate and complete information when creating an account. You may not use another person's account without permission. You are solely responsible for the activity that occurs on your account.",
  },
  {
    heading: "Prohibited Activities",
    body: "You agree not to engage in any activity that interferes with or disrupts the service, attempt to gain unauthorized access to any part of the service, or use the service for any unlawful purpose.",
  },
  {
    heading: "Intellectual Property",
    body: "The service and its original content, features, and functionality are owned by Wallow and are protected by international copyright, trademark, and other intellectual property laws.",
  },
  {
    heading: "Limitation of Liability",
    body: "In no event shall Wallow be liable for any indirect, incidental, special, consequential, or punitive damages resulting from your use of or inability to use the service.",
  },
  {
    heading: "Termination",
    body: "We may terminate or suspend your account at any time without prior notice or liability for any reason, including if you breach these Terms of Service.",
  },
  {
    heading: "Changes to Terms",
    body: "We reserve the right to modify or replace these terms at any time. Continued use of the service after any changes constitutes acceptance of the new terms.",
  },
  {
    heading: "Contact",
    body: "If you have any questions about these Terms of Service, please contact us at support@wallow.dev.",
  },
];

/** The oracle's `BbCardHeader`. */
function DocumentHeading() {
  return (
    <div className="px-0 pt-0 text-center space-y-1">
      <h2 className="text-lg font-semibold text-card-foreground" data-testid="terms-heading">
        Terms of Service
      </h2>
      <p className="text-sm text-muted-foreground">Last updated: {LAST_UPDATED}</p>
    </div>
  );
}

/** One numbered `<section>` of the terms. */
function TermsSection({
  number,
  heading,
  body,
}: {
  readonly number: number;
  readonly heading: string;
  readonly body: string;
}) {
  return (
    <section className="space-y-2">
      <h3 className="font-medium text-foreground">
        {number}. {heading}
      </h3>
      <p>{body}</p>
    </section>
  );
}

/** The oracle's `BbCardContent`: every section, in order. */
function TermsBody() {
  return (
    <div className="space-y-4 text-sm text-muted-foreground" data-testid="terms-content">
      {SECTIONS.map((section, index) => (
        <TermsSection
          key={section.heading}
          number={index + 1}
          heading={section.heading}
          body={section.body}
        />
      ))}
    </div>
  );
}

/**
 * The oracle's `BbCardFooter`, whose `Href` is `/register` and not `/login`:
 * this page is reached FROM the register form's consent checkboxes, so "back"
 * means back to the form the reader left.
 */
function BackToRegister() {
  return (
    <div className="px-0 pt-6 pb-8 flex justify-center">
      <a
        href="/register"
        data-testid="terms-back-button"
        className="block w-full rounded-md border border-border px-3 py-2 text-center text-sm font-medium text-foreground"
      >
        Back to Register
      </a>
    </div>
  );
}

export function TermsPage(): ReactNode {
  return (
    <div className="space-y-4">
      <DocumentHeading />
      <TermsBody />
      <BackToRegister />
    </div>
  );
}
