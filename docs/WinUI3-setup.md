# WinUI 3 Setup

## Актуальный стек проекта

- `WinUI 3`
- `Windows App SDK 1.8.x`
- `.NET 8`
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Http`

См. также `CbsContractsDesktopClient.csproj` для текущих версий пакетов.

## Требования к среде

- Windows 10/11
- Visual Studio 2022 с workload'ами для `.NET desktop development`
- компоненты WinUI / Windows App SDK

## Запуск проекта

1. Открыть `CbsContractsDesktopClient.sln`
2. Выбрать конфигурацию `Debug | x64`
3. Запустить приложение через `F5`

## Что важно знать

- проект уже давно вышел за рамки «чистой заготовки WinUI 3`
- в нем есть собственный shell, auth flow, reference workspace и custom `CbsTableView`
- при диагностике и сборке WinUI/XAML иногда удобнее запускать `dotnet build` или `dotnet test` вне ограничений среды, если локальная песочница мешает писать в `obj`

## Пакеты проекта

На текущий момент в проекте используются:

- `Microsoft.WindowsAppSDK`
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Http`
- `WinUI.TableView`

Последний пакет сейчас не является ядром reference table implementation, но остается в проекте.
