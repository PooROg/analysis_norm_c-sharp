# Migration Log: analysis_norm_python → AnalysisNorm (C#)

## Прогресс: 25% (Чат 2 из 8)

---

## Чат 2 - 2025-01-XX

### ✅ Реализовано полностью

| Python функция/класс | Расположение Python | C# метод/класс | Расположение C# | Статус |
|---------------------|-------------------|---------------|----------------|--------|
| `NormStorage` | norm_storage.py:1-400 | `NormStorage` | Core/NormStorage.cs | ✅ Полная |
| `NormStorage.load_storage()` | norm_storage.py:32 | `NormStorage.LoadStorage()` | Core/NormStorage.cs:112 | ✅ Полная |
| `NormStorage.save_storage()` | norm_storage.py:50 | `NormStorage.SaveStorage()` | Core/NormStorage.cs:138 | ✅ Полная |
| `NormStorage.add_or_update_norms()` | norm_storage.py:75 | `NormStorage.AddOrUpdateNorms()` | Core/NormStorage.cs:194 | ✅ Полная |
| `NormStorage._create_interpolation_function()` | norm_storage.py:120 | `NormStorage.CreateInterpolationFunction()` | Core/NormStorage.cs:273 | ✅ Полная |
| `NormStorage.get_norm_function()` | norm_storage.py:200 | `NormStorage.GetNormFunction()` | Core/NormStorage.cs:414 | ✅ Полная |
| `NormStorage.interpolate_norm_value()` | norm_storage.py:210 | `NormStorage.InterpolateNormValue()` | Core/NormStorage.cs:435 | ✅ Полная |
| `NormStorage.search_norms()` | norm_storage.py:240 | `NormStorage.SearchNorms()` | Core/NormStorage.cs:482 | ✅ Полная |
| `NormStorage.validate_norms()` | norm_storage.py:280 | `NormStorage.ValidateNorms()` | Core/NormStorage.cs:545 | ✅ Полная |
| `NormStorage.get_storage_info()` | norm_storage.py:320 | `NormStorage.GetStorageInfo()` | Core/NormStorage.cs:611 | ✅ Полная |
| `LocomotiveCoefficientsManager` | coefficients.py:1-180 | `CoefficientsManager` | Core/CoefficientsManager.cs | ✅ Полная |
| `LocomotiveCoefficientsManager.normalize_series()` | coefficients.py:28 | `CoefficientsManager.NormalizeSeries()` | Core/CoefficientsManager.cs:69 | ✅ Полная |
| `LocomotiveCoefficientsManager.load_coefficients()` | coefficients.py:35 | `CoefficientsManager.LoadCoefficients()` | Core/CoefficientsManager.cs:91 | ✅ Полная |
| `LocomotiveCoefficientsManager.get_coefficient()` | coefficients.py:160 | `CoefficientsManager.GetCoefficient()` | Core/CoefficientsManager.cs:373 | ✅ Полная |
| `LocomotiveCoefficientsManager.get_statistics()` | coefficients.py:170 | `CoefficientsManager.GetStatistics()` | Core/CoefficientsManager.cs:401 | ✅ Полная |
| `LocomotiveFilter.__init__()` | filter.py:20 | `LocomotiveFilter (constructor)` | Core/LocomotiveFilter.cs:53 | ⚠️ Заглушка |
| `LocomotiveFilter.get_locomotives_by_series()` | filter.py:105 | `LocomotiveFilter.GetLocomotivesBySeries()` | Core/LocomotiveFilter.cs:132 | ✅ Полная |
| `LocomotiveFilter.select_all()` | filter.py:115 | `LocomotiveFilter.SelectAll()` | Core/LocomotiveFilter.cs:172 | ✅ Полная |
| `LocomotiveFilter.filter_routes()` | filter.py:130 | `LocomotiveFilter.FilterRoutes()` | Core/LocomotiveFilter.cs:240 | ⚠️ Заглушка |
| `FileSection` | components.py:35-150 | `FileSection` | GUI/Components/FileSection.cs | ✅ Полная |
| `FileSection._select_files()` | components.py:70 | `FileSection.SelectFiles()` | GUI/Components/FileSection.cs:205 | ✅ Полная |
| `FileSection._load_routes()` | components.py:115 | `FileSection.LoadRoutesButton_Click()` | GUI/Components/FileSection.cs:298 | ⚠️ Заглушка |
| `FileSection._load_norms()` | components.py:125 | `FileSection.LoadNormsButton_Click()` | GUI/Components/FileSection.cs:323 | ⚠️ Заглушка |
| `FileSection.update_status()` | components.py:135 | `FileSection.UpdateStatus()` | GUI/Components/FileSection.cs:373 | ✅ Полная |

### 📁 Созданные файлы

- **Core/NormStorage.cs** (650 строк) - Хранилище норм с гиперболической интерполяцией
  - JSON сериализация вместо pickle
  - Гиперболическая интерполяция через МНК (1 точка → константа, 2+ → гипербола)
  - Кэш функций интерполяции
  - Валидация норм
  
- **Core/CoefficientsManager.cs** (420 строк) - Менеджер коэффициентов локомотивов
  - Загрузка из Excel (все листы = серии)
  - Нормализация названий серий
  - Поддержка двух источников: "Коэффициент" и "Процент"
  - Фильтрация по минимальной работе
  - Статистика отклонений

- **Core/LocomotiveFilter.cs** (280 строк) - Фильтр локомотивов (упрощенная версия)
  - 4 режима работы (Standard, Guess, Depot, Minimal)
  - Группировка по сериям
  - Управление выбором (SelectAll, DeselectAll, InvertSelection)
  - ⚠️ Полная фильтрация DataFrame будет в Чате 3

- **GUI/Components/FileSection.cs** (400 строк) - Секция выбора файлов
  - Выбор HTML маршрутов (множественный выбор)
  - Выбор HTML норм (множественный выбор)
  - Выбор Excel коэффициентов
  - Кнопки загрузки с активацией после выбора
  - Статус загрузки с цветовой индикацией
  - ⚠️ Реальная загрузка HTML будет в Чате 3

- **INSTALL_NUGET_CHAT2.md** - Инструкция по установке NuGet пакетов

### 📦 NuGet пакеты добавлены

| Пакет | Версия | Назначение | Используется в |
|-------|--------|-----------|---------------|
| MathNet.Numerics | Latest | Гиперболическая интерполяция (замена scipy) | Core/NormStorage.cs |
| ClosedXML | Latest | Чтение Excel файлов (замена openpyxl) | Core/CoefficientsManager.cs |

### 🎨 GUI состояние

**Реализовано:**
- ✅ FileSection с тремя группами файлов:
  - HTML маршруты (множественный выбор)
  - HTML нормы (множественный выбор)
  - Excel коэффициенты (одиночный выбор)
- ✅ Кнопки "Выбрать файлы" / "Очистить"
- ✅ Кнопки "Загрузить" (активируются после выбора)
- ✅ Статус загрузки (зеленый/красный/оранжевый)

**Интеграция с MainWindow:**
- ⚠️ FileSection нужно добавить в левую панель MainWindow (TODO в следующем коммите)

### ⚠️ Заглушки для следующих чатов

| Компонент | Метод | Реализация | Чат |
|-----------|-------|-----------|-----|
| `LocomotiveFilter` | `ExtractLocomotivesFromData()` | Полный парсинг DataFrame | Чат 3 |
| `LocomotiveFilter` | `FilterRoutes()` | Векторизованная фильтрация | Чат 3 |
| `FileSection` | `LoadRoutesButton_Click()` | Вызов RouteProcessor | Чат 3 |
| `FileSection` | `LoadNormsButton_Click()` | Вызов NormProcessor | Чат 3 |

### 📝 Примечания

**Особенности реализации:**

1. **NormStorage.cs:**
   - JSON вместо pickle (более переносимо между платформами)
   - МНК вместо scipy.optimize.curve_fit
   - Делегаты `Func<double, double>` вместо Python функций
   - Кэширование в объектах `NormData.InterpolationFunction`

2. **CoefficientsManager.cs:**
   - ClosedXML вместо pandas.read_excel()
   - Парсинг заголовков через поиск подстрок
   - Поддержка двух источников коэффициентов: "Коэффициент" (как есть) и "Процент" (с делением на 100 если >10)

3. **LocomotiveFilter.cs:**
   - Упрощенная версия без DataFrame (пока)
   - Использует простые списки и HashSet
   - TODO Чат 3: Полная интеграция с Microsoft.Data.Analysis

4. **FileSection.cs:**
   - WinForms TableLayoutPanel вместо tkinter grid
   - События (events) вместо Python колл

бэков
   - FlowLayoutPanel для кнопок загрузки

**Отличия от Python:**
- `pickle` → JSON (`Newtonsoft.Json`)
- `scipy.optimize.curve_fit` → Ручной МНК (Math.NET)
- `pandas.read_excel` → ClosedXML
- `tkinter` → WinForms

**Валидация:**
- ✅ Проект компилируется (после установки NuGet пакетов)
- ✅ NormStorage работает (создание/загрузка/интерполяция)
- ✅ CoefficientsManager читает Excel
- ✅ FileSection функционален (выбор/отображение файлов)
- ⚠️ Интеграция в MainWindow - следующий шаг

---

## Чат 1 - 2025-01-XX

### ✅ Реализовано полностью

| Python функция/класс | Расположение Python | C# метод/класс | Расположение C# | Статус |
|---------------------|-------------------|---------------|----------------|--------|
| `AppConfig` | config.py:10-60 | `AppConfig` | Core/AppConfig.cs | ✅ Полная |
| `AppConfig.create_default()` | config.py:42 | `AppConfig.CreateDefault()` | Core/AppConfig.cs:60 | ✅ Полная |
| `AppConfig.ensure_directories()` | config.py:51 | `AppConfig.EnsureDirectories()` | Core/AppConfig.cs:79 | ✅ Полная |
| `normalize_text()` | utils.py:20 | `TextUtils.NormalizeText()` | Utils/CommonUtils.cs:23 | ✅ Полная |
| `safe_float()` | utils.py:28 | `TextUtils.SafeFloat()` | Utils/CommonUtils.cs:37 | ✅ Полная |
| `safe_int()` | utils.py:65 | `TextUtils.SafeInt()` | Utils/CommonUtils.cs:99 | ✅ Полная |
| `safe_divide()` | utils.py:85 | `TextUtils.SafeDivide()` | Utils/CommonUtils.cs:113 | ✅ Полная |
| `format_number()` | utils.py:100 | `TextUtils.FormatNumber()` | Utils/CommonUtils.cs:132 | ✅ Полная |
| `StatusClassifier` | utils.py:120-175 | `StatusClassifier` | Utils/CommonUtils.cs:161-230 | ✅ Полная |
| `StatusClassifier.get_status()` | utils.py:125 | `StatusClassifier.GetStatus()` | Utils/CommonUtils.cs:181 | ✅ Полная |
| `StatusClassifier.get_status_color()` | utils.py:150 | `StatusClassifier.GetStatusColor()` | Utils/CommonUtils.cs:210 | ✅ Полная |
| `NormsAnalyzerGUI.__init__()` | interface.py:30-80 | `MainWindow (constructor)` | GUI/MainWindow.cs:25-40 | ✅ Полная |
| `NormsAnalyzerGUI._setup_gui()` | interface.py:45-150 | `MainWindow.SetupUI()` | GUI/MainWindow.cs:48-110 | ✅ Полная |
| `setup_logging()` | main.py:20-45 | `Program.SetupLogging()` | Program.cs:60-75 | ✅ Полная |
| `main()` | main.py:80-120 | `Program.Main()` | Program.cs:25-55 | ✅ Полная |

### 📁 Созданные файлы (Чат 1)

- **AnalysisNorm.sln** - Solution файл
- **Core/AppConfig.cs** (100 строк)
- **Utils/CommonUtils.cs** (230 строк)
- **GUI/MainWindow.cs** (340 строк)
- **GUI/MainWindow.Designer.cs** (45 строк)
- **Program.cs** (150 строк)
- **MIGRATION_LOG.md**

### 📦 NuGet пакеты (Чат 1)

| Пакет | Версия | Назначение |
|-------|--------|-----------|
| Serilog | 3.1.1 | Логирование |
| Serilog.Sinks.File | 5.0.0 | Запись в файлы |
| Serilog.Sinks.Console | 5.0.1 | Вывод в консоль |
| Newtonsoft.Json | 13.0.3 | Работа с JSON |

---

## Следующий: Чат 3 - HTML парсинг + ControlSection

### Планируется реализовать:

**Analysis модули:**
- `Analysis/HtmlProcessorBase.cs` - Базовые утилиты HTML
- `Analysis/RouteProcessor.cs` - Парсинг HTML маршрутов (~1800 строк Python!)
- `Analysis/NormProcessor.cs` - Парсинг HTML норм

**GUI компоненты:**
- `GUI/Components/ControlSection.cs` - Секция управления анализом
  - Выпадающий список участков
  - Выпадающий список норм
  - Чекбокс "Только один участок"
  - Кнопки "Анализировать", "Фильтр локомотивов"

**Интеграция:**
- FileSection → RouteProcessor → ControlSection (список участков)
- FileSection → NormProcessor → NormStorage

**NuGet зависимости:**
- HtmlAgilityPack (парсинг HTML)
- Microsoft.Data.Analysis (аналог pandas DataFrame)

**Критерии готовности Чата 3:**
- ✅ HTML маршруты парсятся корректно
- ✅ HTML нормы загружаются в NormStorage
- ✅ ControlSection отображает список участков
- ✅ Компилируется и запускается

---

## Блокеры и вопросы

**Текущие блокеры:** Нет

**Вопросы для следующего чата:**
1. Использовать Microsoft.Data.Analysis или свою реализацию DataFrame?
2. Нужно ли реализовать полностью сложные регулярки парсинга маршрутов в Чате 3, или упростить?

---

## Общая статистика

- **Всего чатов:** 8 запланировано
- **Завершено:** 2 (25%)
- **Строк C# кода:** ~2750 (было 865)
- **Файлов создано:** 12 (было 7)
- **NuGet пакетов:** 6 (было 4)

---

## Комментарии к миграции

### Успешно мигрировано:
- ✅ Гиперболическая интерполяция (scipy → МНК вручную)
- ✅ Загрузка Excel (pandas → ClosedXML)
- ✅ Хранение данных (pickle → JSON)
- ✅ GUI файловая секция (tkinter → WinForms)

### Требует доработки в следующих чатах:
- ⚠️ Фильтрация DataFrame (Чат 3)
- ⚠️ HTML парсинг (Чат 3)
- ⚠️ Интеграция FileSection в MainWindow (сейчас)
