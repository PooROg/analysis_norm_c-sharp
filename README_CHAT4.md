# 📘 README Чат 4: Анализатор данных + Графики

## 🎯 Что реализовано в Чате 4

### Основные компоненты:
1. ✅ **DataAnalyzer.cs** - анализ данных маршрутов с интерполяцией норм
2. ✅ **InteractiveAnalyzer.cs** - координатор всех компонентов анализа
3. ✅ **PlotBuilder.cs** - построение графиков (ScottPlot вместо Plotly)
4. ✅ **VisualizationSection.cs** - GUI секция для отображения графиков
5. ✅ **MainWindow.cs** - обновлен с полной интеграцией анализа

### Прогресс миграции:
```
████████████████████████████████████████████████░░░░░░░░░░░░ 55%
```
- Чат 1: 10% ✅
- Чат 2: 25% ✅
- Чат 3: 40% ✅
- **Чат 4: 55%** ✅ ← **ТЕКУЩИЙ**

---

## 📦 Новые NuGet пакеты

### Установка ScottPlot:
```bash
cd analysis_norm_c-sharp
dotnet add package ScottPlot
dotnet add package ScottPlot.WinForms
```

### Все пакеты для Чата 4:
```bash
# Из предыдущих чатов
dotnet add package Serilog
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Newtonsoft.Json
dotnet add package MathNet.Numerics
dotnet add package ClosedXML
dotnet add package HtmlAgilityPack
dotnet add package Microsoft.Data.Analysis
dotnet add package System.Text.Encoding.CodePages

# ЧАТ 4: Новые
dotnet add package ScottPlot
dotnet add package ScottPlot.WinForms
```

---

## 🚀 Быстрый старт

### Шаг 1: Копирование файлов

```bash
# Создать директории
mkdir -p analysis_norm_c-sharp/Analysis
mkdir -p analysis_norm_c-sharp/GUI/Components
mkdir -p analysis_norm_c-sharp/docs/chat4

# Скопировать новые файлы Чата 4
cp /home/claude/Analysis/DataAnalyzer.cs analysis_norm_c-sharp/Analysis/
cp /home/claude/Analysis/InteractiveAnalyzer.cs analysis_norm_c-sharp/Analysis/
cp /home/claude/Analysis/PlotBuilder.cs analysis_norm_c-sharp/Analysis/
cp /home/claude/GUI/Components/VisualizationSection.cs analysis_norm_c-sharp/GUI/Components/
cp /home/claude/GUI/MainWindow_Chat4.cs analysis_norm_c-sharp/GUI/MainWindow.cs

# Скопировать документацию
cp /home/claude/README_CHAT4.md analysis_norm_c-sharp/
```

### Шаг 2: Обновление ControlSection.cs

Добавьте следующие методы в конец класса `ControlSection` (файл `GUI/Components/ControlSection.cs`):

```csharp
/// <summary>
/// Возвращает выбранную норму (или null если не выбрана)
/// ЧАТ 4: Новый метод
/// </summary>
public string? GetSelectedNorm()
{
    if (_normComboBox.Enabled && _normComboBox.SelectedIndex >= 0)
    {
        return _normComboBox.SelectedItem?.ToString();
    }
    return null;
}

/// <summary>
/// Возвращает выбранный участок (или null если не выбран)
/// ЧАТ 4: Новый метод
/// </summary>
public string? GetSelectedSection()
{
    if (_sectionComboBox.Enabled && _sectionComboBox.SelectedIndex >= 0)
    {
        return _sectionComboBox.SelectedItem?.ToString();
    }
    return null;
}
```

### Шаг 3: Установка пакетов

```bash
cd analysis_norm_c-sharp
dotnet add package ScottPlot
dotnet add package ScottPlot.WinForms
dotnet restore
```

### Шаг 4: Компиляция и запуск

```bash
dotnet build
dotnet run
```

---

## 🎮 Что работает сейчас

### Полный цикл анализа:

1. **Загрузка маршрутов** (Чат 3)
   - Выбор HTML файлов маршрутов
   - Парсинг и создание DataFrame
   - Отображение списка участков

2. **Загрузка норм** (Чат 3)
   - Выбор HTML файлов норм
   - Парсинг и сохранение в NormStorage
   - Интерполяция норм

3. **✨ НОВОЕ: Анализ участка** (Чат 4)
   - Выбор участка из списка
   - Опциональный выбор конкретной нормы
   - Режим "Только один участок"
   - Нажатие кнопки "Анализировать"

4. **✨ НОВОЕ: Визуализация** (Чат 4)
   - Построение графика с кривыми норм
   - Отображение точек маршрутов (Экономия/Норма/Перерасход)
   - Интерактивность ScottPlot (zoom, pan)
   - Отображение статистики

5. **✨ НОВОЕ: Экспорт графика** (Чат 4)
   - Экспорт графика в PNG
   - Сохранение с выбором пути

---

## 📊 Архитектура Чата 4

### Flow анализа:

```
1. Пользователь нажимает "Анализировать"
   ↓
2. ControlSection → OnAnalyzeRequested event
   ↓
3. MainWindow.ControlSection_OnAnalyzeRequested()
   ↓
4. InteractiveAnalyzer.AnalyzeSection()
   ├─→ DataAnalyzer.AnalyzeSectionData()
   │    ├─→ NormStorage.GetNormFunction()
   │    └─→ Интерполяция и расчет отклонений
   └─→ PlotBuilder.CreateInteractivePlot()
        └─→ ScottPlot graph
   ↓
5. MainWindow отображает результат:
   ├─→ VisualizationSection.DisplayPlot(plot)
   ├─→ VisualizationSection.DisplayStatistics(stats)
   └─→ ControlSection.UpdateStatistics(stats)
```

### Взаимодействие компонентов:

```
InteractiveAnalyzer (координатор)
    │
    ├─→ RouteProcessor (из Чата 3)
    ├─→ NormProcessor (из Чата 3)
    ├─→ NormStorage (из Чата 2)
    ├─→ DataAnalyzer (новый)
    │    └─→ RouteDataAnalyzer
    │         └─→ Интерполяция норм
    └─→ PlotBuilder (новый)
         └─→ ScottPlot
```

---

## 🔍 Детальное описание компонентов

### 1. DataAnalyzer.cs (~350 строк)

**Ключевые методы:**
- `AnalyzeSectionData()` - главный метод анализа
- `CreateNormFunctions()` - создает функции интерполяции
- `InterpolateAndCalculate()` - интерполирует нормы и рассчитывает отклонения
- `CalculateStatistics()` - статистика по результатам

**Что делает:**
- Получает функции норм из NormStorage
- Интерполирует нормы для каждой точки маршрута
- Рассчитывает отклонения (%)
- Классифицирует статусы (Экономия/Норма/Перерасход)

### 2. InteractiveAnalyzer.cs (~450 строк)

**Ключевые методы:**
- `LoadRoutesFromHtml()` - загрузка маршрутов
- `LoadNormsFromHtml()` - загрузка норм
- `AnalyzeSection()` - анализ участка
- `BuildSectionsNormsMap()` - карта участков → нормы
- `GetAvailableSections()` - список участков

**Что делает:**
- Координирует все компоненты
- Строит карты участков и норм
- Выполняет анализ с вызовом DataAnalyzer и PlotBuilder
- Сохраняет результаты анализа

### 3. PlotBuilder.cs (~400 строк)

**Ключевые методы:**
- `CreateInteractivePlot()` - создание графика
- `AddNormCurves()` - добавление кривых норм
- `AddRoutePoints()` - добавление точек маршрутов
- `GroupPointsByStatus()` - группировка по статусам

**Что делает:**
- Создает график ScottPlot
- Отображает кривые норм (интерполяция)
- Отображает точки маршрутов с цветовым кодированием
- Настраивает легенду и оси

**Отличия от Python (Plotly):**
- Python использует Plotly (интерактивный HTML)
- C# использует ScottPlot (WinForms контрол)
- Базовая интерактивность (zoom, pan) встроена в ScottPlot
- Модальные окна по клику → будет в Чате 6

### 4. VisualizationSection.cs (~250 строк)

**Ключевые методы:**
- `DisplayPlot()` - отображает график
- `DisplayStatistics()` - отображает статистику
- `ExportButton_Click()` - экспорт в PNG

**Что делает:**
- Содержит FormsPlot контрол
- Отображает график от PlotBuilder
- Показывает статистику в TextBox
- Экспорт графика

### 5. MainWindow.cs (обновлен, ~650 строк)

**Ключевые изменения:**
- Добавлено поле `_analyzer` (InteractiveAnalyzer)
- Добавлено поле `_plotBuilder` (PlotBuilder)
- Добавлено поле `_visualizationSection` (GUI)
- `ControlSection_OnAnalyzeRequested()` - ПОЛНАЯ РЕАЛИЗАЦИЯ анализа
- Обновлены `FileSection_OnRoutesLoaded()` и `FileSection_OnNormsLoaded()` для использования `_analyzer`

---

## 🎨 ScottPlot vs Plotly

### Почему ScottPlot?

| Критерий | Plotly (Python) | ScottPlot (C#) | Выбор |
|----------|----------------|----------------|-------|
| Платформа | Web (HTML) | WinForms/WPF | ✅ ScottPlot |
| Интеграция | Сложная | Нативная | ✅ ScottPlot |
| Производительность | Средняя | Высокая | ✅ ScottPlot |
| Интерактивность | Высокая | Средняя | ⚠️ Plotly |
| Простота | Сложная | Простая | ✅ ScottPlot |

**Решение:** ScottPlot проще в интеграции и достаточен для Чата 4. Расширенная интерактивность будет добавлена в Чате 6.

---

## 🧪 Тестирование

### Сценарий 1: Полный цикл анализа

1. Запустить приложение
2. Загрузить HTML маршруты (2-3 файла)
3. Загрузить HTML нормы (2-3 файла)
4. Выбрать участок из списка
5. (Опционально) Выбрать конкретную норму
6. Нажать "Анализировать"
7. **Ожидается:**
   - График отображается в правой секции
   - Кривые норм (синяя, фиолетовая и т.д.)
   - Точки маршрутов (зеленые/оранжевые/красные)
   - Статистика в центральной и правой секциях
   - Кнопка "Экспорт PNG" активна

### Сценарий 2: Экспорт графика

1. После успешного анализа
2. Нажать "Экспорт PNG"
3. Выбрать путь сохранения
4. **Ожидается:**
   - Файл PNG создан (1920x1080)
   - Сообщение об успехе

### Сценарий 3: Режим "Только один участок"

1. Загрузить данные
2. Выбрать участок
3. Включить чекбокс "Только один участок"
4. Нажать "Анализировать"
5. **Ожидается:**
   - Отфильтрованы только маршруты с одним участком
   - График и статистика обновлены

---

## ⚠️ Известные ограничения Чата 4

### Что НЕ реализовано (будет в следующих чатах):

1. **Фильтр локомотивов** (Чат 5)
   - Кнопка "Фильтр локомотивов" - заглушка
   - LocomotiveSelectorDialog

2. **Применение коэффициентов** (Чат 5)
   - CoefficientsApplier - заглушка
   - Кнопка "Загрузить коэффициенты" работает частично

3. **Модальные окна по клику на точку** (Чат 6)
   - Python: click → modal window с деталями
   - C#: будет реализовано через события ScottPlot

4. **Экспорт в Excel** (Чат 7)
   - RouteProcessor.ExportToExcel() - TODO

5. **Управление нормами** (Чат 7)
   - NormManagementDialog - TODO

---

## 📈 Статистика Чата 4

### Новые файлы:
| Файл | Строк | Функций | Статус |
|------|-------|---------|--------|
| DataAnalyzer.cs | ~350 | 8 | ✅ 100% |
| InteractiveAnalyzer.cs | ~450 | 12 | ✅ 100% |
| PlotBuilder.cs | ~400 | 10 | ✅ 100% |
| VisualizationSection.cs | ~250 | 8 | ✅ 100% |
| MainWindow (обновлен) | +150 | +3 | ✅ 100% |
| **ИТОГО** | **~1600** | **~41** | **✅ 100%** |

### Общий прогресс:
- Чат 1: 865 строк (10%) ✅
- Чат 2: 1955 строк (25%) ✅
- Чат 3: 2000 строк (40%) ✅
- **Чат 4: 1600 строк (55%)** ✅ ← **ТЕКУЩИЙ**
- **Итого: 6420 строк**

---

## 🎯 Следующие шаги (Чат 5)

### Что будет реализовано:
1. **LocomotiveSelectorDialog** - диалог выбора локомотивов
2. **CoefficientsApplier** - применение коэффициентов к данным
3. **LogSection** - секция логов в GUI
4. **Интеграция фильтра** - полная функциональность фильтрации
5. **Улучшения MainWindow** - меню и прогресс-бары

---

## 🐛 Решение проблем

### Ошибка: "ScottPlot not found"
```bash
dotnet add package ScottPlot
dotnet add package ScottPlot.WinForms
dotnet restore
```

### Ошибка: "GetSelectedNorm not found"
Проверьте что добавили методы `GetSelectedNorm()` и `GetSelectedSection()` в `ControlSection.cs`

### График не отображается
1. Проверьте логи: `logs/app.log`
2. Проверьте что маршруты и нормы загружены
3. Проверьте что участок выбран в dropdown

### Ошибка компиляции MainWindow.cs
Проверьте using директивы:
```csharp
using ScottPlot;
using AnalysisNorm.Analysis;
```

---

## 📚 Документация

- **README_CHAT4.md** (этот файл) - общий обзор
- **MIGRATION_LOG.md** - детальный лог миграции
- XML комментарии в коде
- Референсы на Python в комментариях

---

## 🎊 Чат 4 завершен на 100%!

**Ключевое достижение:** Полный анализ участка с графиком работает!

**Готовность к Чату 5:** ✅ Полная

---

**Дата:** 2025-10-05  
**Статус:** ✅ Чат 4 завершен  
**Строк кода:** ~6420 (суммарно)  
**Прогресс:** 55%
