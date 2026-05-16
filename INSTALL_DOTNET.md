# Установка .NET SDK

## Проблема

На вашей системе не установлен .NET SDK, необходимый для разработки Green District.

## Решение

### Windows (PowerShell Admin)

#### Вариант 1: Через официальный сайт
1. Посетите https://dotnet.microsoft.com/download
2. Скачайте .NET 7 SDK (LTS рекомендуется .NET 8)
3. Запустите установщик
4. Следуйте инструкциям

#### Вариант 2: Через Chocolatey (если установлен)
```powershell
choco install dotnet-sdk
```

#### Вариант 3: Через Windows Package Manager
```powershell
winget install Microsoft.DotNet.SDK.7
```

### После установки

1. Закройте и переоткройте терминал
2. Проверьте версию:
   ```powershell
   dotnet --version
   ```
3. Перейдите в папку проекта:
   ```powershell
   cd "e:\Green District\src"
   dotnet build GreenDistrict.sln
   ```

## Проверка установки

Если видите версию (например "7.0.x"), всё в порядке ✅

## Альтернатива: Visual Studio 2022

Если у вас установлена Visual Studio 2022 Community (бесплатная):
1. Откройте файл `e:\Green District\src\GreenDistrict.sln`
2. Visual Studio автоматически восстановит пакеты
3. Нажмите Build → Build Solution

## Помощь

Если остались проблемы, посетите:
- https://learn.microsoft.com/en-us/dotnet/core/install/windows
- https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
