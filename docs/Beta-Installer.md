# Beta installer

The beta installer is built with Inno Setup 6 from the Release build output and
installs the unpackaged WinUI desktop client for the current Windows user.

## Prerequisites

Build machine:

- .NET SDK 8
- Inno Setup 6

Default Inno compiler path:

```powershell
C:\Users\sealp\AppData\Local\Programs\Inno Setup 6\ISCC.exe
```

Target machine prerequisites are installed separately and are not included in
the application installer:

- [.NET SDK 8.0.424 x64](https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.424/dotnet-sdk-8.0.424-win-x64.exe)
- [Windows App Runtime 1.8 x64](https://aka.ms/windowsappsdk/1.8/1.8.260710003/windowsappruntimeinstall-x64.exe)
- [Microsoft Visual C++ Redistributable x64](https://aka.ms/vc14/vc_redist.x64.exe)

## Build

From the repository root:

```powershell
.\scripts\build-installer.ps1 -FnsApiKey "FNS API key"
```

Alternatively, set `CBS_FNS_KEY` for the build process and omit the parameter.
The key is embedded into the application assembly for the distribution; target
machines do not require an FNS key environment variable.

The generated installer is written to:

```text
artifacts\installer\CbsContractsDesktopClient-1.0.7-beta-Setup.exe
```

The script builds the application as framework-dependent and packages the full
WinUI build output, including the application PRI and XBF resources. It does
not download or package the .NET, Windows App Runtime, or Visual C++
prerequisite installers.

## Installation

The installer does not require administrator privileges. It installs the app to:

```text
%LocalAppData%\Programs\CBS\ContractsDesktopClient
```

It creates a Start menu shortcut and can optionally create a desktop shortcut.
