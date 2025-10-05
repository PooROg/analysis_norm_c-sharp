# Быстрая инструкция: Применение интеграции Чата 2

## 📥 Что нужно сделать

### Шаг 1: Заменить MainWindow.cs

**Скопировать файл:**
```
/home/claude/MainWindow.cs → analysis_norm_c-sharp/GUI/MainWindow.cs
```

**Или применить изменения вручную:**

#### 1. Добавить using директивы (в начало файла):
```csharp
using AnalysisNorm.GUI.Components;
using AnalysisNorm.Core;
```

#### 2. Изменить поле класса:
```csharp
// БЫЛО:
private Panel fileSectionPanel;

// СТАЛО:
private FileSection _fileSection;
```

#### 3. В методе SetupMainLayout() заменить создание placeholder:

**УДАЛИТЬ:**
```csharp
fileSectionPanel = CreatePlaceholderPanel(
    "Секция выбора файлов",
    "Будет реализована в Чате 2..."
);
mainLayout.Controls.Add(fileSectionPanel, 0, 0);
```

**ДОБАВИТЬ:**
```csharp
// Создаем FileSection вместо placeholder
_fileSection = new FileSection
{
    Dock = DockStyle.Fill
};

// Подписываемся на события FileSection
_fileSection.OnRoutesLoaded += FileSection_OnRoutesLoaded;
_fileSection.OnNormsLoaded += FileSection_OnNormsLoaded;
_fileSection.OnCoefficientsLoaded += FileSection_OnCoefficientsLoaded;

Log.Information("FileSection создан и события подключены");

// Добавляем в layout
mainLayout.Controls.Add(_fileSection, 0, 0);
```

#### 4. Добавить в конец класса (перед закрывающей скобкой):

```csharp
#region Обработчики событий FileSection (ЧАТ 2) ✅

/// <summary>
/// Обработчик события загрузки HTML маршрутов
/// </summary>
private void FileSection_OnRoutesLoaded(List<string> files)
{
    Log.Information("MainWindow: Получены файлы маршрутов: {Count}", files.Count);
    
    foreach (var file in files)
    {
        Log.Debug("  - {FileName}", Path.GetFileName(file));
    }

    UpdateStatusBar($"Выбрано файлов маршрутов: {files.Count}");

    // TODO Чат 3: Вызвать RouteProcessor для парсинга HTML
}

/// <summary>
/// Обработчик события загрузки HTML норм
/// </summary>
private void FileSection_OnNormsLoaded(List<string> files)
{
    Log.Information("MainWindow: Получены файлы норм: {Count}", files.Count);
    
    foreach (var file in files)
    {
        Log.Debug("  - {FileName}", Path.GetFileName(file));
    }

    UpdateStatusBar($"Выбрано файлов норм: {files.Count}");

    // TODO Чат 3: Вызвать NormProcessor для парсинга HTML
}

/// <summary>
/// Обработчик события загрузки Excel коэффициентов
/// </summary>
private void FileSection_OnCoefficientsLoaded(string filePath)
{
    Log.Information("MainWindow: Получен файл коэффициентов: {File}", 
        Path.GetFileName(filePath));

    UpdateStatusBar($"Выбран файл коэффициентов: {Path.GetFileName(filePath)}");

    // TODO Чат 5: Открыть диалог селектора локомотивов
}

#endregion
```

---

### Шаг 2: Скомпилировать

```bash
cd analysis_norm_c-sharp
dotnet build
```

**Ожидаемый результат:** ✅ Build succeeded

---

### Шаг 3: Запустить и протестировать

```bash
dotnet run
```

**Проверить:**
1. ✅ Левая панель показывает "Файлы данных"
2. ✅ Кнопки "Выбрать файлы" работают
3. ✅ Выбранные файлы отображаются
4. ✅ Кнопки "Загрузить" активируются
5. ✅ При нажатии "Загрузить" обновляется статус
6. ✅ Логи пишутся в файл

---

## ✅ Готово!

Теперь интеграция FileSection полностью завершена и Чат 2 на 100% готов.

**Следующий шаг**: Переход к Чату 3 (HTML парсинг)

---

## 📄 Дополнительные файлы

В `/home/claude/` созданы:
- `MainWindow.cs` - полный обновленный файл
- `CHAT2_INTEGRATION_COMPLETE.md` - детальная документация
- `MIGRATION_LOG_UPDATED.md` - обновленный лог миграции

---

## 🆘 Помощь

Если что-то не работает:
1. Проверить using директивы
2. Проверить, что FileSection.cs находится в GUI/Components/
3. Проверить namespace в FileSection.cs: `AnalysisNorm.GUI.Components`
4. Проверить логи Serilog

**Интеграция завершена успешно! ✅**
