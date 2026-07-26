# Changelog

## [0.2.0](https://github.com/bc-solutions-coder/wallow/compare/sdk-v0.1.0...sdk-v0.2.0) (2026-07-26)


### Features

* **sdk:** absorb csrf, ssr context, and facade helpers ([a1f64f6](https://github.com/bc-solutions-coder/wallow/commit/a1f64f608ed94d1e967bc309c86dc983319ab2b8))
* **sdk:** add apps query module ([e020264](https://github.com/bc-solutions-coder/wallow/commit/e02026443adc91a1c0bec2196c7a74c8fb9a9541))
* **sdk:** add auth facade and oidc helpers for tanstack auth app ([5d761ea](https://github.com/bc-solutions-coder/wallow/commit/5d761eabab40a0e4e6f1ddc99049ae3f6e4c600a))
* **sdk:** add auth query module ([182881f](https://github.com/bc-solutions-coder/wallow/commit/182881f3788d9c72330033a7ec2e1f44f494ddf0))
* **sdk:** add central query-key factory ([0ebad6f](https://github.com/bc-solutions-coder/wallow/commit/0ebad6fe484072c24f04b9a0631ca031aa71d4ee))
* **sdk:** add getCurrentUser auth-state facade method ([ddb02f3](https://github.com/bc-solutions-coder/wallow/commit/ddb02f37da7ffc1fcf57d345e9905d68cefb3c65))
* **sdk:** add inquiries query module ([e844369](https://github.com/bc-solutions-coder/wallow/commit/e84436918b108df8310f822f99a04d1e9ca113bd))
* **sdk:** add lazy query-layer bootstrap seam ([565627b](https://github.com/bc-solutions-coder/wallow/commit/565627b4c39041d78cc099fa56fd3e060fb9c3e4))
* **sdk:** add mfa query module ([fabc046](https://github.com/bc-solutions-coder/wallow/commit/fabc046a219afd92a94bb7fde3ee8cb5a5669fc9))
* **sdk:** add organizations query module ([e1891f8](https://github.com/bc-solutions-coder/wallow/commit/e1891f80b031715e4be352cae83ec71c08636155))
* **sdk:** add settings query module ([c755ab5](https://github.com/bc-solutions-coder/wallow/commit/c755ab566f2e7af5da8a4716814d3f55abc75d29))
* **sdk:** add user query module ([13d8d91](https://github.com/bc-solutions-coder/wallow/commit/13d8d91019f03b895e9716acfe5ea83410f3c9a6))
* **sdk:** expose query layer via ./query subpath ([ae76871](https://github.com/bc-solutions-coder/wallow/commit/ae7687136f4d332b13231277af4177170d7b1cc8))
* **sdk:** restructure sdk into pnpm monorepo with redis adapter ([b48cb7c](https://github.com/bc-solutions-coder/wallow/commit/b48cb7c6f5c3e3a746dc7b9f16183e98a76d9b19))
* **sdk:** share mfa api-wrapper slice across apps ([5436863](https://github.com/bc-solutions-coder/wallow/commit/5436863a975f6b3c10979774dd0fdd87125772a1))
* **web:** restore route-tree codegen, centralize styling, regenerate openapi client ([9ea6928](https://github.com/bc-solutions-coder/wallow/commit/9ea69283a1d4885f7af1484358f82fd798c842c1))


### Bug Fixes

* restore example app to pnpm workspace after apps/ relocation ([33d2cdf](https://github.com/bc-solutions-coder/wallow/commit/33d2cdf1f12ea4c920deefb055058b285fd49307))
* **sdk:** add SSR-safe baseUrl and headers options to getUser ([6b6dd54](https://github.com/bc-solutions-coder/wallow/commit/6b6dd54472e4621ae53476023d438ad03c2d9543))
* **sdk:** bump @hey-api/openapi-ts to 0.99 to clear npm security alerts ([1b1ee70](https://github.com/bc-solutions-coder/wallow/commit/1b1ee7051ed65266dd1230ef04e012927abfc323))
* **sdk:** preserve auth error code through error mapping ([d4e27e0](https://github.com/bc-solutions-coder/wallow/commit/d4e27e0921a8eaf055990e3b80482752d061c882))
* **sdk:** scope client-facing auth queries by client_id ([ce5350d](https://github.com/bc-solutions-coder/wallow/commit/ce5350d59796d0a648013dccd90e9a5c3c2d14f0))
* **wallow-auth:** resolve the org-domain interstitial before submitting register ([0320b4d](https://github.com/bc-solutions-coder/wallow/commit/0320b4da0b9e3faa004f234f6d2c38e4c23e2a45))
* **web:** route SSR self-fetch through an internal origin override ([43285af](https://github.com/bc-solutions-coder/wallow/commit/43285aff3b8b9dcea5639ec7d1b330c0654dd2a0))
