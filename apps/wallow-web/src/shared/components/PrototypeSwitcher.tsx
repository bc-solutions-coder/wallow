/**
 * PROTOTYPE — throwaway. Floating bottom-centre bar that cycles a `?variant=`
 * search param so UI variants on a route are shareable and reload-stable.
 * Hidden in production builds. Not part of the design under evaluation.
 */
import { Text } from "@bc-solutions-coder/ui";
import { useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";

export interface PrototypeVariant {
  key: string;
  name: string;
}

export function PrototypeSwitcher(props: {
  variants: readonly PrototypeVariant[];
  current: string;
}) {
  const { variants, current } = props;
  const navigate = useNavigate();
  const index = Math.max(
    0,
    variants.findIndex((v) => v.key === current),
  );

  const go = (delta: number): void => {
    const next = variants[(index + delta + variants.length) % variants.length];
    void navigate({
      to: ".",
      search: (prev: Record<string, unknown>) => ({ ...prev, variant: next.key }),
      replace: true,
    });
  };

  useEffect(() => {
    const onKey = (event: KeyboardEvent): void => {
      const target = event.target as HTMLElement | null;
      if (
        target &&
        (target.tagName === "INPUT" ||
          target.tagName === "TEXTAREA" ||
          target.isContentEditable)
      ) {
        return;
      }
      if (event.key === "ArrowLeft") go(-1);
      if (event.key === "ArrowRight") go(1);
    };
    globalThis.addEventListener("keydown", onKey);
    return () => globalThis.removeEventListener("keydown", onKey);
  });

  if (import.meta.env.PROD) return null;

  const active = variants[index];
  return (
    <div
      data-testid="prototype-switcher"
      className="fixed bottom-4 left-1/2 z-50 flex -translate-x-1/2 items-center gap-3 rounded-full bg-black px-4 py-2 text-white shadow-lg ring-2 ring-yellow-400"
    >
      <button type="button" onClick={() => go(-1)} aria-label="Previous variant" className="px-2">
        ←
      </button>
      <Text as="span" variant="bodySm" className="font-mono text-white">
        PROTOTYPE {active.key} ({active.name}) · {index + 1}/{variants.length}
      </Text>
      <button type="button" onClick={() => go(1)} aria-label="Next variant" className="px-2">
        →
      </button>
    </div>
  );
}
