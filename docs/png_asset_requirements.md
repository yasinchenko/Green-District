# PNG-ассеты для финализации UI Green District

## Цель документа

Определить минимальный набор PNG-файлов, который нужен, чтобы заменить процедурные fallback-элементы карты и подготовить UI к финальному визуальному состоянию.

Документ основан на текущем коде проекта. Сейчас PNG уже поддерживаются в первую очередь в `DistrictMapView`: карта берет `assetKey` из симуляционной модели и пробует загрузить файл из `res://assets/map/...`. Если PNG не найден, карта продолжает рисоваться примитивами.

## Где код уже умеет принимать PNG

### 1. Карта районов

Файл: `godot/scripts/DistrictMapView.cs`

Готовая точка подключения:

- `TryGetMapTexture(assetKey, out texture)`
- `MapAssetCandidates(assetKey)`

Порядок поиска PNG:

- `res://assets/map/{assetKey with dots as folders}.png`
- `res://assets/map/{assetKey}.png`

Пример:

- `business.shop` -> `res://assets/map/business/shop.png`
- `business.shop` -> `res://assets/map/business.shop.png`

Рекомендуемый формат для проекта: использовать папки, то есть `business/shop.png`, `terrain/grass.png`, `road/straight.png`.

### 2. Поверхности карты

Файл: `src/GreenDistrict.Simulation/Map/MapCell.cs`

Карта использует `SurfaceAssetKey`:

- `terrain.grass`
- `terrain.water`
- `terrain.park`
- `terrain.blocked`
- `terrain.shoreline` через `MapGrid.GetSurfaceAssetKey()` для земли рядом с водой.

В `DistrictMapView.DrawGridSurfaces()` эти текстуры рисуются с `tile: true`, поэтому они должны быть бесшовными.

### 3. Дороги и мосты

Файлы:

- `src/GreenDistrict.Simulation/Map/MapCell.cs`
- `src/GreenDistrict.Simulation/Map/RoadNetwork.cs`

Карта использует `RoadAssetKey`:

- `road.end`
- `road.straight`
- `road.turn`
- `road.tjunction`
- `road.cross`
- `road.isolated`
- `bridge.end`
- `bridge.straight`
- `bridge.turn`
- `bridge.tjunction`
- `bridge.cross`
- `bridge.isolated`

В `DistrictMapView.DrawGridRoadCell()` дорожные PNG рисуются по одной клетке карты без тайлинга. Сейчас код не передает ориентацию в PNG и не вращает текстуру, поэтому на MVP лучше делать тайлы визуально терпимыми без направления или доработать поворот тайла отдельной задачей.

### 4. Здания, бизнесы, проекты и маркеры

Файлы:

- `data/config/map_object_sizes.json`
- `src/GreenDistrict.Simulation/Map/MapObjectSizeCatalog.cs`
- `src/GreenDistrict.Simulation/Map/MapGridGenerator.cs`

Объекты получают `assetKey` из каталога размеров. PNG рисуется в прямоугольник footprint-объекта через `DrawTextureRect()`.

Текущие ключи:

- `building.house.small`
- `building.house.medium`
- `business.shop`
- `business.workshop`
- `business.farm`
- `service.clinic`
- `service.school`
- `service.police`
- `park.small`
- `marker.event`
- `marker.crisis`
- `marker.decision`

### 5. Панели интерфейса

Файлы:

- `godot/scripts/MainDashboard.cs`
- `godot/scripts/UiTheme.cs`

Сейчас панели используют текстовые placeholder-иконки (`"P"`, `"$"`, `"!"`, `"H"`, `"S"` и т.п.) через `UiTheme.Icon()`. Прямой загрузки PNG для HUD, карточек, кнопок и issue cards пока нет.

Вывод: для MVP финализации без дополнительного UI-кода PNG нужно делать прежде всего для карты. PNG-иконки интерфейса можно подготовить как P1, но для их использования понадобится небольшой `UiIcon`/`TextureRect` слой.

## Минимальный P0-набор PNG

Папка назначения: `godot/assets/map`.

### Поверхности

| Файл | Asset key | Назначение | Рекомендация |
|---|---|---|---|
| `terrain/grass.png` | `terrain.grass` | базовая земля | бесшовный тайл 64x64 |
| `terrain/water.png` | `terrain.water` | вода | бесшовный тайл 64x64 |
| `terrain/shoreline.png` | `terrain.shoreline` | берег рядом с водой | бесшовный тайл 64x64 |
| `terrain/park.png` | `terrain.park` | зеленая зона | бесшовный тайл 64x64 |
| `terrain/blocked.png` | `terrain.blocked` | заблокированная/недоступная клетка | бесшовный тайл 64x64 |

### Дороги

| Файл | Asset key | Назначение | Рекомендация |
|---|---|---|---|
| `road/isolated.png` | `road.isolated` | одиночная дорожная клетка | 64x64, прозрачные края |
| `road/end.png` | `road.end` | конец дороги | 64x64 |
| `road/straight.png` | `road.straight` | прямая дорога | 64x64 |
| `road/turn.png` | `road.turn` | поворот | 64x64 |
| `road/tjunction.png` | `road.tjunction` | T-перекресток | 64x64 |
| `road/cross.png` | `road.cross` | перекресток | 64x64 |

### Мосты

| Файл | Asset key | Назначение | Рекомендация |
|---|---|---|---|
| `bridge/isolated.png` | `bridge.isolated` | одиночная клетка моста | 64x64 |
| `bridge/end.png` | `bridge.end` | конец моста | 64x64 |
| `bridge/straight.png` | `bridge.straight` | прямой мост | 64x64 |
| `bridge/turn.png` | `bridge.turn` | поворот моста | 64x64 |
| `bridge/tjunction.png` | `bridge.tjunction` | T-соединение моста | 64x64 |
| `bridge/cross.png` | `bridge.cross` | пересечение моста | 64x64 |

### Здания и объекты

| Файл | Asset key | Назначение | Footprint в метрах |
|---|---|---|---|
| `building/house/small.png` | `building.house.small` | малый дом | 8x10 |
| `building/house/medium.png` | `building.house.medium` | средний дом/жилой блок | 10x14 |
| `business/shop.png` | `business.shop` | магазин/рынок | 12x16 |
| `business/workshop.png` | `business.workshop` | мастерская/производство | 18x24 |
| `business/farm.png` | `business.farm` | ферма/еда | 24x18 |
| `service/clinic.png` | `service.clinic` | клиника | 20x28 |
| `service/school.png` | `service.school` | школа | 35x45 |
| `service/police.png` | `service.police` | полиция/безопасность | 18x22 |
| `park/small.png` | `park.small` | парк | 24x24 |

Рекомендация: делать PNG с прозрачным фоном, изометрическим или top-down стилем, но сохранять читаемость в маленьком размере. Карта сама растягивает PNG под footprint объекта.

### Маркеры событий

| Файл | Asset key | Назначение | Рекомендация |
|---|---|---|---|
| `marker/event.png` | `marker.event` | обычное событие | 64x64, прозрачный фон |
| `marker/crisis.png` | `marker.crisis` | кризис | 64x64, высокий контраст |
| `marker/decision.png` | `marker.decision` | событие с выбором | 64x64 |

## Итого P0

Минимальный набор для финальной карты:

- 5 surface PNG;
- 6 road PNG;
- 6 bridge PNG;
- 9 object PNG;
- 3 marker PNG.

Всего: 29 PNG.

## P1-набор для интерфейса вне карты

Этот набор полезен для финального UI, но текущий код еще не загружает эти PNG автоматически. Перед использованием нужно добавить небольшой слой `UiIcon`, который будет пробовать загрузить PNG и падать обратно на текстовую иконку.

Рекомендуемая папка: `godot/assets/ui/icons`.

Минимальные UI-иконки:

- `hud/population.png`
- `hud/budget.png`
- `hud/support.png`
- `hud/satisfaction.png`
- `hud/time.png`
- `project/road.png`
- `project/clinic.png`
- `project/school.png`
- `project/police.png`
- `project/housing.png`
- `project/park.png`
- `event/crisis.png`
- `event/decision.png`
- `event/economic.png`
- `event/election.png`
- `event/political.png`
- `event/social.png`
- `issue/housing.png`
- `issue/jobs.png`
- `issue/safety.png`
- `issue/services.png`
- `issue/economy.png`
- `action/save.png`
- `action/load.png`
- `action/pause.png`
- `action/play.png`
- `action/settings.png`

Рекомендуемый размер: 32x32 или 48x48, прозрачный фон.

## Что не нужно делать для MVP

- Портреты жителей.
- Большие иллюстрации событий.
- Отдельные PNG для каждого конкретного бизнеса из сценария.
- Анимированные ассеты.
- Полная замена всех текстовых иконок в dashboard до появления `UiIcon`.

## Рекомендуемый порядок внедрения

1. Добавить P0 PNG в `godot/assets/map`.
2. Проверить карту в 1152x648, 1280x720 и 1920x1080.
3. Если дорожные тайлы выглядят неправильно из-за ориентации, доработать `DrawGridRoadCell()` так, чтобы он вращал `road.straight`, `road.end`, `road.turn` и `road.tjunction` по `RoadConnections`.
4. После стабилизации карты добавить `UiIcon` для P1 UI-иконок.
5. Провести финальный visual pass RU/EN.

