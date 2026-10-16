# Send Message dialog verification

- Branch: `codex/focused-workspace`; earlier UI changes retained.
- Application and synthetic native preview compiled successfully.
- Focused Send Message/editor regression tests: 18 passed.
- Final full suite: 906 passed, 1 skipped, 4 existing website failures, 911 total.
- Existing failing GitHubPagesLandingPageTests: `LandingPage_ScreenshotGallery_UsesAccessibleLocalTabs`, `LandingPage_ApprovedHero_UsesProductLedMessage`, `LandingPage_DisplayPreferences_ProvideDarkAndReducedMotionModes`, `LandingPage_VisibleCopy_ContainsNoBannedDashCharacters`. Website source and tests are unchanged by this task.
- Native matrix: 32 synthetic captures in light/dark at 1280×800 and 1000×600. Normal Compose, Properties, Delivery, Custom, active template at both sizes; error, sending, empty Custom/body, populated/no-match template popup at minimum size.
- Native assertions verify Send bounds and busy state, normal editor height of at least180px, six template-action bindings, and correct populated/no-match empty-label visibility. The contradictory empty label was reproduced with a failing native assertion before its ItemCount binding correction.
- Initial independent review identified two P2 issues: template empty visibility and light body-placeholder contrast. Final verdict inspected seven refreshed captures and source, resolved both findings, and closed dark Properties coverage; disposition ship at reviewed UI scope.
- No UI edits followed final captures. No HTML/CSS detector applies to native Avalonia.
- No real Azure sends, import/persistence results, assistive-technology output, or Windows/Linux runtime verification was performed.
