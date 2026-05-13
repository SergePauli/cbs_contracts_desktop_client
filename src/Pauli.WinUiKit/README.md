# Pauli.WinUiKit

Reusable WinUI controls and small layout primitives for Pauli desktop applications.

The package is intentionally independent from application models, stores, API clients, and business rules. Controls should expose plain WinUI-friendly properties and events, so the same package can be used by multiple apps or moved to a standalone repository.

## Current Controls

- `CalendarInput` - compact date editor with manual text input, clear button, and calendar flyout.
- `MultiSelect` - dropdown multi-select with search, selected chips, `Options`, `Value`, `OptionLabel`, `OptionItemLabel`, `Display`, `MaxSelectedLabels`, `Placeholder`, `SelectionChanged`, and `Tooltip`.

## Design Rules

- Controls must not depend on CBS contracts domain types.
- Visual behavior belongs in the kit; business rules stay in the consuming app.
- Public APIs should be stable, small, and documented before package releases.

## Packaging

Build a NuGet package with:

```powershell
dotnet pack src\Pauli.WinUiKit\Pauli.WinUiKit.csproj -c Release
```

The generated package can later be published from this repository or moved to a dedicated `Pauli.WinUiKit` repository without changing package consumers.
