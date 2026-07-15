import { createFileRoute } from "@tanstack/react-router";

/**
 * The public home page (Wallow-8w1h.2.2). It server-renders a heading carrying
 * `data-testid="home-heading"`, which is the SSR contract the boot smoke test
 * (and `curl /`) assert against.
 */
function HomeComponent() {
  return (
    <main>
      <h1 data-testid="home-heading">Welcome to Wallow</h1>
    </main>
  );
}

export const Route = createFileRoute("/")({
  component: HomeComponent,
});
