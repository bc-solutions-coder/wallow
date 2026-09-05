# Changelog

## 1.0.0 (2026-09-05)


### ⚠ BREAKING CHANGES

* **wallow-web:** wallow-web's read and mutation sites no longer accept per-site error fallback strings; `errorText` is no longer used by the app.
* **ui,query:** failure surfaces — toast, banner, provider, client callback
* **sdk:** the SDK-private code constants CSRF_INVALID_CODE, NETWORK_ERROR_CODE, and NETWORK_TIMEOUT_CODE are deleted (pinned deleted by src/index.test.ts); their wire values are now Bff.CsrfInvalid, Transport.NetworkError, and Transport.Timeout from api-errors, and the proxy's forward timeout answers 504 instead of 503. The bodiless 401s from the proxy and /bff/user now carry a problem body.
* **sdk:** throw ApiFailure from the SDK and retarget consumers

### Features

* **api-errors:** publish the dependency-free failure package ([1781636](https://github.com/bc-solutions-coder/wallow/commit/17816366f1343cb410ee849a34dc6dc2e564ef4f)), closes [#179](https://github.com/bc-solutions-coder/wallow/issues/179)
* **sdk:** originate BFF proxy and passthrough failures as problems ([732b573](https://github.com/bc-solutions-coder/wallow/commit/732b573db2ee829b31c08edb441606a035002a84)), closes [#181](https://github.com/bc-solutions-coder/wallow/issues/181)
* **sdk:** throw ApiFailure from the SDK and retarget consumers ([5b04bc3](https://github.com/bc-solutions-coder/wallow/commit/5b04bc3e63bcddb085ac348fd5e94a364f3db011)), closes [#180](https://github.com/bc-solutions-coder/wallow/issues/180)
* **ui,query:** failure surfaces — toast, banner, provider, client callback ([3d3533a](https://github.com/bc-solutions-coder/wallow/commit/3d3533a0d1714f9b16fba9a23c5e0206af567419)), closes [#182](https://github.com/bc-solutions-coder/wallow/issues/182)
* **wallow-web:** migrate read and mutation sites onto the failure surfaces ([08e0abc](https://github.com/bc-solutions-coder/wallow/commit/08e0abc2a9095f1f5e97a56c58f63c5fe71139c0))
