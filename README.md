# BusLane

A cross-platform Azure Service Bus management tool built with Avalonia UI and .NET 10.

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet)
![Avalonia UI](https://img.shields.io/badge/Avalonia-11.3-8B44AC?style=flat)
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Platform](https://img.shields.io/badge/Platform-macOS%20%7C%20Windows%20%7C%20Linux-lightgrey.svg)
[![BuyMeACoffee](https://img.shields.io/badge/Buy%20Me%20A%20Coffee-tomaszsolik-FFDD00?style=flat&logo=buy-me-a-coffee&logoColor=000000)](https://www.buymeacoffee.com/tomaszsolik)

<p align="center">
  <a href="https://soliktomasz.github.io/BusLane/">Website</a> |
  <a href="#installation">Installation</a> |
  <a href="#features">Features</a> |
  <a href="ROADMAP.md">Roadmap</a> |
  <a href="SECURITY.md">Security</a>
</p>

---

![BusLane Main Screen](docs/MainScreen.png)

## Features

- **Dual connection modes** - Azure Identity authentication or direct connection strings
- **Full entity support** - Queues, topics, subscriptions, and session-enabled entities
- **Message operations** - Peek, send, resend from DLQ, purge, search and filter
- **Live monitoring** - Real-time message streaming, charts, and configurable alerts
- **Secure storage** - AES-256 encrypted connection strings with machine-specific keys

## Installation

Download the latest release from [Releases](https://github.com/soliktomasz/BusLane/releases).

**macOS note:** Run `xattr -cr "/Applications/Bus Lane.app"` if Gatekeeper blocks the app.

### Build from Source

```bash
git clone https://github.com/soliktomasz/BusLane.git
cd BusLane
dotnet build
dotnet run
```

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (for building)
- Azure RBAC roles: `Azure Service Bus Data Receiver`, `Azure Service Bus Data Sender`, `Reader`

## Usage

**Azure mode:** Sign in with Azure > Select subscription > Browse namespaces > View messages

**Connection string mode:** Open connection library > Add/select connection > Browse entities

## Contributing

Contributions welcome. Fork, create a feature branch, and submit a PR. See [ROADMAP.md](ROADMAP.md) for ideas.

## License

MIT - see [LICENSE](LICENSE)
