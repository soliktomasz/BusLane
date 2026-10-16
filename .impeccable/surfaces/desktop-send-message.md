---
version: 1
slug: "desktop-send-message"
primary_target: "BusLane/Views/Dialogs/SendMessageDialog.axaml"
---

# Desktop Send Message composer

## Intent and authority

Mode: Operate. The user confirmed the existing Compose, Properties, and Custom tabbed composer. This bounded redesign extends the incumbent [desktop design system](../../docs/desktop-design.md): native Avalonia/Fluent controls, Inter, Lucide icons, violet feedback, and dynamic light/dark resources. Root PRODUCT.md and DESIGN.md describe the website; they do not govern this dialog. No identity replacement, global token change, or website sidecar repair is introduced.

The approved brief keeps the destination and sending actions visible while giving the body editor the available space. Metadata stays in clearly named groups, saved templates remain native controls, and the layout uses thin seams and flat form groups rather than nested cards. The artifact is `BusLane/Views/Dialogs/SendMessageDialog.axaml`; view-model and Service Bus behavior are outside this change.

## Implemented composition

- The modal stretches within the available desktop viewport, capped at (960 × 760px), with minimum width (720px) and an outer margin (24px). A fixed header shows Send Message (20px, semibold), the destination with trimming and a full-value tooltip, and an accessible Close icon button (34 × 34px).
- Compose, Properties, and Custom use the existing native radio-tab styling. The adjacent compact toolbar retains Import, Templates, Save template, conditional Update, and Clear. The tabs and toolbar stay above the independently sized content area.
- Errors wrap in the existing themed danger treatment. Their native scroll area is capped at (72px), keeping the footer available when diagnostics are long.
- Compose shows Message Body, content type, and the active template name when present. The template name trims at a maximum width (240px), with a full-value tooltip. Template values scroll natively within a cap (104px).
- The native multiline body editor fills the remaining body height with minimum height (64px), wraps text, and owns its scrolling. It uses the explicit Cascadia Code, Consolas, Monaco, monospace family stack and the existing payload size (13px). Text and placeholder use the dynamic CodeForeground resource. There is no fixed-height editor wrapped in another scroll container.
- Properties scrolls independently and exposes all eleven existing fields in flat Identity, Routing & sessions, and Delivery groups. Identity contains Content Type, Message ID, Subject (Label), and Correlation ID. Routing & sessions contains Session ID, Partition Key, To, Reply To, and Reply To Session ID. Delivery contains Time to Live and Scheduled Enqueue Time. The two columns and form rows use gaps (20px); group titles use (16px, semibold). Existing format helpers remain beside the relevant delivery fields.
- Custom properties scrolls independently. Add Property remains beside the heading/count, above aligned native Key and Value fields with an accessible Remove action in each row. The empty state reads No custom properties and points to Add Property.
- The persistent footer shows Message body is required, Cancel, and Send Message. The primary action is disabled while IsSending is true and reads Sending... during that state. The unsupported Ctrl+Enter hint is removed; no keyboard binding is added.
- Templates uses a native Flyout with width (440px), maximum height (400px), and a scrolling result area capped at (280px). Search precedes flat rows with name (13px, semibold), category/date metadata (12px), and Use, Duplicate, and Delete. The empty helper binds directly to `SavedTemplatesList.ItemCount`, so it does not appear beside populated results.

## States and preservation

Preserve Compose, Properties, and Custom selection, body editing, destination display, content type, all eleven metadata bindings, token-value editing, custom-property add/remove, import, save/update/clear, template search/load/duplicate/delete, Send, Cancel, and Close. Native controls retain their keyboard behavior, and icon actions and form fields expose labels or accessible names. Sending and error states follow the existing view-model properties and commands.

Keep body-empty, custom-empty, populated and no-match template results, long errors, active templates, and the Sending state legible in both themes. The template empty label reflects the visible result count rather than an independently recomputed collection count. This is a layout and presentation change; it does not introduce a send shortcut or alter message serialization, sending, scheduling, import, or template persistence behavior.

## Verification and limits

Source evidence: `BusLane/Views/Dialogs/SendMessageDialog.axaml`, shared dialog/control styles in `BusLane/Styles/AppStyles.axaml`, theme resources in `BusLane/App.axaml`, and existing bindings in `BusLane/ViewModels/SendMessageViewModel.cs`.

The [native capture matrix](../review/send-message/) covers 32 synthetic Avalonia views: Compose, Properties, Delivery, Custom, and active-template composition at window sizes (1280 × 800px) and (1000 × 600px), in both themes; plus error, Sending, empty Custom, empty body, populated Templates, and no-match Templates at (1000 × 600px), in both themes. Template flyout captures show standalone native popup roots. The dark backdrop around other captures is the modal's transparent backdrop rather than an application background sample.

The initial independent review identified a P2 contradictory template-empty helper and a P2 light-theme code-placeholder contrast issue. The implementation binds empty visibility to the native result control's ItemCount and uses CodeForeground for the body placeholder. The final independent review inspected seven refreshed captures and source, marked both findings resolved, closed the dark-theme Properties capture gap, and returned a ship disposition at the reviewed UI scope. Properties is captured at its starting position and Delivery at the end of its scroll area. No UI edits followed the final captures.

The final native harness passed its layout and state assertions: Send remains visible with the correct busy state, normal composition retains at least (180px) of editor height at both reviewed sizes, six populated-template row actions remain bound, and the empty helper is hidden with two templates and visible with no search matches. These operations are binding/state checks using a mocked service; they do not send messages or write user templates.

All 18 focused Send Message/editor tests pass. The full suite reports 906 passed, one skipped, and four pre-existing website failures (911 total). The failures are in GitHubPagesLandingPageTests: LandingPage_ScreenshotGallery_UsesAccessibleLocalTabs, LandingPage_ApprovedHero_UsesProductLedMessage, LandingPage_DisplayPreferences_ProvideDarkAndReducedMotionModes, and LandingPage_VisibleCopy_ContainsNoBannedDashCharacters. These review, harness, and test results were supplied by the implementation workflow, rather than inferred from the images.

The captures establish native rendering with synthetic data. They do not establish real Azure sends, file import results, template persistence, assistive-technology output, or Windows/Linux rendering. This surface uses Avalonia rather than HTML/CSS, so web layout detectors do not apply. Earlier Connections, Activity Log, entity details, and workspace changes remain outside this surface's review scope.
