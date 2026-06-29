# Beta installer

The beta installer is built with Inno Setup 6 from the Release build output and
installs the unpackaged WinUI desktop client for the current Windows user.

## Prerequisites

- .NET SDK 8
- Inno Setup 6

Default Inno compiler path:

```powershell
C:\Users\sealp\AppData\Local\Programs\Inno Setup 6\ISCC.exe
```

## Build

From the repository root:

```powershell
.\scripts\build-installer.ps1
```

The generated installer is written to:

```text
artifacts\installer\CbsContractsDesktopClient-1.0.0-beta-Setup.exe
```

## Installation

The installer does not require administrator privileges. It installs the app to:

```text
%LocalAppData%\Programs\CBS\ContractsDesktopClient
```

It creates a Start menu shortcut and can optionally create a desktop shortcut.
