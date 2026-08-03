/**
 * LandingPage (Wallow-urec.3.1) — the public marketing body, ported from the
 * retired Blazor `Home.razor`: a hero (rooted fork icon, the tagline as the h1,
 * a subhead, and the Get Started / View on GitHub CTAs), six tech badges, a
 * six-card features grid under the `#features` anchor, and a three-step
 * quick-start band.
 *
 * The Razor original's hardcoded hex palette is replaced by the shared theme
 * tokens (`packages/styles`). The outbound fork URL comes off `forkLinks()`,
 * the same seam `PublicLayout` reads, so the two cannot drift and both follow
 * the deployment's environment.
 *
 * Like `PublicLayout`, it stays router-free — plain anchors only, never a
 * TanStack `Link` — so the `/` route still server-renders standalone. The
 * sections are split into subcomponents to keep each one's JSX nesting inside
 * the repo's `jsx-max-depth` budget.
 */

import { appIconUrl, forkBranding } from "@bc-solutions-coder/styles";
import { MutedText, Text } from "@bc-solutions-coder/ui";

import { forkLinks } from "@shared/lib/fork-links";
import { getStartedHref } from "@shared/lib/site-links";

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
  const { repositoryUrl } = forkLinks();

  return (
    <section className="text-center py-24 px-6">
      <img
        src={appIconUrl}
        alt={forkBranding.appName}
        data-testid="home-hero-icon"
        className="size-40 mx-auto mb-6"
        style={{ shapeRendering: "geometricPrecision" }}
      />
      {/* `display` is text-4xl; the hero is one step larger, and the marketing
          scale is deliberate, so the size rides as a className override. The
          tagline stays the h1's ONLY child — `routes/index.test.tsx` matches the
          server-rendered `<h1>` against it. */}
      <Text as="h1" variant="display" className="text-5xl mb-4" data-testid="home-heading">
        {forkBranding.tagline}
      </Text>
      <Text as="p" color="muted" className="text-lg max-w-2xl mx-auto mb-10">
        A modular .NET foundation for building production-ready applications. Multi-tenant,
        event-driven, and built on Clean Architecture from day one.
      </Text>
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
          className="border border-foreground text-foreground font-medium px-8 py-3 rounded-full hover:bg-sidebar hover:text-sidebar-foreground no-underline text-base transition-colors"
        >
          View on GitHub
        </a>
      </div>
    </section>
  );
}

/**
 * The platform's stack, as pill badges. Deliberately NOT the `Badge` primitive:
 * these are marketing pills with their own geometry, not the status chips
 * `Badge` encodes.
 */
function TechBadges() {
  return (
    <section className="flex flex-wrap items-center justify-center gap-3 px-6 pb-20">
      {techBadges.map((badge: string) => (
        <Text
          key={badge}
          as="span"
          variant="bodySm"
          color="accent"
          weight="medium"
          className="bg-accent px-4 py-1.5 rounded-full"
          data-testid="home-tech-badge"
        >
          {badge}
        </Text>
      ))}
    </section>
  );
}

/** One feature card: emoji, title, body. The `h3` holds the title alone. */
function FeatureCard(props: Feature) {
  return (
    <div className="bg-card border-l-4 border-primary rounded-lg p-6 shadow-sm">
      <div className="text-2xl mb-2">{props.emoji}</div>
      {/* `subheading` is text-xl; the card title sits one step down, so the
          marketing size rides as a className override. */}
      <Text
        as="h3"
        variant="subheading"
        color="onCard"
        className="text-lg mb-2"
        data-testid="home-feature-title"
      >
        {props.title}
      </Text>
      <MutedText>{props.body}</MutedText>
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
      <Text as="h2" variant="title" className="text-center mb-12">
        Everything you need to build
      </Text>
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
      {/* `onSidebar` on both: this step sits inside the inverted band, where
          `Text`'s default `text-foreground` would be dark-on-dark. */}
      <Text
        as="h3"
        variant="subheading"
        color="onSidebar"
        className="text-lg mb-2"
        data-testid="home-step-title"
      >
        {props.title}
      </Text>
      <Text as="code" color="onSidebar">
        {props.command}
      </Text>
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
    <section className="bg-sidebar text-sidebar-foreground py-20 px-6">
      <div className="max-w-4xl mx-auto text-center">
        <Text as="h2" variant="title" color="onSidebar" className="mb-12">
          Get running in minutes
        </Text>
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
