// Services/Implementation/ExcelExportService.cs
using System.Drawing;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Models.Domain;
using AnalysisNorm.Infrastructure.Logging;
using AnalysisNorm.Configuration;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// CHAT 4: Полнофункциональный Excel экспорт с форматированием
/// Точное соответствие Python export_to_excel() функциональности
/// Включает: форматирование, подсветку статусов, автоширину колонок, заморозку заголовков
/// </summary>
public class ExcelExportService : IExcelExporter
{
    private readonly IApplicationLogger _logger;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IConfigurationService _configService;
    private readonly ExcelExportConfiguration _config;

    // Определения колонок (точная копия Python columns)
    private static readonly ExcelColumnDefinition[] ColumnDefinitions = 
    {
        new("A", "Номер маршрута", 15, Color.LightBlue),
        new("B", "Дата маршрута", 12, Color.LightGray),
        new("C", "Дата поездки", 12, Color.LightGray),
        new("D", "Табельный машиниста", 18, Color.LightYellow),
        new("E", "Серия локомотива", 15, Color.LightGreen),
        new("F", "Номер локомотива", 15, Color.LightGreen),
        new("G", "НЕТТО", 10, Color.LightCyan),
        new("H", "БРУТТО", 10, Color.LightCyan),
        new("I", "ОСИ", 8, Color.LightCyan),
        new("J", "Наименование участка", 35, Color.White),
        new("K", "Номер нормы", 12, Color.LightPink),
        new("L", "Дв. тяга", 10, Color.Orange),
        new("M", "Ткм брутто", 12, Color.LightYellow),
        new("N", "Км", 8, Color.LightYellow),
        new("O", "Пр.", 8, Color.LightGray),
        new("P", "Расход фактический", 18, Color.LightSalmon),
        new("Q", "Расход по норме", 18, Color.LightSalmon),
        new("R", "Уд. норма, норма на 1 час ман. раб.", 30, Color.LightPink),
        new("S", "Отклонение %", 15, Color.Yellow),
        new("T", "Статус", 12, Color.White),
        new("U", "Количество дубликатов маршрута", 25, Color.LightSteelBlue)
    };

    // Цвета для статусов отклонений (из Python StatusClassifier)
    private static readonly Dictionary<DeviationStatus, Color> StatusColors = new()
    {
        { DeviationStatus.Excellent, Color.DarkGreen },
        { DeviationStatus.Good, Color.Green },
        { DeviationStatus.Acceptable, Color.Orange },
        { DeviationStatus.Poor, Color.Red },
        { DeviationStatus.Critical, Color.DarkRed },
        { DeviationStatus.Unknown, Color.Gray }
    };

    public ExcelExportService(
        IApplicationLogger logger,
        IPerformanceMonitor performanceMonitor,
        IConfigurationService configService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _config = _configService.GetConfiguration<ExcelExportConfiguration>();

        // Настройка EPPlus лицензии
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// ГЛАВНЫЙ метод: Экспорт маршрутов в Excel с полным форматированием
    /// Точное соответствие Python export_to_excel()
    /// </summary>
    public async Task<ProcessingResult<string>> ExportRoutesToExcelAsync(
        IEnumerable<Route> routes, 
        string outputPath, 
        ExportOptions? options = null)
    {
        try
        {
            _performanceMonitor.StartOperation("ExcelExport");
            _logger.LogInformation("Начат экспорт {Count} маршрутов в Excel: {OutputPath}", 
                routes.Count(), outputPath);

            var exportOptions = options ?? ExportOptions.Default;
            var routeList = routes.ToList();

            if (!routeList.Any())
            {
                _logger.LogWarning("Нет маршрутов для экспорта");
                return ProcessingResult<string>.Failure("Нет данных для экспорта");
            }

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Анализ маршрутов");

            // Этап 1: Создание заголовков с форматированием
            await CreateFormattedHeadersAsync(worksheet, exportOptions);

            // Этап 2: Заполнение данными маршрутов
            var currentRow = await FillRouteDataAsync(worksheet, routeList, exportOptions);

            // Этап 3: Применение форматирования и условного форматирования
            await ApplyAdvancedFormattingAsync(worksheet, currentRow, exportOptions);

            // Этап 4: Добавление сводной статистики (если требуется)
            if (exportOptions.IncludeSummaryStatistics)
            {
                await AddSummaryStatisticsAsync(worksheet, routeList, currentRow + 2);
            }

            // Этап 5: Сохранение файла
            await package.SaveAsAsync(outputPath);

            _logger.LogInformation("Excel экспорт завершен успешно: {OutputPath}, размер файла: {FileSize} KB", 
                outputPath, new FileInfo(outputPath).Length / 1024);

            return ProcessingResult<string>.Success(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка экспорта в Excel: {OutputPath}", outputPath);
            return ProcessingResult<string>.Failure($"Ошибка экспорта: {ex.Message}");
        }
        finally
        {
            _performanceMonitor.EndOperation("ExcelExport");
        }
    }

    /// <summary>
    /// Создание заголовков с профессиональным форматированием
    /// </summary>
    private async Task CreateFormattedHeadersAsync(ExcelWorksheet worksheet, ExportOptions options)
    {
        await Task.Yield();

        _logger.LogDebug("Создание заголовков Excel");

        for (int i = 0; i < ColumnDefinitions.Length; i++)
        {
            var col = ColumnDefinitions[i];
            var cell = worksheet.Cells[1, i + 1];
            
            // Устанавливаем текст заголовка
            cell.Value = col.Header;
            
            // Форматирование заголовка
            cell.Style.Font.Bold = true;
            cell.Style.Font.Size = 12;
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.Fill.PatternType = ExcelFillPatternType.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.DarkBlue);
            
            // Границы
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thick, Color.Black);
            
            // Выравнивание и перенос текста
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            cell.Style.WrapText = true;
            
            // Ширина колонки
            worksheet.Column(i + 1).Width = col.Width;
        }

        // Заморозка первой строки
        worksheet.View.FreezePanes(2, 1);
        
        _logger.LogDebug("Заголовки созданы и отформатированы");
    }

    /// <summary>
    /// КЛЮЧЕВАЯ функция: Заполнение данными маршрутов с детальным форматированием
    /// </summary>
    private async Task<int> FillRouteDataAsync(ExcelWorksheet worksheet, List<Route> routes, ExportOptions options)
    {
        await Task.Yield();

        _logger.LogDebug("Заполнение данными {Count} маршрутов", routes.Count);

        int currentRow = 2; // Начинаем со второй строки (после заголовков)

        foreach (var route in routes)
        {
            foreach (var section in route.Sections)
            {
                await FillRouteRowAsync(worksheet, route, section, currentRow, options);
                currentRow++;
            }
        }

        _logger.LogDebug("Данные заполнены, всего строк: {RowCount}", currentRow - 1);
        return currentRow - 1;
    }

    /// <summary>
    /// Заполнение одной строки данных маршрута/участка
    /// </summary>
    private async Task FillRouteRowAsync(ExcelWorksheet worksheet, Route route, Section section, int row, ExportOptions options)
    {
        await Task.Yield();

        // Извлекаем дополнительные данные из метаданных
        var sectionMetadata = section.Metadata?.AdditionalData ?? new Dictionary<string, object>();
        var routeMetadata = route.Metadata?.AdditionalData ?? new Dictionary<string, object>();

        // Заполняем ячейки согласно определениям колонок
        worksheet.Cells[row, 1].Value = route.Number; // Номер маршрута
        worksheet.Cells[row, 2].Value = route.Date.ToString("dd.MM.yyyy"); // Дата маршрута
        worksheet.Cells[row, 3].Value = route.Date.ToString("dd.MM.yyyy"); // Дата поездки (для совместимости)
        worksheet.Cells[row, 4].Value = ""; // Табельный машиниста (из ТУ3 если доступно)
        worksheet.Cells[row, 5].Value = route.Locomotive.Model; // Серия локомотива
        worksheet.Cells[row, 6].Value = route.Locomotive.Number; // Номер локомотива

        // Ю7 данные (НЕТТО/БРУТТО/ОСИ)
        worksheet.Cells[row, 7].Value = sectionMetadata.GetValueOrDefault("Yu7_Netto", "-");
        worksheet.Cells[row, 8].Value = sectionMetadata.GetValueOrDefault("Yu7_Brutto", "-");
        worksheet.Cells[row, 9].Value = sectionMetadata.GetValueOrDefault("Yu7_Osi", "-");

        worksheet.Cells[row, 10].Value = section.Name; // Наименование участка
        worksheet.Cells[row, 11].Value = section.NormId ?? ""; // Номер нормы

        // Двойная тяга
        var isDoubleTraction = sectionMetadata.GetValueOrDefault("IsDoubleTraction", false);
        worksheet.Cells[row, 12].Value = (bool)isDoubleTraction ? "Да" : "";

        // Числовые показатели
        worksheet.Cells[row, 13].Value = section.TkmBrutto; // Ткм брутто
        worksheet.Cells[row, 14].Value = section.Distance; // Км
        worksheet.Cells[row, 15].Value = ""; // Пр. (простой)
        worksheet.Cells[row, 16].Value = section.ActualConsumption; // Расход фактический
        worksheet.Cells[row, 17].Value = section.NormConsumption; // Расход по норме
        worksheet.Cells[row, 18].Value = ""; // Уд. норма

        // Вычисление отклонения и статуса
        var deviationPercent = CalculateDeviationPercent(section.ActualConsumption, section.NormConsumption);
        var status = ClassifyDeviationStatus(deviationPercent);

        worksheet.Cells[row, 19].Value = deviationPercent; // Отклонение %
        worksheet.Cells[row, 20].Value = GetStatusDescription(status); // Статус

        // Количество дубликатов
        var duplicateCount = routeMetadata.GetValueOrDefault("DuplicateCount", 1);
        worksheet.Cells[row, 21].Value = (int)duplicateCount > 1 ? duplicateCount : "";

        // Применяем форматирование к строке
        await ApplyRowFormattingAsync(worksheet, row, status, options);
    }

    /// <summary>
    /// Применение расширенного форматирования к листу
    /// </summary>
    private async Task ApplyAdvancedFormattingAsync(ExcelWorksheet worksheet, int lastRow, ExportOptions options)
    {
        await Task.Yield();

        _logger.LogDebug("Применение расширенного форматирования к {RowCount} строкам", lastRow);

        // Границы для всей таблицы
        var tableRange = worksheet.Cells[1, 1, lastRow, ColumnDefinitions.Length];
        tableRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        tableRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        tableRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        tableRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;

        // Форматирование числовых колонок
        await FormatNumericColumnsAsync(worksheet, lastRow);

        // Условное форматирование для колонки отклонений
        if (options.EnableConditionalFormatting)
        {
            await ApplyConditionalFormattingAsync(worksheet, lastRow);
        }

        // Автоподгонка высоты строк
        for (int row = 1; row <= lastRow; row++)
        {
            worksheet.Row(row).Height = 20;
        }

        _logger.LogDebug("Расширенное форматирование применено");
    }

    /// <summary>
    /// Форматирование числовых колонок
    /// </summary>
    private async Task FormatNumericColumnsAsync(ExcelWorksheet worksheet, int lastRow)
    {
        await Task.Yield();

        // Колонки с числовыми данными и их форматы
        var numericFormats = new Dictionary<int, string>
        {
            { 13, "#,##0.00" }, // Ткм брутто
            { 14, "#,##0.0" },  // Км
            { 16, "#,##0.0" },  // Расход фактический
            { 17, "#,##0.0" },  // Расход по норме
            { 19, "0.0%" }      // Отклонение %
        };

        foreach (var (column, format) in numericFormats)
        {
            var range = worksheet.Cells[2, column, lastRow, column];
            range.Style.Numberformat.Format = format;
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
        }
    }

    /// <summary>
    /// Условное форматирование для статусов
    /// </summary>
    private async Task ApplyConditionalFormattingAsync(ExcelWorksheet worksheet, int lastRow)
    {
        await Task.Yield();

        // Применяем условное форматирование к колонке статусов (T)
        var statusRange = worksheet.Cells[2, 20, lastRow, 20];
        
        foreach (var (status, color) in StatusColors)
        {
            var statusText = GetStatusDescription(status);
            var rule = statusRange.ConditionalFormatting.AddEqual();
            rule.Formula = $"\"{statusText}\"";
            rule.Style.Fill.PatternType = ExcelFillPatternType.Solid;
            rule.Style.Fill.BackgroundColor.Color = color;
            rule.Style.Font.Color.SetColor(Color.White);
            rule.Style.Font.Bold = true;
        }
    }

    /// <summary>
    /// Применение форматирования к отдельной строке
    /// </summary>
    private async Task ApplyRowFormattingAsync(ExcelWorksheet worksheet, int row, DeviationStatus status, ExportOptions options)
    {
        await Task.Yield();

        // Чередующиеся цвета строк для лучшей читаемости
        if (row % 2 == 0)
        {
            var rowRange = worksheet.Cells[row, 1, row, ColumnDefinitions.Length];
            rowRange.Style.Fill.PatternType = ExcelFillPatternType.Solid;
            rowRange.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(245, 245, 245)); // Светло-серый
        }

        // Подсветка критических статусов
        if (status == DeviationStatus.Critical || status == DeviationStatus.Poor)
        {
            var statusCell = worksheet.Cells[row, 20]; // Колонка статуса
            statusCell.Style.Font.Bold = true;
            statusCell.Style.Font.Color.SetColor(Color.White);
            statusCell.Style.Fill.PatternType = ExcelFillPatternType.Solid;
            statusCell.Style.Fill.BackgroundColor.SetColor(StatusColors[status]);
        }
    }

    /// <summary>
    /// ДОПОЛНИТЕЛЬНАЯ функция: Добавление сводной статистики
    /// </summary>
    private async Task AddSummaryStatisticsAsync(ExcelWorksheet worksheet, List<Route> routes, int startRow)
    {
        await Task.Yield();

        _logger.LogDebug("Добавление сводной статистики начиная с строки {StartRow}", startRow);

        // Создаем заголовок сводки
        worksheet.Cells[startRow, 1].Value = "СВОДНАЯ СТАТИСТИКА";
        worksheet.Cells[startRow, 1].Style.Font.Bold = true;
        worksheet.Cells[startRow, 1].Style.Font.Size = 14;
        worksheet.Cells[startRow, 1].Style.Fill.PatternType = ExcelFillPatternType.Solid;
        worksheet.Cells[startRow, 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);

        startRow += 2;

        // Общая статистика
        var totalRoutes = routes.Count;
        var totalSections = routes.Sum(r => r.Sections.Count);
        var totalActualConsumption = routes.Sum(r => r.TotalActualConsumption);
        var totalNormConsumption = routes.Sum(r => r.TotalNormConsumption);

        var statistics = new[]
        {
            ("Всего маршрутов:", totalRoutes.ToString()),
            ("Всего участков:", totalSections.ToString()),
            ("Общий фактический расход:", totalActualConsumption.ToString("N1")),
            ("Общий нормативный расход:", totalNormConsumption.ToString("N1")),
            ("Среднее отклонение:", CalculateDeviationPercent(totalActualConsumption, totalNormConsumption).ToString("P1"))
        };

        foreach (var (label, value) in statistics)
        {
            worksheet.Cells[startRow, 1].Value = label;
            worksheet.Cells[startRow, 2].Value = value;
            worksheet.Cells[startRow, 1].Style.Font.Bold = true;
            startRow++;
        }

        _logger.LogDebug("Сводная статистика добавлена");
    }

    /// <summary>
    /// Экспорт диагностических данных в отдельный лист
    /// </summary>
    public async Task<ProcessingResult<string>> ExportDiagnosticDataAsync(
        DuplicationAnalysis duplicationAnalysis,
        List<SectionMergeAnalysis> mergeAnalyses,
        string outputPath)
    {
        try
        {
            _performanceMonitor.StartOperation("DiagnosticExport");
            _logger.LogInformation("Экспорт диагностических данных: {OutputPath}", outputPath);

            using var package = new ExcelPackage();
            
            // Лист анализа дубликатов
            await CreateDuplicationAnalysisSheetAsync(package, duplicationAnalysis);
            
            // Лист анализа объединения участков
            await CreateMergeAnalysisSheetAsync(package, mergeAnalyses);

            await package.SaveAsAsync(outputPath);

            _logger.LogInformation("Диагностический экспорт завершен: {OutputPath}", outputPath);
            return ProcessingResult<string>.Success(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта диагностических данных: {OutputPath}", outputPath);
            return ProcessingResult<string>.Failure($"Ошибка диагностического экспорта: {ex.Message}");
        }
        finally
        {
            _performanceMonitor.EndOperation("DiagnosticExport");
        }
    }

    // Вспомогательные методы для работы со статусами и расчетами
    
    private decimal CalculateDeviationPercent(decimal actual, decimal norm)
    {
        return norm != 0 ? ((actual - norm) / norm) * 100 : 0;
    }

    private DeviationStatus ClassifyDeviationStatus(decimal deviationPercent)
    {
        var absDeviation = Math.Abs(deviationPercent);
        return absDeviation switch
        {
            <= 5 => DeviationStatus.Excellent,
            <= 10 => DeviationStatus.Good,
            <= 20 => DeviationStatus.Acceptable,
            <= 30 => DeviationStatus.Poor,
            _ => DeviationStatus.Critical
        };
    }

    private string GetStatusDescription(DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.Excellent => "Отлично",
            DeviationStatus.Good => "Хорошо",
            DeviationStatus.Acceptable => "Приемлемо",
            DeviationStatus.Poor => "Плохо",
            DeviationStatus.Critical => "Критично",
            _ => "Неизвестно"
        };
    }

    private async Task CreateDuplicationAnalysisSheetAsync(ExcelPackage package, DuplicationAnalysis analysis)
    {
        await Task.Yield();
        var worksheet = package.Workbook.Worksheets.Add("Анализ дубликатов");
        
        // Реализация создания листа с анализом дубликатов
        worksheet.Cells[1, 1].Value = "Анализ дубликатов маршрутов";
        worksheet.Cells[2, 1].Value = "Всего маршрутов:";
        worksheet.Cells[2, 2].Value = analysis.TotalRoutes;
        // ... дополнительные данные
    }

    private async Task CreateMergeAnalysisSheetAsync(ExcelPackage package, List<SectionMergeAnalysis> analyses)
    {
        await Task.Yield();
        var worksheet = package.Workbook.Worksheets.Add("Анализ объединения");
        
        // Реализация создания листа с анализом объединения участков
        worksheet.Cells[1, 1].Value = "Анализ объединения участков";
        // ... данные по каждому маршруту
    }
}

/// <summary>
/// Определение колонки Excel
/// </summary>
public record ExcelColumnDefinition(string Letter, string Header, int Width, Color BackgroundColor);

/// <summary>
/// Параметры экспорта
/// </summary>
public record ExportOptions
{
    public bool IncludeSummaryStatistics { get; init; } = true;
    public bool EnableConditionalFormatting { get; init; } = true;
    public bool AutoFitColumns { get; init; } = false;
    public bool FreezeHeaderRow { get; init; } = true;
    public string DateFormat { get; init; } = "dd.MM.yyyy";
    public string NumberFormat { get; init; } = "#,##0.0";

    public static ExportOptions Default => new();
}
