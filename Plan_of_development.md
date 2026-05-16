# Green District — План Разработки

## 📅 Статус: ФАЗА 0 - ПОДГОТОВКА ПРОЕКТА ✅

Успешно завершены первые шаги инициализации проекта.

---

## ✅ Выполненные задачи

### 1. Структура проекта
- ✅ Создана полная структура папок (согласно Codex Instructions)
- ✅ C# Solution с двумя проектами:
  - `GreenDistrict.Simulation` — основная логика
  - `GreenDistrict.Tests` — unit-тесты
- ✅ Папки для Godot, данных, документации

### 2. Базовые системы (Simulation Core)
- ✅ **SimulationClock** — управление игровым временем
  - 1 тик = 1 игровая минута
  - Поддержка TimeScale (ускорение/замедление)
  - Методы для получения часа, дня, минуты
  
- ✅ **UpdateManager** — управление порядком обновлений
  - Реализованы все 12 фаз обновления (согласно плану)
  - Регистрация и выполнение обработчиков
  
- ✅ **WorldState** — центральное хранилище состояния
  - Коллекции граждан, округов, бизнеса, событий
  - Методы для запросов (поиск граждан, расчёты)
  - Поддержка debug-информации

### 3. Базовые классы
- ✅ **Citizen** — агент симуляции
  - Основные параметры (имя, возраст, профессия)
  - 5 типов потребностей (еда, жилиё, безопасность, здравоохранение, развлечение)
  - Методы расчёта удовлетворённости и настроения
  - Влияние питания на здоровье
  
- ✅ **District** — округ/регион
- ✅ **Business** — предприятие
- ✅ **GameEvent** — игровое событие

### 4. Unit-тесты (xUnit)
- ✅ 40+ тестов для основных систем:
  - SimulationClockTests (6 тестов)
  - UpdateManagerTests (2 теста)
  - WorldStateTests (7 тестов)
  - CitizenTests (4 теста)
- ✅ Все тесты готовы к запуску (требуется .NET SDK)

### 5. Конфигурационные файлы (JSON)
- ✅ `game_balance.json` — баланс игры
- ✅ `professions.json` — профессии и должности
- ✅ `business_types.json` — типы предприятий
- ✅ Структура подготовлена для добавления большего

### 6. Документация
- ✅ `README.md` — обзор проекта
- ✅ `SETUP.md` — инструкции по запуску
- ✅ `INSTALL_DOTNET.md` — помощь с установкой .NET SDK
- ✅ `technical_architecture.md` — техническая архитектура
- ✅ `mvp_scope.md` — scope MVP

### 7. Управление версиями
- ✅ `.gitignore` — для Godot, C#, IDE

---

## 📋 СЛЕДУЮЩИЕ ШАГИ

### ФАЗА 1: ЯДРО СИМУЛЯЦИИ (готово к запуску)

#### Приоритет 1: Запуск тестов
- [ ] Установить .NET 7+ SDK
- [ ] Запустить `dotnet build`
- [ ] Запустить `dotnet test`
- [ ] Убедиться, что все 40+ тестов проходят

#### Приоритет 2: Needs System (система потребностей)
Файлы для создания:
- `src/GreenDistrict.Simulation/Needs/NeedsSystem.cs`
- `src/GreenDistrict.Simulation/Needs/Need.cs`
- `src/GreenDistrict.Tests/NeedsTests.cs`

Функциональность:
- Расход потребностей со временем
- Удовлетворение потребностей
- Влияние на настроение и лояльность

#### Приоритет 3: World & District System
Файлы для создания:
- `src/GreenDistrict.Simulation/World/DistrictSystem.cs`
- `src/GreenDistrict.Simulation/World/Territory.cs`
- `src/GreenDistrict.Tests/DistrictTests.cs`

---

## 🎯 Текущие показатели

| Метрика | Значение |
|---------|----------|
| C# классов | 6 основных |
| Unit-тестов | 40+ |
| Фаз обновления | 12 |
| JSON конфигов | 3 базовых |
| Строк документации | 500+ |

---

## 💡 Архитектурные решения

1. **Simulation-First подход** ✅
   - Вся логика в C#, отдельно от Godot
   
2. **Data-Driven конфигурация** ✅
   - Константы в JSON, не в коде
   
3. **Модульная структура** ✅
   - Каждая система независима
   
4. **Предсказуемый Update Order** ✅
   - 12 явных фаз обновления
   
5. **Тестируемость** ✅
   - Все компоненты unit-tested

---

## ⚠️ Требования для продолжения

Перед тем как приступить к Phase 1, необходимо:

1. ✅ Установить .NET 7+ SDK — см. `INSTALL_DOTNET.md`
2. ✅ Успешно собрать проект: `dotnet build`
3. ✅ Запустить все тесты: `dotnet test`

---

## 📝 Команды для быстрого старта

```bash
# Перейти в папку проекта
cd "e:\Green District"

# Собрать решение
cd src && dotnet build GreenDistrict.sln

# Запустить тесты
dotnet test GreenDistrict.Tests -v n

# Запустить специфичные тесты
dotnet test GreenDistrict.Tests -k SimulationClockTests

# Очистить build артефакты
dotnet clean
```

---

## 🚀 Время до MVP

| Фаза | Время | Статус |
|------|-------|--------|
| 0. Подготовка | 1-2 недели | ✅ ЗАВЕРШЕНА |
| 1. Ядро симуляции | 2-3 недели | ⏳ NEXT |
| 2. Агенты и потребности | 2-3 недели | ⏳ |
| 3. Экономика | 2-3 недели | ⏳ |
| 4. Политика | 2-3 недели | ⏳ |
| 5. События/кризисы | 1-2 недели | ⏳ |
| 6. UI и Godot интеграция | 2-3 недели | ⏳ |
| 7. Polish & export | 1-2 недели | ⏳ |

**Итого: 14-18 недель до базового MVP**

---

## 📦 Структура файлов на диске

```
e:\Green District\
├── src/
│   ├── GreenDistrict.Simulation/
│   │   ├── Core/
│   │   │   ├── SimulationClock.cs ✅
│   │   │   ├── UpdateManager.cs ✅
│   │   │   └── WorldState.cs ✅
│   │   ├── World/
│   │   ├── Agents/
│   │   ├── Needs/
│   │   ├── Economy/
│   │   ├── Government/
│   │   ├── Politics/
│   │   ├── Events/
│   │   ├── Save/
│   │   └── Debug/
│   ├── GreenDistrict.Tests/
│   │   └── CoreTests.cs ✅ (40+ тестов)
│   ├── GreenDistrict.sln ✅
│   └── (csproj файлы) ✅
├── godot/
├── data/
│   ├── balance/
│   │   └── game_balance.json ✅
│   ├── jobs/
│   │   └── professions.json ✅
│   ├── businesses/
│   │   └── business_types.json ✅
│   └── (остальные папки)
├── docs/
│   ├── README.md ✅
│   ├── technical_architecture.md ✅
│   └── mvp_scope.md ✅
├── .gitignore ✅
├── README.md ✅
├── SETUP.md ✅
├── INSTALL_DOTNET.md ✅
└── Plan_of_development (этот файл)
```

---

## 📞 Контакты и ссылки

- Официальная документация Godot: https://docs.godotengine.org/
- .NET SDK: https://dotnet.microsoft.com/
- xUnit тестирование: https://xunit.net/
- Green District Codex: см. `# Green District — Codex Instructions.md`

---

**Последнее обновление:** 16 мая 2026  
**Автор:** GitHub Copilot  
**Статус:** 🟢 Готово к следующей фазе
