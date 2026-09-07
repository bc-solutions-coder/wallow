function LogoutGlyph({ signedOut }: { readonly signedOut: boolean }) {
  return (
    <svg
      viewBox="0 0 48 48"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      focusable="false"
    >
      {signedOut ? (
        <path className="logout-check" pathLength="1" d="m14 24 7 7 14-15" />
      ) : (
        <>
          <path d="M22 10H12a2 2 0 0 0-2 2v24a2 2 0 0 0 2 2h10" />
          <path className="logout-arrow" d="M20 24h18m-7-7 7 7-7 7" />
        </>
      )}
    </svg>
  );
}

export function LogoutIllustration({ signedOut }: { readonly signedOut: boolean }) {
  return (
    <div className="logout-illustration" aria-hidden="true">
      <div className="logout-orbit" />
      <div className="logout-icon">
        <LogoutGlyph key={String(signedOut)} signedOut={signedOut} />
      </div>
    </div>
  );
}
