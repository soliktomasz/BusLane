# Native workspace verification

- Build: succeeded, 0 warnings, 0 errors.
- Full suite: 903 passed, 1 skipped, 4 failed out of 908.
- Native visual review: ship; no required fixes remained.
- Code review's row-inspection finding was reproduced with two failing parameterized cases, then fixed. Both cases pass in the full suite.
- Native renders: light/dark at 1440px and 1000px; minimum-size expanded navigation and bulk selection. The current macOS display constrains the large client to 1440×848; minimum captures are 1000×600.
- Minimum-size expanded navigation list height: 279px. Bulk mode: 207.5px.
- Screenshot provenance: rendered from the compiled Avalonia window with a temporary harness in `/private/tmp/BusLaneRedesignPreview`. All displayed namespace and message data is synthetic; services are mocked. These images establish layout evidence, not live Azure behavior.

## Existing website failures

These tests and `docs/index.html` are unchanged from HEAD; none is part of the native workspace implementation:

- `GitHubPagesLandingPageTests.LandingPage_ScreenshotGallery_UsesAccessibleLocalTabs`: expects three tabs; current website uses a different selector structure.
- `GitHubPagesLandingPageTests.LandingPage_ApprovedHero_UsesProductLedMessage`: expects the former hero copy.
- `GitHubPagesLandingPageTests.LandingPage_DisplayPreferences_ProvideDarkAndReducedMotionModes`: current website lacks the expected dark-mode rule.
- `GitHubPagesLandingPageTests.LandingPage_VisibleCopy_ContainsNoBannedDashCharacters`: current website title contains an em dash.

No real Azure connectivity, send, settlement, authentication, or cross-platform runtime verification was performed.
