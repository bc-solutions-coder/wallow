# Changelog

## 1.0.0 (2026-09-06)


### ⚠ BREAKING CHANGES

* **api:** give missing tenant scope a dedicated code
* **forms:** retire legacy failure helpers
* **wallow-auth:** the auth and MFA endpoints answer RFC 7807 problems with Auth.*/Mfa.* codes instead of `{ error }` bodies; PasswordlessResult is gone.
* **wallow-web:** wallow-web's read and mutation sites no longer accept per-site error fallback strings; `errorText` is no longer used by the app.
* **ui,query:** failure surfaces — toast, banner, provider, client callback
* **sdk:** the SDK-private code constants CSRF_INVALID_CODE, NETWORK_ERROR_CODE, and NETWORK_TIMEOUT_CODE are deleted (pinned deleted by src/index.test.ts); their wire values are now Bff.CsrfInvalid, Transport.NetworkError, and Transport.Timeout from api-errors, and the proxy's forward timeout answers 504 instead of 503. The bodiless 401s from the proxy and /bff/user now carry a problem body.
* **sdk:** throw ApiFailure from the SDK and retarget consumers

### Features

* **api-errors:** publish the dependency-free failure package ([1781636](https://github.com/bc-solutions-coder/wallow/commit/17816366f1343cb410ee849a34dc6dc2e564ef4f)), closes [#179](https://github.com/bc-solutions-coder/wallow/issues/179)
* **sdk:** originate BFF proxy and passthrough failures as problems ([732b573](https://github.com/bc-solutions-coder/wallow/commit/732b573db2ee829b31c08edb441606a035002a84)), closes [#181](https://github.com/bc-solutions-coder/wallow/issues/181)
* **sdk:** throw ApiFailure from the SDK and retarget consumers ([5b04bc3](https://github.com/bc-solutions-coder/wallow/commit/5b04bc3e63bcddb085ac348fd5e94a364f3db011)), closes [#180](https://github.com/bc-solutions-coder/wallow/issues/180)
* **ui,query:** failure surfaces — toast, banner, provider, client callback ([3d3533a](https://github.com/bc-solutions-coder/wallow/commit/3d3533a0d1714f9b16fba9a23c5e0206af567419)), closes [#182](https://github.com/bc-solutions-coder/wallow/issues/182)
* **wallow-auth:** auth/mfa endpoints answer problems, one registry ([9479110](https://github.com/bc-solutions-coder/wallow/commit/9479110342e9cc94e19df25ac7ca8a8b9181c360))
* **wallow-web:** migrate read and mutation sites onto the failure surfaces ([08e0abc](https://github.com/bc-solutions-coder/wallow/commit/08e0abc2a9095f1f5e97a56c58f63c5fe71139c0))


### Bug Fixes

* **api:** give missing tenant scope a dedicated code ([cc50b90](https://github.com/bc-solutions-coder/wallow/commit/cc50b90644009c294d4dc9d47e46e969586cdc38)), closes [#188](https://github.com/bc-solutions-coder/wallow/issues/188)
* **forms:** surface unmatched validation messages in the banner ([ded89cb](https://github.com/bc-solutions-coder/wallow/commit/ded89cb38ca33c049d332305e448e416ffb541be)), closes [#201](https://github.com/bc-solutions-coder/wallow/issues/201)


### Code Refactoring

* **forms:** retire legacy failure helpers ([4100cd0](https://github.com/bc-solutions-coder/wallow/commit/4100cd071fcebd832c2785b8ab4d12a40fef9b37)), closes [#187](https://github.com/bc-solutions-coder/wallow/issues/187)
