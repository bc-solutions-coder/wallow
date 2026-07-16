import type { ReactNode } from "react";

/**
 * The Privacy Policy screen (Wallow-vec7.3.3), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Privacy.razor`.
 *
 * A static document: no query parameters, no SDK calls, no state.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `privacy-heading`, `privacy-content`, `privacy-back-button`.
 *
 * The prose is the oracle's, section for section and word for word — this is a
 * legal document, so porting it is transcription, not authorship. (`@@wallow.dev`
 * in the oracle is Razor's escape for a literal `@`.)
 *
 * The section list is data rather than nine hand-written blocks of JSX: the
 * failure mode of porting a wall of text by eye is silently dropping one, and a
 * list makes a dropped section obvious. The sibling `TermsPage` has the same
 * shape and deliberately keeps its own copy of it — auth features in this app are
 * self-contained (`ResetPasswordForm` and `ForgotPasswordForm` likewise each own
 * their chrome), and the two documents share a layout today by coincidence rather
 * than by contract.
 */

/** The oracle's `Last updated` line. */
const LAST_UPDATED = "March 20, 2026";

/** The nine numbered sections, in the oracle's order, with the oracle's prose. */
const SECTIONS: readonly { readonly heading: string; readonly body: string }[] = [
  {
    heading: "Information We Collect",
    body: "We collect information you provide directly, such as your email address and password when creating an account. We also collect usage data including access times, pages viewed, and the referring URL.",
  },
  {
    heading: "How We Use Your Information",
    body: "We use the information we collect to provide, maintain, and improve our services, to communicate with you about your account, and to protect the security of our platform.",
  },
  {
    heading: "Information Sharing",
    body: "We do not sell your personal information. We may share information with third-party service providers who assist us in operating our platform, subject to confidentiality obligations.",
  },
  {
    heading: "Data Security",
    body: "We implement appropriate technical and organizational measures to protect your personal information against unauthorized access, alteration, disclosure, or destruction.",
  },
  {
    heading: "Your Rights",
    body: "You have the right to access, correct, or delete your personal information. You may also request a copy of your data or ask us to restrict its processing. Contact us to exercise these rights.",
  },
  {
    heading: "Cookies",
    body: "We use essential cookies to maintain your session and preferences. We do not use third-party tracking cookies. You can configure your browser to refuse cookies, though some features may not function properly.",
  },
  {
    heading: "Children's Privacy",
    body: "Our service is not directed to individuals under the age of 13. We do not knowingly collect personal information from children. If we become aware of such collection, we will take steps to delete the information.",
  },
  {
    heading: "Changes to This Policy",
    body: 'We may update this Privacy Policy from time to time. We will notify you of any changes by posting the new policy on this page and updating the "Last updated" date.',
  },
  {
    heading: "Contact",
    body: "If you have any questions about this Privacy Policy, please contact us at privacy@wallow.dev.",
  },
];

/** The oracle's `BbCardHeader`. */
function DocumentHeading() {
  return (
    <div className="px-0 pt-0 text-center space-y-1">
      <h2 className="text-lg font-semibold text-card-foreground" data-testid="privacy-heading">
        Privacy Policy
      </h2>
      <p className="text-sm text-muted-foreground">Last updated: {LAST_UPDATED}</p>
    </div>
  );
}

/** One numbered `<section>` of the policy. */
function PolicySection({
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
function PolicyBody() {
  return (
    <div className="space-y-4 text-sm text-muted-foreground" data-testid="privacy-content">
      {SECTIONS.map((section, index) => (
        <PolicySection
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
        data-testid="privacy-back-button"
        className="block w-full rounded-md border border-border px-3 py-2 text-center text-sm font-medium text-foreground"
      >
        Back to Register
      </a>
    </div>
  );
}

export function PrivacyPage(): ReactNode {
  return (
    <div className="space-y-4">
      <DocumentHeading />
      <PolicyBody />
      <BackToRegister />
    </div>
  );
}
