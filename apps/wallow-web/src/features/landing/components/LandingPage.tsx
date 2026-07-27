/**
 * LandingPage (Wallow-urec.3.1) — the public marketing body, ported from the
 * retired Blazor `Home.razor`: a hero (rooted fork icon, the tagline as the h1,
 * a subhead, and the Get Started / View on GitHub CTAs), six tech badges, a
 * six-card features grid under the `#features` anchor, and a three-step
 * quick-start band.
 *
 * The Razor original's hardcoded hex palette is replaced by the shared theme
 * tokens (`packages/styles`), and every outbound href comes from
 * `lib/site-links` so it cannot drift from `PublicLayout`'s.
 *
 * Like `PublicLayout`, it stays router-free — plain anchors only, never a
 * TanStack `Link` — so the `/` route still server-renders standalone. The
 * sections are split into subcomponents to keep each one's JSX nesting inside
 * the repo's `jsx-max-depth` budget.
 */

import { appIconUrl, forkBranding } from "../../../lib/branding";
import { getStartedHref, repositoryUrl } from "../../../lib/site-links";

interface Feature {
  emoji: string;
  title: string;
  body: string;
}

interface Step {
  title: string;
  command: string;
}

const techBadges: readonly string[] = [
  ".NET 10",
  "React",
  "PostgreSQL",
  "Wolverine",
  "OpenIddict",
  "Clean Architecture",
];

const features: readonly Feature[] = [
  {
    emoji: "🏢",
    title: "Multi-tenancy",
    body: "Isolated tenant schemas, automatic resolution, and per-tenant configuration out of the box.",
  },
  {
    emoji: "🧩",
    title: "Modular Monolith",
    body: "Independent modules with clear boundaries that can evolve into microservices when you need them.",
  },
  {
    emoji: "🏗️",
    title: "Clean Architecture & DDD",
    body: "Domain-driven design with layered architecture keeping your business logic pure and testable.",
  },
  {
    emoji: "⚡",
    title: "CQRS with Wolverine",
    body: "Command and query separation powered by Wolverine for clean, performant request handling.",
  },
  {
    emoji: "📨",
    title: "Event-Driven Messaging",
    body: "Modules communicate through Wolverine events, keeping boundaries clean and enabling async workflows.",
  },
  {
    emoji: "🔐",
    title: "Identity & OAuth",
    body: "OpenIddict-powered authentication with social logins, roles, and fine-grained permissions.",
  },
];

const steps: readonly Step[] = [
  { title: "Fork & Clone", command: "git clone your-fork" },
  { title: "Start Infrastructure", command: "docker compose up -d" },
  { title: "Run the API", command: "dotnet run" },
];

/**
 * Fork icon, tagline, subhead, and the two CTAs.
 *
 * The tagline is the heading's ONLY child and a bare text node — the SSR
 * contract in `routes/index.test.tsx` matches the `<h1>` with a regex that
 * rejects nested markup.
 */
function Hero() {
  return (
    <section className="text-center py-24 px-6">
      <img
        src={appIconUrl}
        alt={forkBranding.appName}
        data-testid="home-hero-icon"
        className="size-40 mx-auto mb-6"
        style={{ shapeRendering: "geometricPrecision" }}
      />
      <h1 data-testid="home-heading" className="text-5xl font-bold text-foreground mb-4">
        {forkBranding.tagline}
      </h1>
      <p className="text-lg text-foreground/70 max-w-2xl mx-auto mb-10">
        A modular .NET foundation for building production-ready applications. Multi-tenant,
        event-driven, and built on Clean Architecture from day one.
      </p>
      <div className="flex items-center justify-center gap-4">
        <a
          href={getStartedHref}
          data-testid="home-get-started"
          className="bg-primary text-primary-foreground font-medium px-8 py-3 rounded-full hover:opacity-90 no-underline text-base"
        >
          Get Started
        </a>
        <a
          href={repositoryUrl}
          target="_blank"
          rel="noreferrer"
          data-testid="home-github-link"
          className="border border-foreground text-foreground font-medium px-8 py-3 rounded-full hover:bg-foreground hover:text-background no-underline text-base transition-colors"
        >
          View on GitHub
        </a>
      </div>
    </section>
  );
}

/** The platform's stack, as pill badges. */
function TechBadges() {
  return (
    <section className="flex flex-wrap items-center justify-center gap-3 px-6 pb-20">
      {techBadges.map((badge: string) => (
        <span
          key={badge}
          data-testid="home-tech-badge"
          className="bg-accent text-accent-foreground text-sm font-medium px-4 py-1.5 rounded-full"
        >
          {badge}
        </span>
      ))}
    </section>
  );
}

/** One feature card: emoji, title, body. The `h3` holds the title alone. */
function FeatureCard(props: Feature) {
  return (
    <div className="bg-card border-l-4 border-primary rounded-lg p-6 shadow-sm">
      <div className="text-2xl mb-2">{props.emoji}</div>
      <h3
        data-testid="home-feature-title"
        className="text-lg font-semibold text-card-foreground mb-2"
      >
        {props.title}
      </h3>
      <p className="text-sm text-card-foreground/70">{props.body}</p>
    </div>
  );
}

/**
 * The six-card grid. `PublicLayout`'s Features nav link targets `/#features`,
 * so this section owns that anchor id.
 */
function FeaturesGrid() {
  return (
    <section id="features" data-testid="home-features" className="max-w-6xl mx-auto px-6 pb-24">
      <h2 className="text-3xl font-bold text-foreground text-center mb-12">
        Everything you need to build
      </h2>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {features.map((feature: Feature) => (
          <FeatureCard
            key={feature.title}
            emoji={feature.emoji}
            title={feature.title}
            body={feature.body}
          />
        ))}
      </div>
    </section>
  );
}

/** One quick-start step: its 1-based numeral, title, and shell command. */
function QuickStartStep(props: { position: number; title: string; command: string }) {
  return (
    <div>
      <div className="text-primary text-4xl font-bold mb-3">{props.position}</div>
      <h3 data-testid="home-step-title" className="text-lg font-semibold mb-2">
        {props.title}
      </h3>
      <code className="text-sm text-background/60">{props.command}</code>
    </div>
  );
}

/** The three steps, laid out in a row. */
function StepsGrid() {
  return (
    <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
      {steps.map((step: Step, index: number) => (
        <QuickStartStep
          key={step.title}
          position={index + 1}
          title={step.title}
          command={step.command}
        />
      ))}
    </div>
  );
}

/** The inverted quick-start band closing the page. */
function StepsBand() {
  return (
    <section className="bg-foreground text-background py-20 px-6">
      <div className="max-w-4xl mx-auto text-center">
        <h2 className="text-3xl font-bold mb-12">Get running in minutes</h2>
        <StepsGrid />
      </div>
    </section>
  );
}

export function LandingPage() {
  return (
    <>
      <Hero />
      <TechBadges />
      <FeaturesGrid />
      <StepsBand />
    </>
  );
}
