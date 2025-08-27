using System.Drawing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Сервис экспорта в Excel с форматированием и подсветкой
/// Соответствует export функциональности Python с улучшениями
/// </summary>
public class ExcelExportService : IExcelExportService
{
    private readonly ILogger<ExcelExportService> _logger;
    private readonly ApplicationSettings _settings;
    
    // Цвета для подсветки статусов (соответствуют Python ColorMapper)
    private static readonly Dictionary<string, Color> StatusColors = new()
    {
        [DeviationStatus.EconomyStrong] = Color.FromArgb(0, 100, 0),    // DarkGreen
        [DeviationStatus.EconomyMedium] = Color.FromArgb(0, 128, 0),    // Green  
        [DeviationStatus.EconomyWeak] = Color.FromArgb(144, 238, 144),  // LightGreen
        [DeviationStatus.Normal] = Color.FromArgb(173, 216, 230),       // LightBlue
        [DeviationStatus.OverrunWeak] = Color.FromArgb(255, 165, 0),    // Orange
        [DeviationStatus.OverrunMedium] = Color.FromArgb(255, 140, 0),  // DarkOrange
        [DeviationStatus.OverrunStrong] = Color.FromArgb(220, 20, 60)   // Crimson
    };

    public ExcelExportService(ILogger<ExcelExportService> logger, IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
        
        // Настройка EPPlus
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    /// <summary>
    /// Экспортирует маршруты в Excel с форматированием
    /// Соответствует export_to_excel из Python HTMLRouteProcessor
    /// </summary>
    public async Task<bool> ExportRoutesToExcelAsync(
        IEnumerable<Route> routes, 
        string outputPath,
        ExportOptions? options = null)
    {
        var routesList = routes.ToList();
        _logger.LogInformation("Экспортируем {Count} маршрутов в файл: {OutputPath}", routesList.Count, outputPath);

        if (!routesList.Any())
        {
            _logger.LogWarning("Нет данных для экспорта");
            return false;
        }

        options ??= new ExportOptions();

        try
        {
            // Создаем директорию если не существует
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Маршруты");

            // Создаем заголовки
            CreateHeaders(worksheet, options);
            
            // Заполняем данные
            await FillRoutesDataAsync(worksheet, routesList, options);
            
            // Применяем форматирование
            if (options.IncludeFormatting)
            {
                ApplyFormatting(worksheet, routesList.Count, options);
            }

            // Применяем подсветку отклонений
            if (options.HighlightDeviations)
            {
                ApplyDeviationHighlighting(worksheet, routesList);
            }

            // Добавляем статистику
            if (options.IncludeStatistics)
            {
                AddStatisticsSheet(package, routesList);
            }

            // Сохраняем файл
            await package.SaveAsAsync(new FileInfo(outputPath));
            
            _logger.LogInformation("Экспорт завершен успешно: {OutputPath}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта в Excel: {OutputPath}", outputPath);
            return false;
        }
    }

    /// <summary>
    /// Экспортирует результаты анализа в Excel
    /// </summary>
    public async Task<bool> ExportAnalysisToExcelAsync(AnalysisResult analysisResult, string outputPath)
    {
        _logger.LogInformation("Экспортируем результаты анализа участка {SectionName} в файл: {OutputPath}", 
            analysisResult.SectionName, outputPath);

        try
        {
            using var package = new ExcelPackage();
            
            // Лист с общей информацией об анализе
            var summarySheet = package.Workbook.Worksheets.Add("Сводка анализа");
            CreateAnalysisSummary(summarySheet, analysisResult);

            // Лист с маршрутами (если есть)
            if (analysisResult.Routes.Any())
            {
                var routesSheet = package.Workbook.Worksheets.Add("Маршруты");
                CreateHeaders(routesSheet, new ExportOptions { IncludeFormatting = true, HighlightDeviations = true });
                await FillRoutesDataAsync(routesSheet, analysisResult.Routes, new ExportOptions());
                ApplyFormatting(routesSheet, analysisResult.Routes.Count, new ExportOptions { IncludeFormatting = true });
                ApplyDeviationHighlighting(routesSheet, analysisResult.Routes.ToList());
            }

            await package.SaveAsAsync(new FileInfo(outputPath));
            
            _logger.LogInformation("Экспорт результатов анализа завершен: {OutputPath}", outputPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта анализа в Excel: {OutputPath}", outputPath);
            return false;
        }
    }

    /// <summary>
    /// Создает заголовки столбцов
    /// </summary>
    private void CreateHeaders(ExcelWorksheet worksheet, ExportOptions options)
    {
        var headers = GetColumnHeaders();
        
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cells[1, i + 1];
            cell.Value = headers[i];
            
            if (options.IncludeFormatting)
            {
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(79, 129, 189));
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
        }

        // Автоподбор ширины колонок
        worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
    }

    /// <summary>
    /// Возвращает заголовки колонок (соответствуют Python структуре)
    /// </summary>
    private string[] GetColumnHeaders()
    {
        return new[]
        {
            "Номер маршрута", "Дата маршрута", "Дата поездки", "Табельный машиниста",
            "Серия локомотива", "Номер локомотива", "НЕТТО", "БРУТТО", "ОСИ", "Нагрузка на ось",
            "Наименование участка", "Номер нормы", "Дв. тяга", "Ткм брутто", "Км", "Пр.",
            "Расход фактический", "Расход по норме", "Уд. фактический", "Уд. норма",
            "Отклонение, %", "Статус", "Коэффициент", "USE_RED_COLOR", "USE_RED_RASHOD",
            "Количество дубликатов", "Н=Ф", "Простой с бригадой", "Маневры", "Трогание с места",
            "Нагон опозданий", "Ограничения скорости", "На пересылаемые локомотивы"
        };
    }

    /// <summary>
    /// Заполняет данные маршрутов
    /// </summary>
    private async Task FillRoutesDataAsync(ExcelWorksheet worksheet, List<Route> routes, ExportOptions options)
    {
        await Task.Run(() =>
        {
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var row = i + 2; // +1 для заголовка, +1 для 1-based индексации

                // Основные данные маршрута
                worksheet.Cells[row, 1].Value = route.RouteNumber;
                worksheet.Cells[row, 2].Value = route.RouteDate;
                worksheet.Cells[row, 3].Value = route.TripDate;
                worksheet.Cells[row, 4].Value = route.DriverTab;
                worksheet.Cells[row, 5].Value = route.LocomotiveSeries;
                worksheet.Cells[row, 6].Value = route.LocomotiveNumber;
                worksheet.Cells[row, 7].Value = route.NettoTons;
                worksheet.Cells[row, 8].Value = route.BruttoTons;
                worksheet.Cells[row, 9].Value = route.AxesCount;
                worksheet.Cells[row, 10].Value = route.AxleLoad;
                worksheet.Cells[row, 11].Value = route.SectionName;
                worksheet.Cells[row, 12].Value = route.NormNumber;
                worksheet.Cells[row, 13].Value = route.DoubleTraction;
                worksheet.Cells[row, 14].Value = route.TonKilometers;
                worksheet.Cells[row, 15].Value = route.Kilometers;
                worksheet.Cells[row, 16].Value = route.Pr;
                worksheet.Cells[row, 17].Value = route.FactConsumption;
                worksheet.Cells[row, 18].Value = route.NormConsumption;
                worksheet.Cells[row, 19].Value = route.FactUd;
                worksheet.Cells[row, 20].Value = route.NormaWork;
                worksheet.Cells[row, 21].Value = route.DeviationPercent;
                worksheet.Cells[row, 22].Value = route.Status;
                worksheet.Cells[row, 23].Value = route.Coefficient;
                worksheet.Cells[row, 24].Value = route.UseRedColor;
                worksheet.Cells[row, 25].Value = route.UseRedRashod;
                worksheet.Cells[row, 26].Value = route.DuplicatesCount;
                worksheet.Cells[row, 27].Value = route.NEqualsF;
                
                // Дополнительные составляющие
                worksheet.Cells[row, 28].Value = route.IdleBrigadaTotal;
                worksheet.Cells[row, 29].Value = route.ManevryTotal;
                worksheet.Cells[row, 30].Value = route.StartTotal;
                worksheet.Cells[row, 31].Value = route.DelayTotal;
                worksheet.Cells[row, 32].Value = route.SpeedLimitTotal;
                worksheet.Cells[row, 33].Value = route.TransferLocoTotal;
            }
        });

        _logger.LogDebug("Заполнено {Count} строк данных", routes.Count);
    }

    /// <summary>
    /// Применяет форматирование к листу
    /// </summary>
    private void ApplyFormatting(ExcelWorksheet worksheet, int dataRowCount, ExportOptions options)
    {
        var dataRange = worksheet.Cells[1, 1, dataRowCount + 1, GetColumnHeaders().Length];

        // Границы таблицы
        dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
        dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;

        // Числовое форматирование для числовых колонок
        ApplyNumericFormatting(worksheet, dataRowCount);

        // Автоподбор ширины
        worksheet.Cells.AutoFitColumns(5, 50); // Минимум 5, максимум 50 символов

        _logger.LogDebug("Применено форматирование к {RowCount} строкам", dataRowCount);
    }

    /// <summary>
    /// Применяет числовое форматирование
    /// </summary>
    private void ApplyNumericFormatting(ExcelWorksheet worksheet, int dataRowCount)
    {
        // Колонки с целыми числами
        var intColumns = new[] { 6, 9 }; // Номер локомотива, Оси
        foreach (var col in intColumns)
        {
            worksheet.Cells[2, col, dataRowCount + 1, col].Style.Numberformat.Format = "#,##0";
        }

        // Колонки с десятичными числами (2 знака)
        var decimalColumns = new[] { 7, 8, 10, 14, 15, 17, 18, 19, 20, 21, 23 }; // Веса, расходы, отклонения
        foreach (var col in decimalColumns)
        {
            worksheet.Cells[2, col, dataRowCount + 1, col].Style.Numberformat.Format = "#,##0.00";
        }
    }

    /// <summary>
    /// Применяет подсветку отклонений
    /// Соответствует цветовой схеме Python StatusClassifier
    /// </summary>
    private void ApplyDeviationHighlighting(ExcelWorksheet worksheet, List<Route> routes)
    {
        for (int i = 0; i < routes.Count; i++)
        {
            var route = routes[i];
            var row = i + 2; // +1 для заголовка, +1 для 1-based

            if (!string.IsNullOrEmpty(route.Status) && StatusColors.TryGetValue(route.Status, out var color))
            {
                // Подсвечиваем колонки с отклонением и статусом
                var deviationCell = worksheet.Cells[row, 21]; // Отклонение, %
                var statusCell = worksheet.Cells[row, 22];     // Статус

                deviationCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                deviationCell.Style.Fill.BackgroundColor.SetColor(color);
                
                statusCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                statusCell.Style.Fill.BackgroundColor.SetColor(color);

                // Белый текст для темных фонов
                if (IsColorDark(color))
                {
                    deviationCell.Style.Font.Color.SetColor(Color.White);
                    statusCell.Style.Font.Color.SetColor(Color.White);
                }
            }
        }

        _logger.LogDebug("Применена подсветка отклонений к {Count} строкам", routes.Count);
    }

    /// <summary>
    /// Добавляет лист со статистикой
    /// </summary>
    private void AddStatisticsSheet(ExcelPackage package, List<Route> routes)
    {
        var statsSheet = package.Workbook.Worksheets.Add("Статистика");
        
        // Общая статистика
        statsSheet.Cells["A1"].Value = "Общая статистика анализа";
        statsSheet.Cells["A1"].Style.Font.Bold = true;
        statsSheet.Cells["A1"].Style.Font.Size = 14;

        var currentRow = 3;
        
        // Количественные показатели
        statsSheet.Cells[currentRow, 1].Value = "Общее количество маршрутов:";
        statsSheet.Cells[currentRow, 2].Value = routes.Count;
        currentRow++;

        var validDeviations = routes.Where(r => r.DeviationPercent.HasValue).ToList();
        statsSheet.Cells[currentRow, 1].Value = "Маршрутов с отклонениями:";
        statsSheet.Cells[currentRow, 2].Value = validDeviations.Count;
        currentRow++;

        if (validDeviations.Any())
        {
            statsSheet.Cells[currentRow, 1].Value = "Среднее отклонение, %:";
            statsSheet.Cells[currentRow, 2].Value = Math.Round(validDeviations.Average(r => r.DeviationPercent!.Value), 2);
            currentRow++;

            statsSheet.Cells[currentRow, 1].Value = "Минимальное отклонение, %:";
            statsSheet.Cells[currentRow, 2].Value = validDeviations.Min(r => r.DeviationPercent!.Value);
            currentRow++;

            statsSheet.Cells[currentRow, 1].Value = "Максимальное отклонение, %:";
            statsSheet.Cells[currentRow, 2].Value = validDeviations.Max(r => r.DeviationPercent!.Value);
            currentRow += 2;
        }

        // Статистика по статусам
        if (routes.Any(r => !string.IsNullOrEmpty(r.Status)))
        {
            statsSheet.Cells[currentRow, 1].Value = "Распределение по статусам:";
            statsSheet.Cells[currentRow, 1].Style.Font.Bold = true;
            currentRow++;

            var statusStats = routes
                .Where(r => !string.IsNullOrEmpty(r.Status))
                .GroupBy(r => r.Status)
                .OrderByDescending(g => g.Count())
                .ToList();

            foreach (var group in statusStats)
            {
                statsSheet.Cells[currentRow, 1].Value = group.Key;
                statsSheet.Cells[currentRow, 2].Value = group.Count();
                statsSheet.Cells[currentRow, 3].Value = $"{(double)group.Count() / routes.Count * 100:F1}%";
                currentRow++;
            }
        }

        // Автоподбор ширины колонок
        statsSheet.Cells.AutoFitColumns();
    }

    /// <summary>
    /// Создает сводку анализа
    /// </summary>
    private void CreateAnalysisSummary(ExcelWorksheet worksheet, AnalysisResult analysisResult)
    {
        worksheet.Cells["A1"].Value = $"Анализ участка: {analysisResult.SectionName}";
        worksheet.Cells["A1"].Style.Font.Bold = true;
        worksheet.Cells["A1"].Style.Font.Size = 16;

        var currentRow = 3;

        // Параметры анализа
        worksheet.Cells[currentRow, 1].Value = "Дата анализа:";
        worksheet.Cells[currentRow, 2].Value = analysisResult.CreatedAt.ToString("dd.MM.yyyy HH:mm");
        currentRow++;

        if (!string.IsNullOrEmpty(analysisResult.NormId))
        {
            worksheet.Cells[currentRow, 1].Value = "Норма:";
            worksheet.Cells[currentRow, 2].Value = analysisResult.NormId;
            currentRow++;
        }

        worksheet.Cells[currentRow, 1].Value = "Только один участок:";
        worksheet.Cells[currentRow, 2].Value = analysisResult.SingleSectionOnly ? "Да" : "Нет";
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Использование коэффициентов:";
        worksheet.Cells[currentRow, 2].Value = analysisResult.UseCoefficients ? "Да" : "Нет";
        currentRow += 2;

        // Результаты
        worksheet.Cells[currentRow, 1].Value = "Результаты анализа:";
        worksheet.Cells[currentRow, 1].Style.Font.Bold = true;
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Общее количество маршрутов:";
        worksheet.Cells[currentRow, 2].Value = analysisResult.TotalRoutes;
        currentRow++;

        worksheet.Cells[currentRow, 1].Value = "Проанализировано маршрутов:";
        worksheet.Cells[currentRow, 2].Value = analysisResult.AnalyzedRoutes;
        currentRow++;

        if (analysisResult.AverageDeviation.HasValue)
        {
            worksheet.Cells[currentRow, 1].Value = "Среднее отклонение, %:";
            worksheet.Cells[currentRow, 2].Value = Math.Round(analysisResult.AverageDeviation.Value, 2);
            currentRow++;
        }

        if (analysisResult.CompletedAt.HasValue)
        {
            worksheet.Cells[currentRow, 1].Value = "Время выполнения:";
            var duration = analysisResult.CompletedAt.Value - analysisResult.CreatedAt;
            worksheet.Cells[currentRow, 2].Value = $"{duration.TotalSeconds:F1} сек";
            currentRow++;
        }

        // Автоподбор
        worksheet.Cells.AutoFitColumns();
    }

    /// <summary>
    /// Проверяет является ли цвет темным (для выбора цвета текста)
    /// </summary>
    private static bool IsColorDark(Color color)
    {
        // Формула яркости: (R * 299 + G * 587 + B * 114) / 1000
        var brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000.0;
        return brightness < 128;
    }
}