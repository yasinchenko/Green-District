# План Дальнейшей Разработки

## Статус по циклам

- Последний цикл: Фаза 4, районные агрегаты расширены по рабочим местам, безопасности, услугам и проектам.
- Проверка последнего цикла: `dotnet build src\GreenDistrict.sln` и `dotnet test src\GreenDistrict.sln --no-build` проходят, 76/76 тестов.

## Фаза 1: Стабилизация Ядра - готово

Срок: 2-4 дня.

- [x] Исправить `professions.json`.
- [x] Обновить или убрать явную зависимость `System.Text.Json 8.0.0`.
- [x] Ввести инварианты занятости: единый метод `Hire/Fire/Retire`, синхронизация `Citizen.Job`, `Business.EmployeeIds`, `EmployeeCount`.
- [x] Запретить найм детей, пенсионеров и умерших/неактивных граждан.
- [x] Добавить тесты на пенсию, смерть, увольнение и payroll без stale employee ids.
- [x] Доработать `UpdateManager`, чтобы фаза могла иметь несколько handlers в стабильном порядке.

## Фаза 2: Нормальная Модель Агента - готово

Срок: 1 неделя.

- [x] Разнести модели из `WorldState.cs` по отдельным файлам: `Citizen`, `Household`, `Business`, `District`, `GameEvent`.
- [x] Добавить `LifeStage`: `Child`, `Student`, `Adult`, `Retired`.
- [x] Добавить `EmploymentStatus`: `Unemployed`, `Employed`, `Retired`, `Student`.
- [x] Добавить домохозяйства: состав семьи, доход семьи, базовое жильё, район.
- [x] Уточнить демографию: пол ребёнка через RNG, фамилии/имена, район, household и родительские связи.
- [x] Сделать `WorldState.Initialize()` реальной загрузкой стартового сценария.

## Сквозной блок: Локализация - в работе

- [x] Предусмотреть переключение языка игры: русский/английский.
- [x] Добавить `LocalizationSystem` в simulation layer.
- [x] Добавить словари `data/localization/en.json` и `data/localization/ru.json`.
- [x] Добавить fallback: активный язык -> английский -> ключ строки.
- [x] Покрыть загрузку словарей и переключение языка тестами.
- [ ] Перевести все пользовательские события (`GameEvent`) на ключи локализации вместо hardcoded text.
- [ ] Подключить переключатель языка в Godot UI.
- [ ] Добавить проверку полноты словарей ru/en.

Примечания по частично готовым пунктам:

- Домохозяйства имеют состав семьи, район, доход семьи, per-capita income и синхронизацию с полноценными `HousingUnit`; поля `HousingUnitId`/`HousingCapacity`/`RentPerTick` сохранены как удобный кэш и для обратной совместимости сценариев.
- Демография уже задаёт пол ребёнка через RNG, имя/фамилию, район, household, `MotherId` и, если возможно, `FatherId` из household. Более глубокая fertility-модель остаётся будущим балансным расширением.
- `WorldState.Initialize()` загружает дефолтный сценарий из кода; `InitializeFromJson`/`InitializeFromJsonFile` загружают сценарии из JSON. Добавлен `data/scenarios/default_scenario.json`.

## Фаза 3: Экономика MVP - готово

Срок: 1-2 недели.

- [x] Добавить производство/продажи: `baseOutput`, спрос, цена, выручка.
- [x] Привязать бизнесы к `business_types.json`.
- [x] Зарплаты брать из профессий/ролей, а не одного `WagePerEmployee`.
- [x] Добавить прибыльность, банкротство, открытие новых бизнесов.
- [x] Сделать безработицу метрикой трудоспособного населения, а не всей популяции.
- [x] Добавить налоги: income tax, business tax, расходы бюджета.

Примечания по экономике:

- `Business` хранит `ProductionType`, `BaseOutput`, `UnitPrice`, `DemandMultiplier`, последние произведённые/проданные единицы и последнюю sales revenue.
- `EconomySystem.ProcessProductionAndSales()` начисляет выручку по staffing ratio.
- `BusinessTypeCatalog` загружает типы бизнесов из `data/businesses/business_types.json`.
- `ProfessionCatalog` загружает профессии из `data/jobs/professions.json`; payroll берёт `BaseWage` по профессии и использует `Business.WagePerEmployee` как fallback.
- `BusinessStatus` поддерживает `Active`, `Bankrupt`, `Closed`; закрытые бизнесы не нанимают и не производят.
- `EconomySystem.UpdateBusinessViability()` закрывает устойчиво убыточные бизнесы и освобождает сотрудников.
- `EconomySystem.TryOpenBusiness()` создаёт новый бизнес из каталога при достаточном бюджете и низкой безработице.
- `WorldState` хранит `IncomeTaxRate`, `BusinessTaxRate`, базовые/project operating expenses и fiscal-метрики последнего тика.
- `EconomySystem.ProcessPayroll()` собирает income tax, `ProcessBusinessTaxes()` собирает налог с положительной прибыли, `ProcessGovernmentExpenses()` списывает обслуживание бюджета.

## Фаза 4: Жильё, Районы, Миграция - готово

Срок: 1 неделя.

- [x] Добавить `HousingUnit` или хотя бы `HousingCapacity` на район.
- [x] Связать `HousingSatisfaction` с доступностью жилья, арендой, overcrowding.
- [x] Миграцию делать не случайной, а по работе, жилью, безопасности, удовлетворённости.
- [x] Районы должны агрегировать население, рабочие места, жильё, безопасность, услуги.

Примечания по жилью:

- Добавлен `HousingUnit` как отдельная сущность с районом, вместимостью, арендой и привязкой к household.
- `WorldState` хранит `HousingUnits`, умеет назначать и освобождать жильё через `AssignHouseholdToHousingUnit`/`ReleaseHouseholdHousing`.
- Сценарии поддерживают секцию `housingUnits`; старые household-поля `HousingUnitId`/`HousingCapacity`/`RentPerTick` остаются совместимыми.
- `DistrictSystem.UpdateDistrictAggregates()` считает capacity/occupied/available housing и среднюю housing satisfaction.
- `NeedsSystem` теперь рассчитывает housing-дельту по фактическому household: без жилья показатель падает быстрее, overcrowding штрафует за лишних жителей, высокая аренда относительно дохода даёт rent-burden штраф, стабильное жильё слегка восстанавливает удовлетворённость.
- `DemographySystem` выбирает район для миграции через скоринг: средняя удовлетворённость, безопасность, открытые вакансии и доступное подходящее жильё. Домохозяйства переезжают вместе и занимают жильё в целевом районе; уже обеспеченные жильём household не переезжают в район без доступного жилья.
- `District` теперь хранит `TotalJobs`, `OpenJobs`, `EmploymentRate`, `AverageSafetySatisfaction`, `AverageHealthcareSatisfaction`, `AverageEntertainmentSatisfaction`, `ServiceLevel`, `ActiveProjects`, `CompletedProjects`.
- `DistrictSystem.UpdateDistrictAggregates()` пересчитывает рабочие места, занятость, безопасность, сервисный уровень и районные проекты вместе с населением, жильём и экономикой.

## Фаза 5: Государство и Игровые Решения

Срок: 1-2 недели.

- [ ] Проекты должны менять показатели района, а не просто возвращать бюджет.
- [ ] Ввести типы проектов: дороги, клиники, школы, полиция, жильё, парки.
- [ ] Добавить политические последствия: поддержка по районам, кризисы, выборы.
- [ ] Сделать события не только логом, но и игровыми развилками/решениями.

## Фаза 6: Save/Load, Сценарии, Баланс

Срок: 1 неделя.

- [ ] Сериализация `WorldState`.
- [ ] Загрузка сценариев из JSON: стартовое население, районы, бизнесы, бюджет.
- [ ] Seed-based reproducibility: один seed должен давать одинаковый прогон.
- [ ] CLI/headless runner для прогонов на 1, 10, 50 игровых лет.
- [ ] Баланс-тесты: популяция не взрывается, бюджет не уходит в бесконечность, экономика не схлопывается без причины.

## Фаза 7: Godot UI MVP

Срок: 1-2 недели.

- [ ] Подключить C# simulation assembly к Godot.
- [ ] Первый экран не меню, а рабочий дашборд города: бюджет, население, поддержка, районы, события.
- [ ] Карта районов 2D top-down с базовой визуализацией.
- [ ] Панели: район, бизнес, гражданин, проекты, события.
- [ ] Кнопки запуска проектов и управление скоростью симуляции.
- [ ] UI-переключатель языка русский/английский с использованием `LocalizationSystem`.
