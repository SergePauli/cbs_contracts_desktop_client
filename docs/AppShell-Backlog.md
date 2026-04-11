# AppShell Backlog

## Цель

Собрать устойчивый `AppShell` для WinUI 3 после логина так, чтобы shell-регионы и рабочие страницы развивались независимо, а общее состояние layout и контекста жило в одном месте.

## Что уже сделано

- Создан `AppShellPage` с layout на 5 регионов:
  - левый `NavigationSidebarView`
  - верхний `TopBarView`
  - центральный `ContentHostView`
  - правый `AuditPanelView`
  - нижний `FooterBarView`
- Shell подключен после успешного логина через `MainWindow`.
- Добавлен `app.manifest` с `PerMonitorV2` DPI awareness, что выровняло четкость текста и линий до уровня WinUI Gallery.
- Shell приведен к более нативному WinUI-стилю:
  - системная типографика через `Typography.xaml`
  - отказ от локальных `FontFamily="Segoe UI"` для обычного текста
  - стандартный `NavigationView` вместо кастомного меню
- Раздел `Справочники` сделан collapsible и по умолчанию свернут.
- Для `Выход` реализованы logout и возврат на `LoginPage`.
- Создан `TopBarView` с:
  - `BreadcrumbBar`
  - кнопкой сворачивания sidebar
  - `ToggleButton` для открытия и скрытия audit panel
- `TopBarView` подключен к `AppShellPage`.
- Создан общий `AppShellViewModel` как единый shell-state.
- В `AppShellViewModel` уже живут:
  - `IsSidebarVisible`
  - `IsAuditPanelOpen`
  - `SelectedNavigationItem`
  - `BreadcrumbItems`
  - `FooterState`
  - `ContextNavigationItems`
  - `AuditPanelState`
- `FooterBarView` переведен на binding через `FooterState`.
- `AuditPanelView` переведен на binding через `AuditPanelState`.
- `NavigationSidebarView` теперь умеет отображать динамический контекстный блок `Контекст` из `ContextNavigationItems`.

## Текущая архитектурная идея

После входа приложение открывает не временную страницу, а постоянный shell-каркас. Рабочие страницы должны менять только центральный контент и публиковать контекст в общий shell-state.

Shell-state используется как аналог `layoutStore` из web-клиента:

- top bar читает breadcrumbs и состояние audit panel
- sidebar читает выбранный пункт и контекстные ссылки
- audit panel читает текущий таймлайн и описание контекста
- footer читает компактный статус пользователя
- content pages публикуют изменения в shell-state, но не управляют напрямую соседними view

## Актуальные ключевые файлы

- `src/Views/Shell/AppShellPage.xaml`
- `src/Views/Shell/AppShellPage.xaml.cs`
- `src/Views/Shell/NavigationSidebarView.xaml`
- `src/Views/Shell/NavigationSidebarView.xaml.cs`
- `src/Views/Shell/TopBarView.xaml`
- `src/Views/Shell/TopBarView.xaml.cs`
- `src/Views/Shell/ContentHostView.xaml`
- `src/Views/Shell/AuditPanelView.xaml`
- `src/Views/Shell/AuditPanelView.xaml.cs`
- `src/Views/Shell/FooterBarView.xaml`
- `src/Views/Shell/FooterBarView.xaml.cs`
- `src/ViewModels/Shell/AppShellViewModel.cs`
- `src/Models/Shell/FooterState.cs`
- `src/Models/Shell/AuditPanelState.cs`
- `src/Models/Shell/AuditEntry.cs`
- `src/Models/Navigation/NavigationMenuSection.cs`
- `src/Models/Navigation/NavigationMenuItem.cs`
- `src/Services/Navigation/NavigationMenuService.cs`

## Что уже можно считать завершенным

1. Базовый shell после логина.
2. WinUI-стилизация shell и типографики.
3. Четкий рендеринг UI через `PerMonitorV2`.
4. Sidebar c role-based меню и collapsible `Справочники`.
5. Top bar с breadcrumb и shell actions.
6. Logout flow.
7. Общий shell-state для top bar, sidebar, audit panel и footer.

## Что остается следующим этапом

1. Перевести центральную область с placeholder на реальные `Page` через `Frame` или другой единый host-механизм.
2. Ввести `INavigationService` для переключения рабочих страниц внутри shell.
3. Определить `AppPageDefinition` или эквивалентный реестр страниц.
4. Начать перенос первого рабочего сценария из web-клиента.
5. Подключить публикацию контекста из content pages в `AppShellViewModel`:
   - выбранный контракт
   - связанные локальные файлы
   - аудит изменений
6. При необходимости выделить дополнительные shell-модели:
   - `NavigationContextState`
   - `TopBarState`
   - `CurrentPageState`

## Ближайший практический шаг

Следующий полезный результат: сделать первую реальную страницу внутри shell, которая при выборе сущности обновляет:

- `BreadcrumbItems`
- `ContextNavigationItems`
- `AuditPanelState`

Тогда shell станет не просто каркасом, а полноценным контейнером рабочих сценариев.
