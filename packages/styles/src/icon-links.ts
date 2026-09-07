import { toRootRelativeAssetUrl } from "./asset-urls";
import { forkBranding, type ForkBranding } from "./branding";

/** Document icons share fork configuration and the consuming app's URL prefix. */
export function appIconLinks({
  branding = forkBranding,
  basePath = "",
}: {
  readonly branding?: Pick<ForkBranding, "appIcon" | "favicon" | "appleTouchIcon">;
  readonly basePath?: string;
} = {}) {
  return [
    ...(branding.favicon
      ? [
          {
            rel: "icon",
            type: "image/png",
            sizes: "32x32",
            href: toRootRelativeAssetUrl(branding.favicon, basePath),
          },
        ]
      : []),
    { rel: "icon", href: toRootRelativeAssetUrl(branding.appIcon, basePath) },
    ...(branding.appleTouchIcon
      ? [
          {
            rel: "apple-touch-icon",
            sizes: "180x180",
            href: toRootRelativeAssetUrl(branding.appleTouchIcon, basePath),
          },
        ]
      : []),
  ];
}
