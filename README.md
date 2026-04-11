# cbs_contracts_desktop_client

Нативный Windows-клиент для системы «База контрактов и контрагентов», реализуемый на WinUI 3 и Windows App SDK с опорой на существующий web-клиент `cbs_contracts_webclient`.

## Текущий статус

- Работает WinUI 3 desktop-приложение на .NET 8.
- Подключены DI, `HttpClient` и базовые сервисы приложения.
- Работает авторизация против внешнего API.
- Реализовано сохранение учетных данных через Windows Credential Manager.
- После успешного логина открывается полноценный `AppShell`, а не временный debug-экран.
- Shell уже переведен на общий state через `AppShellViewModel`.
- Типографика и четкость рендеринга приведены к уровню WinUI Gallery через `PerMonitorV2` DPI awareness и системные WinUI-стили.

## Что уже сделано

### Инфраструктура

- Создан WinUI 3 проект под .NET 8.
- Подключены:
  - `Microsoft.WindowsAppSDK`
  - `CommunityToolkit.Mvvm`
  - `Microsoft.Extensions.DependencyInjection`
  - `Microsoft.Extensions.Http`
- В `App.xaml.cs` настроен контейнер зависимостей.
- В проект добавлен `app.manifest` с `PerMonitorV2` DPI awareness.

### Авторизация

- Реализован `AuthService` для входа через API.
- Базовый адрес API сейчас настроен на `http://serge-lenovo:5000`.
- Вход выполняется через endpoint `POST auth/login`.
- Ответ сервера преобразуется в локальную модель пользователя.
- Текущий пользователь сохраняется в `UserService`.
- Реализован logout с возвратом на `LoginPage`.

### Экран логина

- Собран экран авторизации на XAML.
- Добавлены поля логина и пароля, индикатор загрузки, вывод ошибки входа и чекбокс `Запомнить меня`.
- Правая колонка оформлена как информационная панель с логотипом и версией клиента.

### Shell после логина

- Добавлен `AppShellPage` как базовый каркас приложения.
- Layout разделен на 5 регионов:
  - левый sidebar
  - верхний top bar
  - центральная область контента
  - правый audit panel
  - нижний footer bar
- Регионы вынесены в отдельные `UserControl`:
  - `NavigationSidebarView`
  - `TopBarView`
  - `ContentHostView`
  - `AuditPanelView`
  - `FooterBarView`
- Sidebar построен на стандартном `NavigationView`.
- Раздел `Справочники` сделан collapsible и по умолчанию свернут.
- `TopBarView` использует `BreadcrumbBar`, кнопку сворачивания sidebar и `ToggleButton` audit panel.
- Обычный текст переведен на общие типографические стили из `Typography.xaml`.

### Общий shell-state

- Добавлен `AppShellViewModel` как единый state для shell.
- В нем уже представлены:
  - видимость sidebar
  - открытие audit panel
  - выбранный пункт меню
  - breadcrumbs
  - footer-state
  - контекстные ссылки для sidebar
  - состояние audit panel
- `TopBarView`, `NavigationSidebarView`, `AuditPanelView` и `FooterBarView` работают через этот shared state.

### Footer и audit panel

- `FooterBarView` получает данные через `FooterState`.
- `AuditPanelView` получает данные через `AuditPanelState`.
- Sidebar уже умеет показывать контекстный раздел `Контекст`, который позже будет заполняться из рабочих страниц.

## Структура проекта

- `src/Models/` — модели данных и shell-state
- `src/Services/` — API, пользовательское состояние, навигационные сервисы
- `src/ViewModels/` — MVVM-логика
- `src/Views/` — XAML-страницы и `UserControl`
- `Assets/` — используемые иконки и изображения
- `docs/` — сопроводительная документация

## Что пока остается временным

- Центральная область `AppShell` пока показывает placeholder вместо реальной рабочей страницы.
- Контекстные ссылки sidebar и audit panel пока подключены как механизм, но еще не заполняются живыми данными из рабочего экрана.
- Ссылки `Регистрация` и `Не помню пароль` пока ведут на заглушки.
- Версия в footer пока задана статично как `v1.0.0`.

## Следующий шаг

Следующий практический этап — подключить первую реальную рабочую страницу внутри shell и связать ее с общим shell-state.

Минимальный полезный результат следующей итерации:

1. Пользователь входит в систему.
2. Попадает в `AppShell`.
3. В центральной области открывается первая рабочая страница.
4. При выборе сущности на странице обновляются:
   - breadcrumbs
   - контекстный блок sidebar
   - audit panel

## Запуск в Visual Studio 2022

1. Открыть `CbsContractsDesktopClient.sln`.
2. Выбрать конфигурацию `Debug | x64`.
3. Убедиться, что установлены workload'ы для .NET desktop development и компоненты WinUI 3 / Windows App SDK.
4. Запустить проект через `F5`.

## Тесты

- В solution добавлен тестовый проект `tests/CbsContractsDesktopClient.Tests`.
- Базовый стек тестирования:
  - `xUnit`
  - `Microsoft.NET.Test.Sdk`
  - `coverlet.collector`
- Уже покрыты базовые сценарии для:
  - `UserService`
  - `AuthService`
  - `LoginViewModel`

Запуск из консоли:

1. `dotnet test`

Хорошие следующие кандидаты на покрытие:

- `AppShellViewModel`
- `NavigationMenuService`
- будущий `INavigationService`
- публикация shell-context из рабочих страниц

## Ближайшие направления развития

- первая реальная страница после логина
- навигация между страницами внутри shell
- контекстные данные для sidebar и audit panel
- обработка `401` и истечения токена
- формы регистрации и восстановления пароля
- перенос следующих экранов из web-клиента
