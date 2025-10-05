# Интеграция FileSection в MainWindow

## Задача
Заменить placeholder панель "Секция выбора файлов" на реальный компонент `FileSection`.

## Шаги интеграции

### 1. Добавить using в MainWindow.cs

```csharp
using AnalysisNorm.GUI.Components;
using AnalysisNorm.Core;
```

### 2. Добавить поле в MainWindow

```csharp
public partial class MainWindow : Form
{
    private FileSection _fileSection;  // <-- ДОБАВИТЬ
    
    // ... остальные поля
```

### 3. Заменить placeholder в SetupUI()

Найти в методе `SetupUI()` код создания левой панели:

```csharp
// СТАРЫЙ КОД (удалить):
var fileSectionPlaceholder = new Label
{
    Text = "СЕКЦИЯ ВЫБОРА ФАЙЛОВ\n\n" +
           "Здесь будут кнопки:\n" +
           "- Загрузить HTML маршрутов\n" +
           "- Загрузить HTML норм\n" +
           "- Загрузить коэффициенты",
    Dock = DockStyle.Fill,
    TextAlign = ContentAlignment.MiddleCenter,
    BackColor = Color.LightBlue
};
```

Заменить на:

```csharp
// НОВЫЙ КОД:
_fileSection = new FileSection
{
    Dock = DockStyle.Fill
};

// Подписываемся на события
_fileSection.OnRoutesLoaded += FileSection_OnRoutesLoaded;
_fileSection.OnNormsLoaded += FileSection_OnNormsLoaded;
_fileSection.OnCoefficientsLoaded += FileSection_OnCoefficientsLoaded;
```

### 4. Добавить обработчики событий

Добавить в конец класса `MainWindow`:

```csharp
#region Обработчики событий FileSection

private void FileSection_OnRoutesLoaded(List<string> files)
{
    Log.Information("MainWindow: Получены файлы маршрутов: {Count}", files.Count);
    UpdateStatusBar($"Выбрано файлов маршрутов: {files.Count}");
    
    // TODO Чат 3: Вызвать RouteProcessor для парсинга HTML
}

private void FileSection_OnNormsLoaded(List<string> files)
{
    Log.Information("MainWindow: Получены файлы норм: {Count}", files.Count);
    UpdateStatusBar($"Выбрано файлов норм: {files.Count}");
    
    // TODO Чат 3: Вызвать NormProcessor для парсинга HTML
}

private void FileSection_OnCoefficientsLoaded(string filePath)
{
    Log.Information("MainWindow: Получен файл коэффициентов: {File}", Path.GetFileName(filePath));
    UpdateStatusBar($"Выбран файл коэффициентов: {Path.GetFileName(filePath)}");
    
    // TODO Чат 5: Открыть диалог селектора локомотивов
}

#endregion
```

### 5. Обновить TableLayoutPanel

Заменить добавление placeholder:

```csharp
// СТАРЫЙ КОД (удалить):
mainLayout.Controls.Add(fileSectionPlaceholder, 0, 0);
```

На:

```csharp
// НОВЫЙ КОД:
mainLayout.Controls.Add(_fileSection, 0, 0);
```

## Проверка работы

После интеграции:

1. ✅ Запустить приложение
2. ✅ Левая панель отображается как GroupBox "Файлы данных"
3. ✅ Кнопки "Выбрать файлы" работают
4. ✅ При выборе файлов:
   - Отображаются имена файлов
   - Активируются кнопки "Загрузить"
5. ✅ При нажатии "Загрузить":
   - В лог пишется информация о файлах
   - Статус бар обновляется
   - FileSection показывает сообщение (пока заглушка)

## Тестирование

Добавить в `Program.Main()` перед `Application.Run()`:

```csharp
#if DEBUG
    // Тестирование NormStorage
    Log.Information("=== Тест NormStorage ===");
    var storage = new NormStorage("test_norms.json");
    
    var testNorms = new Dictionary<string, NormData>
    {
        ["123"] = new NormData
        {
            Points = new List<NormPoint>
            {
                new NormPoint(50, 100),
                new NormPoint(60, 90),
                new NormPoint(70, 85)
            },
            NormType = "Нажатие",
            Description = "Тестовая норма"
        }
    };
    
    storage.AddOrUpdateNorms(testNorms);
    var interpolated = storage.InterpolateNormValue("123", 55);
    Log.Information("Интерполированное значение для нагрузки 55: {Value}", interpolated);
    
    // Тестирование CoefficientsManager
    Log.Information("=== Тест CoefficientsManager ===");
    var coeffManager = new CoefficientsManager();
    Log.Information("CoefficientsManager создан, готов к загрузке Excel");
#endif
```

## Результат

После успешной интеграции приложение будет:
- ✅ Компилироваться
- ✅ Запускаться с рабочим GUI
- ✅ Позволять выбирать файлы
- ✅ Логировать выбор файлов
- ⚠️ Реальная загрузка HTML - в Чате 3
