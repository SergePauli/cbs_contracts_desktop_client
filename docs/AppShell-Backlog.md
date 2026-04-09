# AppShell Backlog

## Цель

Построить базовый shell приложения после логина так, чтобы layout и функциональные страницы можно было независимо развивать, комбинировать и ограничивать по ролям пользователя.

## Архитектурная идея

После успешного входа приложение должно переходить не на временный экран, а на постоянный shell-каркас, который содержит основные регионы интерфейса:

- левый sidebar с меню
- верхний top bar
- центральную область контента
- правый audit panel
- нижний footer bar

Shell должен жить отдельно от конкретных рабочих экранов. Центральные страницы должны подменяться независимо, не разрушая sidebar, top bar, audit panel и footer.

## Рекомендуемая структура папок

- `src/Views/Shell/`
- `src/ViewModels/Shell/`
- `src/Views/Pages/`
- `src/ViewModels/Pages/`
- `src/Services/Navigation/`
- `src/Services/Security/`
- `src/Services/Audit/`
- `src/Models/Navigation/`
- `src/Models/Shell/`

## Рекомендуемые файлы shell

- `src/Views/Shell/AppShellPage.xaml`
- `src/Views/Shell/AppShellPage.xaml.cs`
- `src/Views/Shell/NavigationSidebarView.xaml`
- `src/Views/Shell/NavigationSidebarView.xaml.cs`
- `src/Views/Shell/TopBarView.xaml`
- `src/Views/Shell/TopBarView.xaml.cs`
- `src/Views/Shell/AuditPanelView.xaml`
- `src/Views/Shell/AuditPanelView.xaml.cs`
- `src/Views/Shell/FooterBarView.xaml`
- `src/Views/Shell/FooterBarView.xaml.cs`

## Рекомендуемые ViewModel

- `src/ViewModels/Shell/AppShellViewModel.cs`
- `src/ViewModels/Shell/NavigationSidebarViewModel.cs`
- `src/ViewModels/Shell/TopBarViewModel.cs`
- `src/ViewModels/Shell/AuditPanelViewModel.cs`
- `src/ViewModels/Shell/FooterBarViewModel.cs`

## Навигация и роли

- `src/Services/Navigation/INavigationService.cs`
- `src/Services/Navigation/NavigationService.cs`
- `src/Services/Navigation/INavigationRegistry.cs`
- `src/Services/Navigation/NavigationRegistry.cs`
- `src/Services/Security/IRoleContext.cs`
- `src/Services/Security/RoleContext.cs`

## Модели

- `src/Models/Navigation/NavigationItem.cs`
- `src/Models/Navigation/AppPageDefinition.cs`
- `src/Models/Shell/AppShellState.cs`

## Аудит

- `src/Services/Audit/IAuditContextService.cs`
- `src/Services/Audit/AuditContextService.cs`
- `src/Models/Shell/AuditPanelState.cs`

## Первые page-экраны

- `src/Views/Pages/DashboardPage.xaml`
- `src/Views/Pages/DashboardPage.xaml.cs`
- `src/ViewModels/Pages/DashboardViewModel.cs`

Если первым рабочим экраном будет список договоров:

- `src/Views/Pages/ContractsPage.xaml`
- `src/Views/Pages/ContractsPage.xaml.cs`
- `src/ViewModels/Pages/ContractsViewModel.cs`

## Принцип связности

- `MainWindow` после логина открывает `AppShellPage`
- `AppShellPage` держит постоянные sidebar, top bar, audit panel и footer
- центральная область внутри `AppShellPage` переключается через `Frame`
- `NavigationService` управляет только центральным контентом
- `RoleContext` определяет, какие разделы и страницы доступны пользователю
- `AuditContextService` позволяет любой странице открыть правую audit-панель в нужном контексте

## Технический backlog

1. Создать папки `Views/Shell`, `ViewModels/Shell`, `Services/Navigation`, `Services/Security`, `Models/Navigation`, `Models/Shell`.
2. Создать `AppShellPage` с layout на 5 регионов.
3. Создать `NavigationSidebarView`, `TopBarView`, `AuditPanelView`, `FooterBarView` как отдельные `UserControl`.
4. Создать `AppShellViewModel` и вынести в него состояние shell: текущая страница, открытие audit panel, footer status.
5. Ввести `NavigationItem` и `AppPageDefinition` для описания меню и страниц.
6. Реализовать `IRoleContext` и `RoleContext` на базе текущего `UserService`.
7. Реализовать `INavigationRegistry` и `NavigationRegistry` со списком доступных страниц.
8. Реализовать `INavigationService` для переключения центральной области.
9. Подключить role-based фильтрацию пунктов меню в `NavigationSidebarViewModel`.
10. Создать `DashboardPage` как первый экран внутри shell.
11. Переключить успешный логин с debug-экрана на переход в `AppShellPage`.
12. Добавить базовый `IAuditContextService`, даже если правая панель пока будет с заглушкой.
13. Добавить базовый footer status: версия клиента, пользователь, состояние API.
14. После стабилизации shell начать перенос первого рабочего экрана: `ContractsPage` или `DashboardPage`.

## Что создавать первыми

- `src/Views/Shell/AppShellPage.xaml`
- `src/ViewModels/Shell/AppShellViewModel.cs`
- `src/Views/Shell/NavigationSidebarView.xaml`
- `src/Views/Shell/TopBarView.xaml`
- `src/Views/Shell/AuditPanelView.xaml`
- `src/Views/Shell/FooterBarView.xaml`
- `src/Services/Navigation/INavigationService.cs`
- `src/Services/Navigation/NavigationService.cs`
- `src/Services/Security/IRoleContext.cs`
- `src/Services/Security/RoleContext.cs`
- `src/Models/Navigation/NavigationItem.cs`
- `src/Models/Navigation/AppPageDefinition.cs`
