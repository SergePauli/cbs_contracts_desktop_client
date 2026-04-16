# Login Window Implementation

## Статус

Экран логина реализован и уже является рабочей точкой входа в приложение.

Это больше не прототип и не временная заглушка.

## Что поддерживается

- поля username/password
- валидация и отображение ошибки
- состояние загрузки
- чекбокс `Запомнить меня`
- сохранение учетных данных через Windows Credential Manager
- переход в `AppShell` после успешного логина
- logout с возвратом на `LoginPage`

## Ключевые файлы

- `src/Views/LoginPage.xaml`
- `src/Views/LoginPage.xaml.cs`
- `src/ViewModels/LoginViewModel.cs`
- `src/Services/AuthService.cs`
- `src/Services/UserService.cs`
- `src/Services/CredentialManagerService.cs`

## Что уже неактуально из ранних планов

Следующие пункты уже закрыты и не являются «следующим шагом»:

- настройка DI
- навигация после логина
- подключение shell вместо debug-экрана
- базовая интеграция с реальным API

## Что может быть следующим развитием именно для login flow

- более аккуратная обработка `401` и истечения токена
- silent restore session при старте
- формы регистрации / восстановления пароля, если это потребуется бизнес-сценарию
