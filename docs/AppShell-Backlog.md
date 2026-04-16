# AppShell Backlog

## Где проект сейчас

Shell уже больше не является только каркасом после логина.

Внутри него уже работает реальный reference workspace, поэтому основной риск проекта сместился с shell-архитектуры на развитие прикладных сценариев.

## Что уже завершено

### Shell layout

- `AppShellPage` с 5 регионами:
  - `NavigationSidebarView`
  - `TopBarView`
  - `ContentHostView`
  - `AuditPanelView`
  - `FooterBarView`

### Shell-state

В `AppShellViewModel` уже живут:

- `IsSidebarVisible`
- `IsAuditPanelOpen`
- `SelectedNavigationItem`
- `BreadcrumbItems`
- `FooterState`
- `ContextNavigationItems`
- `AuditPanelState`
- `AuditPanelText`

### После логина

- переход в shell
- logout flow
- role-based navigation
- collapsible раздел `Справочники`

### Центральный контент

Центральная область уже не placeholder:

- в shell встроен рабочий экран справочников
- он route-driven
- поддерживает lazy loading
- публикует технический контекст в audit panel

## Что теперь считается следующим этапом

Теперь следующий этап уже не “сделать shell”, а:

1. Расширять реальные content scenarios внутри shell
2. Развивать reference workspace как основу прикладных процессов
3. Публиковать в `ContextNavigationItems` и `AuditPanelState` не только техническую телеметрию, но и доменный контекст
4. При росте числа экранов выделить более формальный `INavigationService` и реестр страниц

## Практический next step

Наиболее логичный следующий шаг:

- продолжить развитие экрана справочников
- затем на этой же shell-платформе переносить следующие рабочие страницы из web-клиента
