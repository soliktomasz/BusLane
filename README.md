# BusLane

A modern, cross-platform Azure Service Bus management tool built with Avalonia UI and .NET 10.

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)
![Avalonia UI](https://img.shields.io/badge/Avalonia-11.3-8B44AC?style=flat)
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Version](https://img.shields.io/badge/Version-0.8.2-blue.svg)
![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Windows%20%7C%20Linux-lightgrey.svg)
[![BuyMeACoffee](https://img.shields.io/badge/Buy%20Me%20A%20Coffee-tomaszsolik-FFDD00?style=flat&logo=buy-me-a-coffee&logoColor=000000)](https://www.buymeacoffee.com/tomaszsolik)

<p align="center">
  <a href="https://soliktomasz.github.io/BusLane/">Website</a> •
  <a href="#installation">Installation</a> •
  <a href="#features">Features</a> •
  <a href="ROADMAP.md">Roadmap</a> •
  <a href="#contributing">Contributing</a>
</p>

---

## Features

### Connection Options
- **Azure Authentication** - Sign in with your Azure account using Azure Identity
- **Connection String Support** - Connect directly using Service Bus connection strings
- **Connection Library** - Save and manage multiple connection strings for quick access

### Namespace & Entity Management
- **Subscription Management** - Browse and switch between Azure subscriptions
- **Namespace Explorer** - View all Service Bus namespaces in your subscription
- **Namespace Selection Panel** - Slide-in panel for easy namespace browsing and selection
- **Namespace Search** - Filter namespaces by name, location, or resource group
- **Queue Management** - Browse queues, view message counts, and manage messages
- **Topic & Subscription Support** - Full support for topics and their subscriptions
- **Session-Enabled Queues** - Support for session-enabled queues and subscriptions

### Messaging Features
- **Message Peek** - Preview messages without consuming them
- **Message Search & Filter** - Search messages by content, ID, correlation ID, subject, or sequence number
- **Send Messages** - Send new messages with full control over:
  - Message body and content type
  - Custom properties (key-value pairs)
  - System properties (CorrelationId, SessionId, Subject, etc.)
  - Message scheduling (ScheduledEnqueueTime)
  - Time-to-live (TTL) settings
  - Partition keys and reply-to settings
- **Save & Load Messages** - Save message templates for reuse
- **Dead Letter Queue** - View and manage dead-lettered messages
- **Resend from DLQ** - Resend dead-letter messages back to the original queue
- **Purge Messages** - Bulk delete messages from queues or subscriptions
- **Message Details** - View complete message details including headers and properties

### Live Monitoring
- **Live Message Streaming** - Real-time message stream viewer with peek mode
- **Live Charts** - Visual metrics with line charts, pie charts, and bar charts for:
  - Message counts over time
  - Dead letter counts over time
  - Entity distribution
  - Queue/Subscription comparison
- **Configurable Time Ranges** - View metrics for 15 minutes, 1 hour, 6 hours, or 24 hours
- **Alert System** - Create custom alert rules with configurable thresholds and severity levels
- **System Notifications** - Get desktop notifications when alerts are triggered

### User Experience
- **Session Persistence** - Automatically restores your previous session
- **Settings Dialog** - Configure application preferences
- **Modern UI** - Clean, intuitive Fluent design interface

## Screenshots

*Coming soon*

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- An Azure account with access to Azure Service Bus resources
- Required Azure RBAC roles:
  - `Azure Service Bus Data Receiver` - to peek/receive messages
  - `Azure Service Bus Data Sender` - to send messages
  - `Reader` - to browse namespaces, queues, and topics

## Installation

### Download Pre-built Releases

Download the latest release from the [Releases](https://github.com/soliktomasz/BusLane/releases) page.

#### macOS Installation Note

Since the app is not signed with an Apple Developer certificate, macOS Gatekeeper may show a warning that the app "is damaged and can't be opened." To fix this:

1. Open Terminal
2. Run the following command (adjust the path if needed):
   ```bash
   xattr -cr "/Applications/Bus Lane.app"
   ```
3. Try opening the app again

Alternatively, you can right-click the app and select "Open" to bypass the warning.

### Build from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/BusLane.git
   cd BusLane
   ```

2. Build the application:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

### Publish as Self-Contained

```bash
# For macOS (Intel)
dotnet publish -c Release -r osx-x64 --self-contained

# For macOS (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained

# For Windows
dotnet publish -c Release -r win-x64 --self-contained

# For Linux
dotnet publish -c Release -r linux-x64 --self-contained
```

## Usage

### Azure Account Mode
1. **Sign In** - Click "Sign in with Azure" to authenticate with your Azure account
2. **Select Subscription** - Choose the Azure subscription containing your Service Bus namespaces
3. **Browse Namespaces** - Click on a namespace to view its queues and topics
4. **View Messages** - Select a queue or topic subscription to peek at messages
5. **Toggle Dead Letter** - Use the dead letter toggle to view dead-lettered messages
6. **Send Messages** - Click the send button to compose and send new messages

### Connection String Mode
1. **Open Connection Library** - Access saved connections or add new ones
2. **Add Connection** - Paste your Service Bus connection string and give it a name
3. **Connect** - Select a saved connection to browse queues and topics
4. **Manage Messages** - View, send, and manage messages just like in Azure mode

## Architecture

BusLane follows the MVVM (Model-View-ViewModel) pattern:

```
BusLane/
├── Models/          # Data models
│   ├── AlertRule.cs              # Alert rule configuration
│   ├── LiveStreamMessage.cs      # Live stream message model
│   ├── QueueInfo.cs              # Queue metadata
│   ├── TopicInfo.cs              # Topic metadata
│   ├── SubscriptionInfo.cs       # Subscription metadata
│   ├── MessageInfo.cs            # Message details
│   ├── SavedConnection.cs        # Stored connection strings
│   └── SavedMessage.cs           # Message templates
├── Services/        # Azure integration services
│   ├── Abstractions/             # Service interfaces
│   ├── Auth/                     # Azure authentication
│   ├── Infrastructure/           # Core infrastructure services
│   ├── Monitoring/               # Metrics, alerts, and notifications
│   │   ├── IAlertService.cs      # Alert management interface
│   │   ├── AlertService.cs       # Alert rule and event handling
│   │   ├── IMetricsService.cs    # Metrics collection interface
│   │   ├── MetricsService.cs     # Metrics recording and history
│   │   ├── INotificationService.cs   # Desktop notifications interface
│   │   └── NotificationService.cs    # System notification handling
│   ├── ServiceBus/               # Service Bus operations
│   │   ├── IServiceBusService.cs     # Service Bus operations interface
│   │   ├── ServiceBusService.cs      # Service Bus implementation
│   │   ├── ILiveStreamService.cs     # Live streaming interface
│   │   └── LiveStreamService.cs      # Real-time message streaming
│   └── Storage/                  # Local storage services
├── ViewModels/      # MVVM ViewModels with CommunityToolkit.Mvvm
│   ├── AlertsViewModel.cs        # Alert management
│   ├── ChartsViewModel.cs        # Live charts and metrics
│   ├── LiveStreamViewModel.cs    # Message streaming
│   └── ...                       # Other view models
├── Views/           # Avalonia XAML views
│   ├── Controls/    # Reusable UI components
│   │   ├── ChartsView.axaml      # Live charts component
│   │   ├── LiveStreamView.axaml  # Message streaming component
│   │   └── ...                   # Other controls
│   └── Dialogs/     # Modal dialogs (Send, Save, Settings, Alerts, etc.)
├── Converters/      # Value converters for data binding
└── Styles/          # Application styles and themes
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.3.10 | Cross-platform UI framework |
| Avalonia.Desktop | 11.3.10 | Desktop platform support |
| Avalonia.Themes.Fluent | 11.3.10 | Fluent design theme |
| Avalonia.Fonts.Inter | 11.3.10 | Inter font family |
| Avalonia.ReactiveUI | 11.3.8 | ReactiveUI integration |
| Azure.Identity | 1.17.1 | Azure authentication |
| Azure.ResourceManager | 1.13.2 | Azure Resource Manager SDK |
| Azure.ResourceManager.ServiceBus | 1.1.0 | Service Bus management |
| Azure.Messaging.ServiceBus | 7.20.1 | Service Bus messaging |
| CommunityToolkit.Mvvm | 8.4.0 | MVVM toolkit with source generators |
| Microsoft.Extensions.DependencyInjection | 10.0.1 | Dependency injection |
| LiveChartsCore.SkiaSharpView.Avalonia | 2.0.0-rc5.4 | Live charts and metrics visualization |
| System.Reactive | 6.0.1 | Reactive extensions for live streaming |

## Security

BusLane takes security seriously and implements multiple layers of protection for your sensitive data.

### Key Security Features

- **AES-256 Encryption** - All saved connection strings are encrypted using AES-256-CBC with machine-specific keys
- **Secure Token Storage** - Azure authentication tokens are stored in your system's secure credential store (Keychain on macOS, Credential Manager on Windows)
- **No Hardcoded Secrets** - Zero API keys, passwords, or credentials in the source code
- **Password Masking** - Connection strings are displayed with bullet characters in the UI
- **Local Storage Only** - Encrypted credentials are stored locally in your user AppData folder, never transmitted

### Connection String Security

When you save a connection string:
1. It's encrypted using AES-256-CBC with a machine-specific key
2. Stored in `%APPDATA%/BusLane/connections.json` (Windows) or `~/.config/BusLane/connections.json` (macOS/Linux)
3. Cannot be decrypted on a different machine
4. Protected from unauthorized access

### Best Practices

- ✅ **Use Azure Authentication** when possible instead of connection strings
- ✅ **Enable MFA** on your Azure account
- ✅ **Rotate keys regularly** if using connection strings
- ✅ **Use least privilege** - only grant necessary RBAC permissions
- ✅ **Keep BusLane updated** to get the latest security patches
- ❌ **Never share** your connection strings or commit them to source control
- ❌ **Don't use saved connections** on shared or public computers

For detailed security information and vulnerability reporting, see [SECURITY.md](SECURITY.md).

## Roadmap

See [ROADMAP.md](ROADMAP.md) for planned features and future development.

Highlights for upcoming releases:
- ⌨️ Keyboard shortcuts
- 🎨 Syntax highlighting for JSON/XML
- 📊 Historical metrics and custom dashboards
- 📈 Export charts and metric comparison
- 🔄 Auto-update functionality
- 📦 Windows/macOS/Linux installers

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

Check the [ROADMAP.md](ROADMAP.md) for ideas on what to contribute.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Avalonia UI](https://avaloniaui.net/) - Cross-platform .NET UI framework
- [Azure SDK for .NET](https://github.com/Azure/azure-sdk-for-net) - Azure service libraries
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM source generators
