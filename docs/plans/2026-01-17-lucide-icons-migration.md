# Lucide Icons Migration Design

## Overview

Replace current Fluent-style StreamGeometry icons with Lucide icons using the Lucide.Avalonia NuGet package for a visual refresh.

## Goals

- Modernize the icon aesthetic with Lucide's clean, consistent stroke-based style
- Simplify icon management via NuGet package with automatic updates
- Maintain theme compatibility (dark/light mode)

## Non-Goals

- Replacing the custom bus logo (brand identity)
- Changing icon behavior or interactions
- Adding new icons beyond current set

## Technical Approach

### Package

**Lucide.Avalonia** (dme-compunet)
- Version: 0.1.38
- Actively maintained with daily auto-updates
- No xmlns declaration required
- MIT licensed

Install:
```bash
dotnet add package Lucide.Avalonia
```

### Syntax Change

**Before** (current):
```xml
<PathIcon Data="{StaticResource IconRefresh}" Width="16" Height="16"
          Foreground="{DynamicResource SubtleForeground}"/>
```

**After** (Lucide):
```xml
<LucideIcon Kind="RefreshCw" Size="16"
            Foreground="{DynamicResource SubtleForeground}"/>
```

Key differences:
- `Kind` enum instead of `StaticResource` reference
- `Size` property (single value) instead of Width/Height
- `StrokeWidth` property defaults to 1.5 (can omit)
- `Foreground` works the same way for theming

## Icon Mapping

### Navigation & UI Controls
| Current | Lucide |
|---------|--------|
| IconChevronDown | `ChevronDown` |
| IconChevronLeft | `ChevronLeft` |
| IconChevronRight | `ChevronRight` |
| IconClose | `X` |
| IconSidebar | `PanelLeft` |
| IconLibrary | `Library` |

### Authentication & Connections
| Current | Lucide |
|---------|--------|
| IconCloud | `Cloud` |
| IconSignOut | `LogOut` |
| IconConnection | `Cable` |
| IconTestConnection | `PlugZap` |
| IconAzureSubscription | `CreditCard` |

### Service Bus Entities
| Current | Lucide |
|---------|--------|
| IconNamespace | `Server` |
| IconQueue | `ListOrdered` |
| IconTopic | `Megaphone` |
| IconSubscription | `Inbox` |
| IconLocation | `MapPin` |

### Actions & Operations
| Current | Lucide |
|---------|--------|
| IconPlus | `Plus` |
| IconDelete | `Trash2` |
| IconEdit | `Pencil` |
| IconRefresh | `RefreshCw` |
| IconSend | `Send` |
| IconSave | `Save` |
| IconLoad | `Download` |
| IconCopy | `Copy` |
| IconExport | `Upload` |
| IconSwap | `ArrowLeftRight` |
| IconCheck | `Check` |
| IconCheckCircle | `CheckCircle` |

### Features & Monitoring
| Current | Lucide |
|---------|--------|
| IconStream | `Radio` |
| IconChart | `BarChart3` |
| IconBell | `Bell` |
| IconMessage | `MessageSquare` |
| IconBroadcast | `Rss` |
| IconSearch | `Search` |
| IconKeyboard | `Keyboard` |

### Status & Information
| Current | Lucide |
|---------|--------|
| IconWarning | `AlertTriangle` |
| IconInfo | `Info` |
| IconPlay | `Play` |
| IconPause | `Pause` |
| IconStop | `Square` |
| IconStarFilled | `Star` (with Fill) |
| IconStarOutline | `Star` |

## File Changes

### Files to Delete
- `BusLane/Styles/Icons.axaml`

### Files to Modify

**`BusLane/App.axaml`**
- Remove: `<ResourceInclude Source="/Styles/Icons.axaml"/>`

**`BusLane/BusLane.csproj`**
- Add: `<PackageReference Include="Lucide.Avalonia" Version="0.1.38" />`

**Views to Update**
- `Views/MainWindow.axaml`
- `Views/NavigationSidebar.axaml`
- `Views/MainWindow.axaml`
- `Views/NavigationSidebar.axaml`
- `Views/Controls/MessagesPanelView.axaml`
- `Views/Dialogs/MessageDetailDialog.axaml`
- `Views/Dialogs/ConnectionLibraryDialog.axaml`
- `Views/Dialogs/SendMessageDialog.axaml`
- `Views/Dialogs/SettingsDialog.axaml`
- `Views/Dialogs/AlertsDialog.axaml`
- `Views/Controls/LiveStreamView.axaml`
- `Views/Controls/ChartsView.axaml`

### Unchanged
- Custom bus logo in `NavigationSidebar.axaml` (inline Canvas)
- All ViewModel files
- Theme/color resources

## Edge Cases

### Filled vs Outline Stars
- `IconStarOutline` → `<LucideIcon Kind="Star"/>`
- `IconStarFilled` → `<LucideIcon Kind="Star" Fill="CurrentColor"/>`

### Size Variations
Current Width/Height becomes single Size:
- `Width="12" Height="12"` → `Size="12"`
- `Width="16" Height="16"` → `Size="16"`
- `Width="24" Height="24"` → `Size="24"`

### Color Inheritance
Lucide icons inherit `Foreground` from parent by default. Explicit colors work as before:
```xml
<LucideIcon Kind="Check" Foreground="{DynamicResource TextSuccess}"/>
```

## Implementation Steps

1. Add NuGet package
2. Update views (replace PathIcon with LucideIcon)
3. Delete `Icons.axaml` and remove reference from `App.axaml`
4. Build and verify
5. Visual QA on all screens

## Testing Checklist

- [ ] All toolbar buttons show correct icons
- [ ] Navigation sidebar icons render properly
- [ ] Connection library dialog icons work
- [ ] Message list/detail icons display
- [ ] Alert/Chart/LiveStream panel icons visible
- [ ] Dark mode theming works correctly
- [ ] Bus logo unchanged in sidebar header
