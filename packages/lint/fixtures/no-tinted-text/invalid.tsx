export function Invalid() {
  return (
    <div>
      {/* expect-error: wallow/no-tinted-text */}
      <p className="text-foreground/60">muted</p>
      {/* expect-error: wallow/no-tinted-text */}
      <span className="hover:text-primary/80">hover</span>
    </div>
  );
}
