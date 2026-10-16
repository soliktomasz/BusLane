# Focused workspace implementation plan

**Goal:** Implement the approved light concept's workspace arrangement in Avalonia with both themes.

**Architecture:** Reuse current message selection, commands, formatting, and virtualization. Add a side inspector and retain the full details dialog as an explicit expand action. Keep transport, settlement, authentication, and persistence behavior intact.

**Reference:** `.impeccable/mocks/decision/light.html`; user approved continuing from the recommended layout.

## Work

- [x] Add explicit expanded-details state and selection-reset coverage in `MessageOperationsViewModel` and its existing tests.
- [x] Add `MessageInspectorView`, bind it beside `MessagesPanelView`, and make the existing details dialog opt-in.
- [x] Replace message cards with aligned, virtualized subject/ID/time rows; retain bulk, session, locked, loading, empty, search, and dead-letter paths.
- [x] Compact navigation, namespace tabs, entity headers, and desktop theme surfaces.
- [x] Build and run tests, inspect both themes at desktop and minimum widths, and obtain a fresh review.
- [x] Record the finished desktop system in `docs/desktop-design.md` without replacing the website system.

## Review focus

- A selected dead letter must retain reason and diagnostics, plus all metadata through expanded details.
- Entity, subscription, page, and connection changes must not leave a stale selection in the inspector.
- Session and locked-message operations must retain their controls and semantics.
- At minimum size, inspector and navigation must yield space rather than clip message actions.
- Theme changes must update all selection and syntax colors through dynamic resources.

## Outcome

Build succeeds with zero warnings/errors. Full suite: 903 passed, 1 skipped, 4 pre-existing website checks failed. Native visual review: ship, across both themes, standard/minimum widths, expanded navigation, and bulk mode. A failing row-inspection regression was fixed with an explicit full-details action available when the inline inspector is hidden. Native layout assertions verified useful list height in minimum-size expanded and bulk states.

Verification details: `.impeccable/review/implemented/verification.md`. Real Azure operations were not exercised.
