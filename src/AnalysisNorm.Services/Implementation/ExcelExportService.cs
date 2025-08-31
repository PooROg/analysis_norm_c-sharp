using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// ИСПРАВЛЕНО: Полная реализация сервиса экспорта в Excel
/// Все методы интерфейса реализованы, устранены проблемы с типами
/// </summary>
public class ExcelExportService : IExcelExportService
{
    #region Fields

    private readonly ILogger<ExcelExportService> _logger;

    // ИСПРАВЛЕНО: Статические константы для цветов DeviationStatus  
    private static readonly Dictionary<DeviationStatus, Color> StatusColors = new()
    {
        { DeviationStatus.StrongEconomy, Color.DarkGreen },
        { DeviationStatus.MediumEconomy, Color.Green },
        { DeviationStatus.WeakEconomy, Color.LightGreen },
        { DeviationStatus.Normal, Color.Blue },
        { DeviationStatus.WeakOverrun, Color.Orange },
        { DeviationStatus.MediumOverrun, Color.DarkOrange },
        { DeviationStatus.StrongOverrun, Color.Red },
        { DeviationStatus.Unknown, Color.Gray }
    };

    #endregion

    #region Constructor

    public ExcelExportService(ILogger<ExcelExportService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Настройка EPPlus лицензии
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    #endregion

    #region IExcelExportService Implementation - ИСПРАВЛЕНО

    /// <summary>
    /// ИСПРАВЛЕНО: Реализация метода экспорта результатов анализа
    /// </summary>
    public async Task<bool> ExportAnalysisResultsAsync(AnalysisResult result, string filePath,
        ExportOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Начало экспорта результатов анализа в файл {FilePath}", filePath);

            options ??= new ExportOptions();

            using var package = new ExcelPackage();

            // Создаем листы
            await CreateSummarySheetAsync(package, result, options, cancellationToken);
            await CreateRoutesSheetAsync(package, result.Routes, options, cancellationToken);

            if (options.IncludeStatistics)
                await CreateStatisticsSheetAsync(package, result, options, cancellationToken);

            if (options.IncludeCharts)
                await CreateChartsSheetAsync(package, result, options, cancellationToken);

            // Сохраняем файл
            await package.SaveAsAsync(new FileInfo(filePath), cancellationToken);

            _logger.LogInformation("Экспорт результатов анализа успешно завершен");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при экспорте результатов анализа");
            return false;
        }
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Реализация метода экспорта маршрутов
    /// </summary>
    public async Task<bool> ExportRoutesToExcelAsync(IEnumerable<Route> routes, string filePath,
        ExportOptions? options = null)
    {
        try
        {
            _logger.LogInformation("Начало экспорта {Count} маршрутов в файл {FilePath}",
                routes.Count(), filePath);

            options ??= new ExportOptions();

            using var package = new ExcelPackage();

            // ИСПРАВЛЕНО: Правильное преобразование IEnumerable в IList
            var routesList = routes.ToList();
            await CreateRoutesSheetAsync(package, routesList, options, CancellationToken.None);

            await package.SaveAsAsync(new FileInfo(filePath));

            _logger.LogInformation("Экспорт маршрутов успешно завершен");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при экспорте маршрутов");
            return false;
        }
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Реализация проверки возможности создания файла
    /// </summary>
    public bool CanCreateFile(string filePath)
    {
        try
        {
            // Проверяем что путь не пустой
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            // Проверяем что директория существует или может быть создана
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch
                {
                    return false;
                }
            }

            // Проверяем что файл может быть создан (не заблокирован)
            using var stream = File.Create(filePath);

            // Удаляем тестовый файл
            File.Delete(filePath);

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Private Sheet Creation Methods

    /// <summary>
    /// Создает лист с общей информацией
    /// </summary>
    private async Task CreateSummarySheetAsync(ExcelPackage package, AnalysisResult result,
        ExportOptions options, CancellationToken cancellationToken)
    {
        var worksheet = package.Workbook.Worksheets.Add("Сводка");

        int row = 1;

        // Заголовок
        worksheet.Cells[row, 1].Value = "РЕЗУЛЬТАТЫ АНАЛИЗА МАРШРУТОВ";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Size = 16;
        row += 2;

        // Основная информация
        worksheet.Cells[row, 1].Value = "Название анализа:";
        worksheet.Cells[row, 2].Value = result.Name;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        row++;

        worksheet.Cells[row, 1].Value = "Дата анализа:";
        worksheet.Cells[row, 2].Value = result.AnalysisDate; // ИСПРАВЛЕНО: используем правильное свойство
        worksheet.Cells[row, 2].Style.Numberformat.Format = "dd.mm.yyyy hh:mm";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        row++;

        worksheet.Cells[row, 1].Value = "Общее количество маршрутов:";
        worksheet.Cells[row, 2].Value = result.TotalRoutes;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        row++;

        worksheet.Cells[row, 1].Value = "Средний процент отклонения:";
        // ИСПРАВЛЕНО: правильная обработка nullable типов
        worksheet.Cells[row, 2].Value = result.AverageDeviationPercentage?.ToString("F2") ?? "Н/Д";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        row += 2;

        // Статистика по статусам отклонений
        worksheet.Cells[row, 1].Value = "РАСПРЕДЕЛЕНИЕ ПО СТАТУСАМ ОТКЛОНЕНИЙ";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        row++;

        foreach (var statusCount in result.DeviationCounts.OrderBy(dc => dc.Key))
        {
            worksheet.Cells[row, 1].Value = DeviationStatusHelper.GetDescription(statusCount.Key);
            worksheet.Cells[row, 2].Value = statusCount.Value;

            // ИСПРАВЛЕНО: правильное получение цвета и его применение
            var color = StatusColors[statusCount.Key];
            worksheet.Cells[row, 1].Style.Font.Color.SetColor(color);
            worksheet.Cells[row, 1].Style.Font.Bold = true;

            row++;
        }

        // Автоширина колонок
        worksheet.Columns.AutoFit();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Создает лист с детальной информацией о маршрутах
    /// </summary>
    private async Task CreateRoutesSheetAsync(ExcelPackage package, IList<Route> routes,
        ExportOptions options, CancellationToken cancellationToken)
    {
        var worksheet = package.Workbook.Worksheets.Add("Маршруты");

        // Создаем заголовки
        var headers = new[]
        {
            "№", "Название маршрута", "Дата", "Локомотив", "Серия", "Номер",
            "Расстояние, км", "Масса состава, т", "Нагрузка на ось, т",
            "Фактический расход", "Нормативный расход", "Отклонение",
            "Отклонение, %", "Статус", "Участки"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
        }

        // Заполняем данными
        int row = 2;
        int index = 1;

        foreach (var route in routes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            worksheet.Cells[row, 1].Value = index++;
            worksheet.Cells[row, 2].Value = route.Name;

            // ИСПРАВЛЕНО: используем правильные свойства
            worksheet.Cells[row, 3].Value = route.Date;
            worksheet.Cells[row, 3].Style.Numberformat.Format = "dd.mm.yyyy";

            worksheet.Cells[row, 4].Value = route.LocomotiveType ?? "Н/Д";
            worksheet.Cells[row, 5].Value = route.LocomotiveSeries ?? "Н/Д";
            worksheet.Cells[row, 6].Value = route.LocomotiveNumber?.ToString() ?? "Н/Д";

            worksheet.Cells[row, 7].Value = (double)route.Distance;
            worksheet.Cells[row, 8].Value = (double)route.TrainMass;
            worksheet.Cells[row, 9].Value = (double)route.AxleLoad;

            // ИСПРАВЛЕНО: правильная обработка nullable decimal
            worksheet.Cells[row, 10].Value = route.ActualConsumption.HasValue
                ? (double)route.ActualConsumption.Value
                : (object)"Н/Д";
            worksheet.Cells[row, 11].Value = route.NormativeConsumption.HasValue
                ? (double)route.NormativeConsumption.Value
                : (object)"Н/Д";
            worksheet.Cells[row, 12].Value = route.Deviation.HasValue
                ? (double)route.Deviation.Value
                : (object)"Н/Д";
            worksheet.Cells[row, 13].Value = route.DeviationPercentage.HasValue
                ? (double)route.DeviationPercentage.Value
                : (object)"Н/Д";

            // ИСПРАВЛЕНО: правильное преобразование DeviationStatus в строку
            worksheet.Cells[row, 14].Value = DeviationStatusHelper.ToString(route.Status);

            // Цветовое кодирование статуса
            var statusColor = StatusColors[route.Status];
            worksheet.Cells[row, 14].Style.Font.Color.SetColor(statusColor);
            worksheet.Cells[row, 14].Style.Font.Bold = true;

            // ИСПРАВЛЕНО: правильная работа с SectionNames
            worksheet.Cells[row, 15].Value = string.Join(", ", route.SectionNames);

            row++;
        }

        // Форматируем колонки
        worksheet.Cells[1, 1, row - 1, headers.Length].AutoFitColumns();

        // Замораживаем заголовок
        worksheet.View.FreezePanes(2, 1);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Создает лист со статистикой
    /// </summary>
    private async Task CreateStatisticsSheetAsync(ExcelPackage package, AnalysisResult result,
        ExportOptions options, CancellationToken cancellationToken)
    {
        var worksheet = package.Workbook.Worksheets.Add("Статистика");

        int row = 1;

        // Заголовок
        worksheet.Cells[row, 1].Value = "ПОДРОБНАЯ СТАТИСТИКА";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Size = 16;
        row += 2;

        // Временная статистика
        worksheet.Cells[row, 1].Value = "Время начала анализа:";
        worksheet.Cells[row, 2].Value = result.StartTime;
        worksheet.Cells[row, 2].Style.Numberformat.Format = "dd.mm.yyyy hh:mm:ss";
        row++;

        if (result.EndTime.HasValue)
        {
            worksheet.Cells[row, 1].Value = "Время окончания:";
            worksheet.Cells[row, 2].Value = result.EndTime.Value;
            worksheet.Cells[row, 2].Style.Numberformat.Format = "dd.mm.yyyy hh:mm:ss";
            row++;

            worksheet.Cells[row, 1].Value = "Длительность анализа:";
            worksheet.Cells[row, 2].Value = result.Duration?.ToString(@"hh\:mm\:ss") ?? "Н/Д";
            row++;
        }

        row++;

        // Качественные показатели
        worksheet.Cells[row, 1].Value = "ПОКАЗАТЕЛИ КАЧЕСТВА";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        row++;

        worksheet.Cells[row, 1].Value = "Количество ошибок:";
        worksheet.Cells[row, 2].Value = result.ErrorCount;
        row++;

        worksheet.Cells[row, 1].Value = "Количество предупреждений:";
        worksheet.Cells[row, 2].Value = result.WarningCount;
        row++;

        worksheet.Cells[row, 1].Value = "Оценка качества данных:";
        worksheet.Cells[row, 2].Value = result.DataQualityScore;
        row++;

        // Автоширина колонок
        worksheet.Columns.AutoFitColumns();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Создает лист с графиками (заглушка)
    /// </summary>
    private async Task CreateChartsSheetAsync(ExcelPackage package, AnalysisResult result,
        ExportOptions options, CancellationToken cancellationToken)
    {
        var worksheet = package.Workbook.Worksheets.Add("Графики");

        // Пока что создаем заглушку
        worksheet.Cells[1, 1].Value = "Графики будут добавлены в следующих версиях";
        worksheet.Cells[1, 1].Style.Font.Bold = true;
        worksheet.Cells[1, 1].Style.Font.Size = 14;

        await Task.CompletedTask;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Применяет стандартное форматирование к заголовкам
    /// </summary>
    private static void FormatHeaders(ExcelWorksheet worksheet, int row, int startColumn, int endColumn)
    {
        var headerRange = worksheet.Cells[row, startColumn, row, endColumn];
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.PatternType = ExcelFillStyle.Solid;
        headerRange.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
        headerRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thick;
        headerRange.Style.Border.Bottom.Color.SetColor(Color.Black);
    }

    /// <summary>
    /// Форматирует числовые значения в колонке
    /// </summary>
    private static void FormatNumericColumn(ExcelWorksheet worksheet, int column, int startRow, int endRow, string format)
    {
        var range = worksheet.Cells[startRow, column, endRow, column];
        range.Style.Numberformat.Format = format;
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
    }

    /// <summary>
    /// Устанавливает автоширину для всех колонок
    /// </summary>
    private static void AutoFitAllColumns(ExcelWorksheet worksheet)
    {
        worksheet.Cells.AutoFitColumns();

        // Ограничиваем максимальную ширину колонок
        for (int i = 1; i <= worksheet.Dimension.End.Column; i++)
        {
            if (worksheet.Column(i).Width > 50)
                worksheet.Column(i).Width = 50;
        }
    }

    #endregion
}

/// <summary>
/// Опции экспорта
/// ИСПРАВЛЕНО: Убираем конфликт имен с другими ExportOptions
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Включать ли статистику в экспорт
    /// </summary>
    public bool IncludeStatistics { get; set; } = true;

    /// <summary>
    /// Включать ли графики в экспорт
    /// </summary>
    public bool IncludeCharts { get; set; } = false;

    /// <summary>
    /// Максимальное количество маршрутов для экспорта
    /// </summary>
    public int MaxRoutes { get; set; } = 10000;

    /// <summary>
    /// Формат даты для экспорта
    /// </summary>
    public string DateFormat { get; set; } = "dd.mm.yyyy";

    /// <summary>
    /// Формат чисел для экспорта
    /// </summary>
    public string NumberFormat { get; set; } = "0.00";

    /// <summary>
    /// Включать ли цветовое кодирование статусов
    /// </summary>
    public bool UseColorCoding { get; set; } = true;

    /// <summary>
    /// Автоматически подгонять ширину колонок
    /// </summary>
    public bool AutoFitColumns { get; set; } = true;
}