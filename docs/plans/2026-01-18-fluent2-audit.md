# BusLane Fluent 2 Design Audit

**Date:** 2026-01-18
**Scope:** Full application UI/UX audit against Microsoft Fluent 2 design principles

---

## Executive Summary

BusLane has a solid foundation aligned with Fluent 2 design principles. The application uses semantic color tokens, a proper typography ramp, light/dark theme support, and consistent interactive states. However, there are gaps in spacing consistency, icon usage, accessibility, and component standardization that should be addressed to achieve full Fluent 2 compliance.

### What's Working Well

- **Color Token System** - `App.axaml` uses semantic tokens (`AccentBrand`, `SurfaceSubtle`, `TextDanger`, etc.) that map well to Fluent 2's token architecture
- **Typography Ramp** - Proper type hierarchy (display → title → heading → body → caption) with correct sizes and weights
- **Light/Dark Theme Support** - Both themes use appropriate color adjustments for contrast
- **4px Base Unit** - Spacing values (8, 12, 16, 24px) generally follow Fluent's 4px grid
- **Consistent Corner Radius** - 4px for buttons, 6-8px for cards
- **Interactive States** - Hover, pressed, and selected states implemented with subtle background changes
- **Elevation System** - Cards use BoxShadow for depth with hover transitions

---

## Gap Analysis

### 1. Spacing & Layout Issues

**Fluent 2 Principle:** Fluent 2 uses a strict 4px base unit for all spacing. Standard ramp: 4, 8, 12, 16, 20, 24, 32, 40, 48px.

#### Findings

| Location | Issue |
|----------|-------|
| `MainWindow.axaml:54` | `Margin="24,20"` - 20 is not on the standard ramp |
| `NavigationSidebar.axaml:17` | `Padding="16,16,16,12"` - asymmetric padding breaks rhythm |
| `SendMessageDialog.axaml:19` | `Padding="24,20"` - same issue |
| Sidebar sections | Varying margins: `12,16,12,12`, `12,12,12,0`, `12,0,12,4` |
| Tab bar vs content | Misaligned padding between tab row and content below |

#### Recommendations

- Standardize all spacing to the 4px ramp (prefer 8, 16, 24 for most cases)
- Use consistent section margins throughout the sidebar (`Margin="12,16"` uniformly)
- Align tab bar padding with content area margins

---

### 2. Typography Issues

**Fluent 2 Principle:** Specific type styles with exact size/weight/line-height combinations. Mixing ad-hoc font sizes breaks visual hierarchy.

#### Defined Type Ramp

| Style | Size | Weight |
|-------|------|--------|
| display | 40px | SemiBold |
| title | 24px | SemiBold |
| heading | 16px | SemiBold |
| body | 14px | Regular |
| subtitle | 13px | Regular |
| caption | 12px | Regular |

#### Findings

| Location | Issue |
|----------|-------|
| `NavigationSidebar.axaml:62` | `FontSize="18"` - not in ramp |
| `MainWindow.axaml:305` | `FontSize="20"` - not in ramp |
| `SendMessageDialog.axaml:219` | `FontSize="14"` inline instead of `Classes="body"` |
| `MessagesPanelView.axaml:224-231` | Inline `FontSize="12"`, `FontWeight="SemiBold"` instead of class |
| Multiple locations | Inline font sizes missing `LineHeight` |

#### Recommendations

- Add `title-small` (18px) and `title-large` (20px) to the type ramp if needed
- Replace inline font styling with class references wherever possible
- Audit all `FontSize=` usages and map to the nearest type class

---

### 3. Button & Interactive Element Issues

**Fluent 2 Principle:** Consistent interactive patterns with predictable sizing, clear visual hierarchy (primary → secondary → subtle), and consistent icon placement. Minimum touch targets of 32x32px.

#### Findings

**Mixed Icon Approaches:**

| Pattern | Example | Issue |
|---------|---------|-------|
| Lucide icons | `<LucideIcon Kind="Send" Size="14"/>` | Consistent, preferred |
| Emoji text | `<TextBlock Text="✉" FontSize="18"/>` | Renders differently per platform |
| Unicode symbols | `Content="↻ Refresh"` | Inconsistent with icon buttons |

Locations with emoji: `SendMessageDialog.axaml:31,57-59,91-92,159,302`

**Button Content Pattern Inconsistency:**

| Location | Pattern |
|----------|---------|
| `MessagesPanelView.axaml:24` | `Content="+ Send"` (text with symbol) |
| Various | `<StackPanel><LucideIcon/><TextBlock/></StackPanel>` (icon + text) |
| `MessagesPanelView.axaml:53` | `Content="↻ Refresh"` (Unicode symbol) |

**Close Button Inconsistency:**

| Location | Implementation |
|----------|----------------|
| `MainWindow.axaml:136` | `Content="✕ Close"` (text) |
| `MainWindow.axaml:313` | `<LucideIcon Kind="X" Size="16"/>` (icon only) |
| `SendMessageDialog.axaml:129` | `<LucideIcon Kind="X" Size="14"/>` (different size) |

**Icon Size Variance:** 10, 11, 12, 14, 16, 18, 24px used across the app.

#### Recommendations

- Replace all emoji icons with Lucide equivalents:
  - `✉` → `Mail`
  - `📥` → `Download`
  - `📂` → `Folder`
  - `⏱` → `Clock`
  - `🏷` → `Tag`
  - `💡` → `Lightbulb`
  - `🗑` → `Trash2`
- Standardize icon sizes: 12px (compact/inline), 16px (standard buttons), 20px (prominent)
- Create consistent button content pattern using StackPanel with icon + text

---

### 4. Focus States & Accessibility

**Fluent 2 Principle:** Every interactive element needs visible focus indicators for keyboard navigation. Focus rings should be 2px with contrasting color. Critical for users who can't use a mouse.

#### Findings

**Missing Focus Styles:**

| Control | Has `:pointerover` | Has `:focus` |
|---------|-------------------|--------------|
| `Button.subtle` | Yes | No |
| `Button.icon` | Yes | No |
| `Button.toolbar` | Yes | No |
| `ListBoxItem` | Yes | No |
| `ToggleButton.pill` | Yes | No |
| `Border.entity-item` | Yes | No |
| `TextBox` | Yes | Yes (only control with focus style) |

**Contrast Concerns:**

| Token | Light | Dark | Issue |
|-------|-------|------|-------|
| `MutedForeground` | `#8A8A8A` | `#8A8A8A` | ~4.1:1 ratio on dark - fails WCAG AA for small text |

**Touch Target Issues:**

| Element | Size | Issue |
|---------|------|-------|
| Sidebar chevron toggle | 20px width | Below 44px recommended touch target |
| `Button.toolbar` | `MinWidth="0"` | Could become too small |

#### Recommendations

- Add `:focus-visible` styles with 2px accent-colored ring to all button variants
- Increase `MutedForeground` in dark theme to `#A0A0A0` for better contrast
- Set minimum touch targets of 32x32px (desktop) or 44x44px (touch-friendly)
- Example focus style to add:

```xml
<Style Selector="Button.subtle:focus-visible /template/ ContentPresenter">
    <Setter Property="BoxShadow" Value="0 0 0 2 {DynamicResource AccentBrand}"/>
</Style>
```

---

### 5. Cards, Surfaces & Elevation

**Fluent 2 Principle:** Layered elevation system where surfaces at different depths have different shadow intensities. Higher elevation = more important/interactive. Shadows should be subtle and consistent.

#### Findings

**Inconsistent Shadow Values:**

| Component | Shadow Value |
|-----------|--------------|
| `Border.card` | `0 2 4 0 #18000000` |
| `Border.card-elevated` | `0 4 12 0 #24000000` |
| `Border.modal` | `0 8 24 0 #32000000` |
| Loading overlay card | `0 12 48 0 #50000000` |
| Alerts dialog | `0 8 32 0 #40000000` |

These don't follow a consistent progression.

**Surface Token Redundancy:**

| Token | Light Value | Issue |
|-------|-------------|-------|
| `SurfaceSubtle` | `#F9F9F9` | Nearly identical to SidebarBackground |
| `SidebarBackground` | `#FAFAFA` | Could be consolidated |
| `CardBackground` | `#FFFFFF` | Same as InputBackground |
| `InputBackground` | `#FFFFFF` | Redundant token |

**Other Issues:**

- Some inline cards omit borders while `Border.card` has `BorderThickness="1"`
- `NavigationSidebar.axaml:344` manually sets border properties instead of using `.card` class
- `SendMessageDialog` nests `.surface-subtle` inside `.send-message-panel` (nested elevation confusion)

#### Recommendations

- Define 4 elevation levels with consistent shadow tokens:

| Level | Use Case | Shadow |
|-------|----------|--------|
| Rest | Cards, surfaces | `0 2 4 0 #10000000` |
| Hover | Interactive cards | `0 4 8 0 #18000000` |
| Dropdown | Flyouts, menus | `0 8 16 0 #24000000` |
| Modal | Dialogs, overlays | `0 16 32 0 #32000000` |

- Consolidate redundant surface tokens
- Avoid nesting elevated surfaces - use flat sections within cards instead

---

### 6. Modal & Dialog Patterns

**Fluent 2 Principle:** Dialogs follow specific patterns: consistent header/content/footer structure, predictable button placement (primary action on right), proper focus trapping. Overlay opacity and dialog sizing should be standardized.

#### Findings

**Inconsistent Modal Overlay Opacity:**

| Token | Light | Dark |
|-------|-------|------|
| `ModalOverlay` | `#66000000` (40%) | `#B3000000` (70%) |
| `LoadingOverlay` | `#99000000` (60%) | `#CC000000` (80%) |

**Dialog Size Inconsistency:**

| Dialog | Size |
|--------|------|
| `Border.modal` | `MaxWidth="640"`, `MaxHeight="560"` |
| `SendMessageDialog` | `Width="800"`, `MaxHeight="750"` |
| Alerts dialog | `MaxWidth="900"`, `MaxHeight="700"` |
| Namespace panel | `Width="420"` fixed |

**Other Issues:**

- Header/footer pattern varies between dialogs
- Close button position inconsistent (top-right icon vs inline text)
- Modal overlays don't trap keyboard focus
- Escape key handling may be inconsistent

#### Recommendations

- Standardize overlay to 50% opacity (`#80000000`) for both themes
- Define dialog size tokens:

| Size | Max Width | Use Case |
|------|-----------|----------|
| Small | 400px | Confirmations, simple inputs |
| Medium | 640px | Standard forms, settings |
| Large | 900px | Complex forms, data tables |

- Create shared dialog template with header/content/footer structure
- Implement focus trapping for all modal dialogs

---

## Prioritized Action Items

### High Priority (High Impact, Low-Medium Effort)

| # | Issue | Action | Files |
|---|-------|--------|-------|
| 1 | Mixed emoji/Lucide icons | Replace all emoji with Lucide equivalents | `SendMessageDialog.axaml` |
| 2 | Missing focus states | Add `:focus-visible` styles to all button variants | `AppStyles.axaml` |
| 3 | Dark mode contrast | Increase `MutedForeground` to `#A0A0A0` | `App.axaml` |
| 4 | Inline font sizes | Replace with type classes, add missing ramp sizes | Multiple views |

### Medium Priority (Medium Impact, Medium Effort)

| # | Issue | Action | Files |
|---|-------|--------|-------|
| 5 | Spacing inconsistency | Normalize all padding/margins to 4px grid | All views |
| 6 | Button content patterns | Standardize icon+text StackPanel pattern | Multiple views |
| 7 | Icon size variance | Reduce to 3 sizes: 12px, 16px, 20px | All views |
| 8 | Dialog sizes | Define small/medium/large size tokens | `AppStyles.axaml`, dialogs |

### Lower Priority (Polish, Higher Effort)

| # | Issue | Action | Files |
|---|-------|--------|-------|
| 9 | Shadow elevation system | Define 4 consistent elevation levels | `AppStyles.axaml` |
| 10 | Surface token cleanup | Consolidate redundant tokens | `App.axaml` |
| 11 | Modal overlay consistency | Standardize opacity across themes | `App.axaml` |
| 12 | Focus trapping | Implement for all modal dialogs | Dialog views |

---

## References

- [Fluent 2 Design System](https://fluent2.microsoft.design/)
- [Fluent 2 Design Principles](https://fluent2.microsoft.design/design-principles)
- [Fluent 2 Layout](https://fluent2.microsoft.design/layout)
