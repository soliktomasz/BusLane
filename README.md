# BusLane

A modern, cross-platform Azure Service Bus management tool built with Avalonia UI and .NET 8.

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)
![Avalonia UI](https://img.shields.io/badge/Avalonia-11.1-8B44AC?style=flat)
![License](https://img.shields.io/badge/License-MIT-green.svg)

## Features

- 🔐 **Azure Authentication** - Sign in with your Azure account using Azure Identity
- 📋 **Subscription Management** - Browse and switch between Azure subscriptions
- 🏢 **Namespace Explorer** - View all Service Bus namespaces in your subscription
- 📬 **Queue Management** - Browse queues, view message counts, and manage messages
- 📨 **Topic & Subscription Support** - Full support for topics and their subscriptions
- 👀 **Message Peek** - Preview messages without consuming them
- ✉️ **Send Messages** - Send new messages with custom properties, headers, and scheduling
- 🗑️ **Dead Letter Queue** - View and manage dead-lettered messages
- 🧹 **Purge Messages** - Bulk delete messages from queues or subscriptions
- 💾 **Session Persistence** - Automatically restores your previous session

## Screenshots

*Coming soon*

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An Azure account with access to Azure Service Bus resources
- Required Azure RBAC roles:
  - `Azure Service Bus Data Receiver` - to peek/receive messages
  - `Azure Service Bus Data Sender` - to send messages
  - `Reader` - to browse namespaces, queues, and topics

## Installation

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
# For macOS
dotnet publish -c Release -r osx-x64 --self-contained

# For Windows
dotnet publish -c Release -r win-x64 --self-contained

# For Linux
dotnet publish -c Release -r linux-x64 --self-contained
```

## Usage

1. **Sign In** - Click "Sign in with Azure" to authenticate with your Azure account
2. **Select Subscription** - Choose the Azure subscription containing your Service Bus namespaces
3. **Browse Namespaces** - Click on a namespace to view its queues and topics
4. **View Messages** - Select a queue or topic subscription to peek at messages
5. **Toggle Dead Letter** - Use the dead letter toggle to view dead-lettered messages
6. **Send Messages** - Click the send button to compose and send new messages

## Architecture

BusLane follows the MVVM (Model-View-ViewModel) pattern:

```
BusLane/
├── Models/          # Data models (QueueInfo, TopicInfo, MessageInfo, etc.)
├── Services/        # Azure integration services
│   ├── IAzureAuthService.cs      # Authentication interface
│   ├── AzureAuthService.cs       # Azure Identity implementation
│   ├── IServiceBusService.cs     # Service Bus operations interface
│   └── ServiceBusService.cs      # Service Bus implementation
├── ViewModels/      # MVVM ViewModels with CommunityToolkit.Mvvm
├── Views/           # Avalonia XAML views
└── Styles/          # Application styles and themes
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Avalonia | 11.1.0 | Cross-platform UI framework |
| Avalonia.Themes.Fluent | 11.1.0 | Fluent design theme |
| Azure.Identity | 1.17.1 | Azure authentication |
| Azure.ResourceManager | 1.13.2 | Azure Resource Manager SDK |
| Azure.ResourceManager.ServiceBus | 1.1.0 | Service Bus management |
| Azure.Messaging.ServiceBus | 7.20.1 | Service Bus messaging |
| CommunityToolkit.Mvvm | 8.2.2 | MVVM toolkit with source generators |

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- [Avalonia UI](https://avaloniaui.net/) - Cross-platform .NET UI framework
- [Azure SDK for .NET](https://github.com/Azure/azure-sdk-for-net) - Azure service libraries
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM source generators
