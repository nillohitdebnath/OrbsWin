# OrbsWin

A .NET 8 WPF application that runs strictly as a system tray app with no visible main window or taskbar icon on launch.

## Requirements

- .NET 8 SDK

## Building the Application

To build the project:

```bash
dotnet build
```

## Running the Application

To run the application:

```bash
dotnet run
```

When started, OrbsWin runs quietly in the Windows system tray. 

### System Tray Features

Right-clicking the system tray icon brings up the context menu:

- **Settings**: Stub (reserved for future settings window).
- **Start with Windows**: Toggleable menu item stub (reserved for autostart setup).
- **Quit**: Exits and shuts down the application cleanly.

## Asset Fallback

The app looks for a tray icon at `Assets/tray-icon.ico`. If missing or unreadable, a default blue circular icon is dynamically generated at runtime.
