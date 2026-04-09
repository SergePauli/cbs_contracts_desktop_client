# cbs_contracts_desktop_client

Цель: создать нативный Windows-десктоп клиент на WinUI 3 + Windows App SDK, портируя UI и бизнес-логику из `cbs_contracts_webclient`.

## Что уже есть

- Внутри `cbs_contracts_webclient` реализован React SPA с маршрутизацией, формами, таблицами, загрузкой/выгрузкой данных и API-интеграцией.
- `cbs_contracts_desktop_client` пока пустой — сюда будет выстраиваться нативный Windows-проект.

## Основная стратегия

1. Использовать `cbs_contracts_webclient` как референс для экранов, маршрутной структуры и пользовательских сценариев.
2. Проектировать нативный UI в WinUI 3 с XAML и C#.
3. Перенести сетевые запросы из `axios` в `HttpClient`/`System.Net.Http`.
4. Перенести экспорт/импорт Excel, CSV, файловую работу на .NET-библиотеки.
5. Разделить проект на слои:
   - UI (XAML + ViewModels)
   - Данные / API-клиент
   - Модели
   - Сервисы

## Что будет в этой папке

- `src/` — исходники WinUI-приложения
- `src/Models/` — модели данных и DTO
- `src/ViewModels/` — ViewModel-слой
- `src/Views/` — страницы и диалоги XAML
- `src/Services/` — HTTP, хранилище, экспорт/импорт
- `docs/` — инструкции по настройке среды и архитектуре

## Ближайшие шаги

1. Настроить среду разработки:
   - Установить Visual Studio 2022 или 2023
   - Установить `Desktop development with C++` и `Universal Windows Platform development`
   - Установить `Windows App SDK` (последняя стабильная версия)
2. Создать проект WinUI 3:
   - `Blank App, Packaged (WinUI 3 in Desktop)`
   - Назвать `CbsContractsDesktopClient`
3. Создать папки `src/Views`, `src/ViewModels`, `src/Models`, `src/Services`, `src/Helpers`
4. Скопировать структуру экранов из веб-клиента и разместить её в `Views` и `ViewModels`
5. Реализовать первый экран:
   - стартовый экран / таблица договоров
   - навигацию
   - модель данных
6. Подключить HTTP-клиент и получить тестовый ответ из API
7. Добавить пакет `CommunityToolkit.Mvvm` для MVVM
8. Протестировать запуск и убедиться, что приложение компилируется

## Что не делать сразу

- Не переписывать весь интерфейс за один проход.
- Не пытаться повторять все анимации/стили PrimeReact.
- Сразу оставить сложные “Excel-экспорт” и “массовый импорт” на потом.

## Примеры пакетов, которые понадобятся

- `Microsoft.WindowsAppSDK` (WinUI 3)
- `CommunityToolkit.Mvvm`
- `System.Net.Http`
- `Newtonsoft.Json` или `System.Text.Json`
- `CommunityToolkit.WinUI.UI` (если нужны дополнительные контролы)
- `ClosedXML` или `EPPlus` для работы с Excel
- `Microsoft.Extensions.DependencyInjection` (опционально)

## Следующее действие

- Создать начальный WinUI 3 проект и заполнить `src/` базовым шаблоном.
- Затем документировать первую цель: экран списка договоров и API-слой.
