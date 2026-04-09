# Настройка среды для WinUI 3 + Windows App SDK

## Требования к системе

- Windows 10 версии 1903 (19H1) или новее, или Windows 11
- Visual Studio 2022 или 2023 (рекомендуется 2023)
- .NET 8.0 или новее (входит в Visual Studio)
- Windows App SDK 1.4 или новее

## Установка Visual Studio

1. Скачайте и установите Visual Studio 2022/2023 с сайта Microsoft: https://visualstudio.microsoft.com/
2. Во время установки выберите следующие компоненты:
   - **Desktop development with C++** (обязательно для WinUI 3)
   - **Universal Windows Platform development** (для UWP-компонентов)
   - **.NET desktop development** (для C# и .NET)
   - **Windows App SDK C# Templates** (если доступно в установщике)

## Установка Windows App SDK

1. Скачайте Windows App SDK с GitHub: https://github.com/microsoft/WindowsAppSDK/releases
2. Установите последнюю стабильную версию (например, 1.4.x)
3. Или через Visual Studio Installer → Modify → Individual components → Windows App SDK

## Создание нового проекта WinUI 3

1. Откройте Visual Studio
2. Создайте новый проект: File → New → Project
3. Выберите шаблон: **Blank App, Packaged (WinUI 3 in Desktop)**
4. Назовите проект: `CbsContractsDesktopClient`
5. Выберите .NET 8.0
6. Укажите расположение: `c:\Projects\cbs_contracts_desktop_client\src`
7. Создайте проект

## Настройка NuGet пакетов

После создания проекта добавьте следующие пакеты через NuGet Package Manager:

### Обязательные

- `CommunityToolkit.Mvvm` (версия 8.2.x) — для MVVM паттерна
- `Microsoft.Extensions.DependencyInjection` (версия 8.0.x) — для DI контейнера
- `Newtonsoft.Json` (версия 13.x) — для JSON сериализации (или используйте System.Text.Json)

### Рекомендуемые для функциональности

- `CommunityToolkit.WinUI.UI` (версия 7.1.x) — дополнительные контролы и утилиты
- `ClosedXML` (версия 0.102.x) — для работы с Excel файлами
- `Microsoft.Extensions.Http` (версия 8.0.x) — для HTTP клиента
- `Microsoft.Extensions.Logging` (версия 8.0.x) — для логирования

## Структура проекта

После создания базового проекта WinUI 3, организуйте файлы следующим образом:

```
src/
├── App.xaml / App.xaml.cs          # Главное приложение
├── MainWindow.xaml / MainWindow.xaml.cs  # Главное окно
├── Views/                          # XAML страницы и контролы
│   ├── MainPage.xaml
│   ├── ContractsPage.xaml
│   └── ...
├── ViewModels/                     # ViewModel классы
│   ├── MainViewModel.cs
│   ├── ContractsViewModel.cs
│   └── ...
├── Models/                         # Модели данных
│   ├── Contract.cs
│   ├── User.cs
│   └── ...
├── Services/                       # Сервисы и API клиенты
│   ├── ApiService.cs
│   ├── ExcelService.cs
│   └── ...
└── Helpers/                        # Вспомогательные классы
    ├── NavigationService.cs
    └── ...
```

## Настройка App.xaml.cs для DI

В `App.xaml.cs` настройте dependency injection:

```csharp
public partial class App : Application
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        m_window = new MainWindow();
        m_window.Activate();
    }

    private Window m_window;
}
```

Добавьте в проект файл `ServiceLocator.cs` для регистрации сервисов.

## Следующие шаги после настройки

1. Создайте базовую навигацию между страницами
2. Реализуйте первый ViewModel и View
3. Настройте HTTP клиент для подключения к API
4. Добавьте модели данных на основе веб-клиента
5. Создайте экран списка договоров

## Полезные ссылки

- [Документация WinUI 3](https://learn.microsoft.com/en-us/windows/winui/winui3/)
- [Windows App SDK](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- [Community Toolkit для WinUI](https://learn.microsoft.com/en-us/windows/communitytoolkit/)
- [Примеры WinUI 3](https://github.com/microsoft/WindowsAppSDK-Samples)
