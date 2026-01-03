# BusLane Roadmap

This document outlines the planned features and improvements for BusLane. Items are organized by priority and timeline, though this may change based on community feedback and contributions.

> 💡 **Have a suggestion?** Open an [issue](https://github.com/soliktomasz/BusLane/issues) or start a [discussion](https://github.com/soliktomasz/BusLane/discussions)!

---

## ✅ Completed (v0.7.0)

- [x] **Message Search & Filter** — Search messages by content, properties, or metadata
- [x] **Bulk Operations** — Select multiple messages for delete/move operations
- [x] **Export Messages** — Export messages to JSON/XML files
- [x] **Import Messages** — Send messages from JSON/XML files
- [x] **Resend from DLQ** — Resend dead-letter messages to original queue

---

## 📅 Planned

### v0.8.0 — Developer Experience

- [ ] **Keyboard Shortcuts** — Quick actions (Ctrl+R refresh, Ctrl+N new message, etc.)
- [ ] **Syntax Highlighting** — JSON/XML highlighting in message body
- [ ] **JSON Formatter** — Format and validate JSON message bodies
- [ ] **Code Generation** — Generate C#/Python code snippets for sending messages
- [ ] **Recent Connections** — Quick access to recently used connections

### v0.9.0 — Advanced Monitoring

- [ ] **Historical Metrics** — Store and display metric history
- [ ] **Custom Dashboards** — Configurable dashboard layouts
- [ ] **Export Charts** — Save charts as images or PDF
- [ ] **Metric Comparison** — Compare metrics across queues/namespaces
- [ ] **Scheduled Reports** — Generate periodic health reports

### v1.0.0 — Production Ready

- [ ] **Auto-Update** — In-app update notifications and installation
- [ ] **Windows Installer** — MSI/MSIX package
- [ ] **macOS Signing** — Code signing and notarization
- [ ] **Linux Packages** — AppImage, Flatpak, or Snap
- [ ] **Comprehensive Documentation** — User guide and API docs
- [ ] **Logging** — Integrated logging with Serilog
- [ ] **Telemetry** — Optional anonymous usage analytics

---

## 🔮 Future Considerations

These items are being considered for future releases but are not yet scheduled:

### Additional Azure Services
- [ ] Azure Event Hubs support
- [ ] Azure Storage Queues support
- [ ] Azure Event Grid integration

### Collaboration
- [ ] Export/Import connection configurations (encrypted)
- [ ] Shared message templates
- [ ] Operation audit log

### Advanced Tooling
- [ ] Message scheduling calendar view
- [ ] Load testing (send N messages)
- [ ] Schema registry integration
- [ ] Message transformation pipelines
- [ ] Request/response testing mode

### UI/UX Enhancements
- [ ] Light/Dark theme toggle
- [ ] Customizable message list columns
- [ ] Notification sounds for alerts
- [ ] Drag-and-drop support
- [ ] Multi-window support

### AI-Powered Features
- [ ] Natural language message search
- [ ] Anomaly detection in message patterns
- [ ] Smart property suggestions
- [ ] Auto-generate test messages from schema

---

## 🤝 Contributing

We welcome contributions! If you'd like to work on any roadmap item:

1. Check [existing issues](https://github.com/soliktomasz/BusLane/issues) to avoid duplicates
2. Open an issue to discuss your approach
3. Fork the repository and create a feature branch
4. Submit a pull request

See [CONTRIBUTING.md](CONTRIBUTING.md) for detailed guidelines.

---

## 📊 Priority Matrix

| Priority | Impact | Effort | Items |
|----------|--------|--------|-------|
| 🔴 High | High | Low | Recent connections, Keyboard shortcuts |
| 🟠 Medium | High | Medium | Syntax highlighting, JSON formatter, Code generation |
| 🟡 Medium | Medium | Medium | Historical metrics, Custom dashboards, Auto-update |
| 🟢 Low | Medium | High | Additional Azure services, AI features |

---

## 📝 Version History

See [CHANGELOG.md](CHANGELOG.md) for detailed release notes.

| Version | Date | Highlights |
|---------|------|------------|
| v0.7.0 | 2026 | Enhanced Message Management, Search & Filter, Bulk Operations, Export/Import |
| v0.6.0 | 2025 | Live Charts, Alert system |
| v0.5.x | 2025 | Connection library, Message templates |
| v0.4.x | 2024 | Session support, DLQ improvements |
| v0.3.x | 2024 | Initial public release |

---

<p align="center">
  <i>This roadmap is a living document and will be updated as the project evolves.</i>
</p>
