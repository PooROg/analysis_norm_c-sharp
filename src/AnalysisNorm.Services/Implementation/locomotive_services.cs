using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeOpenXml;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Data;
using Microsoft.EntityFrameworkCore;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// Сервис управления коэффициентами локомотивов
/// Соответствует LocomotiveCoefficientsManager из Python coefficients.py
/// </summary>
public class LocomotiveCoefficientService : ILocomotiveCoefficientService
{
    private readonly ILogger<LocomotiveCoefficientService> _logger;
    private readonly AnalysisNormDbContext _context;
    private readonly ITextNormalizer _textNormalizer;
    private readonly ApplicationSettings _settings;

    public LocomotiveCoefficientService(
        ILogger<LocomotiveCoefficientService> logger,
        AnalysisNormDbContext context,
        ITextNormalizer textNormalizer,
        IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _context = context;
        _textNormalizer = textNormalizer;
        _settings = settings.Value;
    }

    /// <summary>
    /// Загружает коэффициенты из Excel файла
    /// Соответствует load_from_excel из Python LocomotiveCoefficientsManager
    /// </summary>
    public async Task<bool> LoadCoefficientsAsync(string filePath, double minWorkThreshold = 0.0)
    {
        _logger.LogInformation("Загружаем коэффициенты локомотивов из файла: {FilePath}", filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogError("Файл коэффициентов не найден: {FilePath}", filePath);
                return false;
            }

            // Настройка EPPlus (отключаем телеметрию)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var coefficients = new List<LocomotiveCoefficient>();
            var processedCount = 0;
            var skippedCount = 0;

            using var package = new ExcelPackage(new FileInfo(filePath));
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            
            if (worksheet == null)
            {
                _logger.LogError("В Excel файле не найдены листы");
                return false;
            }

            _logger.LogDebug("Обрабатываем лист: {WorksheetName}, строк: {RowCount}", 
                worksheet.Name, worksheet.Dimension?.Rows ?? 0);

            // Определяем структуру файла (ищем заголовки)
            var headerInfo = FindHeaders(worksheet);
            if (headerInfo == null)
            {
                _logger.LogError("Не удалось определить структуру Excel файла");
                return false;
            }

            // Обрабатываем строки с данными
            var startRow = headerInfo.HeaderRow + 1;
            var endRow = worksheet.Dimension?.Rows ?? 0;

            for (int row = startRow; row <= endRow; row++)
            {
                try
                {
                    var coefficient = ParseCoefficientRow(worksheet, row, headerInfo, minWorkThreshold);
                    if (coefficient != null)
                    {
                        coefficients.Add(coefficient);
                        processedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ошибка обработки строки {Row} в файле коэффициентов", row);
                    skippedCount++;
                }
            }

            // Сохраняем в базу данных
            await SaveCoefficientsToDatabase(coefficients);

            _logger.LogInformation("Загрузка коэффициентов завершена: обработано {ProcessedCount}, пропущено {SkippedCount}", 
                processedCount, skippedCount);

            return processedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка загрузки коэффициентов из файла {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Находит заголовки в Excel файле
    /// </summary>
    private ExcelHeaderInfo? FindHeaders(ExcelWorksheet worksheet)
    {
        var headerInfo = new ExcelHeaderInfo();
        
        // Ищем строку с заголовками (обычно первые 5 строк)
        for (int row = 1; row <= Math.Min(5, worksheet.Dimension?.Rows ?? 0); row++)
        {
            var rowText = string.Join(" ", Enumerable.Range(1, worksheet.Dimension?.Columns ?? 0)
                .Select(col => worksheet.Cells[row, col].Text?.ToLower() ?? string.Empty));

            if (rowText.Contains("серия") && rowText.Contains("номер") && rowText.Contains("коэффициент"))
            {
                headerInfo.HeaderRow = row;
                
                // Находим позиции колонок
                for (int col = 1; col <= worksheet.Dimension?.Columns; col++)
                {
                    var cellText = worksheet.Cells[row, col].Text?.ToLower() ?? string.Empty;
                    
                    if (cellText.Contains("серия"))
                        headerInfo.SeriesColumn = col;
                    else if (cellText.Contains("номер"))
                        headerInfo.NumberColumn = col;
                    else if (cellText.Contains("коэффициент"))
                        headerInfo.CoefficientColumn = col;
                    else if (cellText.Contains("работ") && cellText.Contains("факт"))
                        headerInfo.WorkFactColumn = col;
                    else if (cellText.Contains("работ") && cellText.Contains("норм"))
                        headerInfo.WorkNormColumn = col;
                }

                if (headerInfo.SeriesColumn > 0 && headerInfo.NumberColumn > 0 && headerInfo.CoefficientColumn > 0)
                {
                    _logger.LogDebug("Найдены заголовки в строке {Row}: Серия={SeriesCol}, Номер={NumberCol}, Коэффициент={CoeffCol}", 
                        row, headerInfo.SeriesColumn, headerInfo.NumberColumn, headerInfo.CoefficientColumn);
                    return headerInfo;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Парсит строку с коэффициентом из Excel
    /// </summary>
    private LocomotiveCoefficient? ParseCoefficientRow(ExcelWorksheet worksheet, int row, ExcelHeaderInfo headerInfo, double minWorkThreshold)
    {
        try
        {
            var series = worksheet.Cells[row, headerInfo.SeriesColumn].Text?.Trim();
            var numberText = worksheet.Cells[row, headerInfo.NumberColumn].Text?.Trim();
            var coefficientText = worksheet.Cells[row, headerInfo.CoefficientColumn].Text?.Trim();

            // Проверяем обязательные поля
            if (string.IsNullOrEmpty(series) || string.IsNullOrEmpty(numberText) || string.IsNullOrEmpty(coefficientText))
            {
                return null;
            }

            var number = _textNormalizer.SafeInt(numberText);
            var coefficient = _textNormalizer.SafeDecimal(coefficientText);
            
            if (number <= 0 || coefficient <= 0)
            {
                return null;
            }

            var locomotiveCoeff = new LocomotiveCoefficient
            {
                Series = series,
                Number = number,
                Coefficient = coefficient,
                CreatedAt = DateTime.UtcNow
            };

            // Дополнительные поля если есть
            if (headerInfo.WorkFactColumn > 0)
            {
                var workFactText = worksheet.Cells[row, headerInfo.WorkFactColumn].Text?.Trim();
                locomotiveCoeff.WorkFact = _textNormalizer.SafeDecimal(workFactText);
            }

            if (headerInfo.WorkNormColumn > 0)
            {
                var workNormText = worksheet.Cells[row, headerInfo.WorkNormColumn].Text?.Trim();
                locomotiveCoeff.WorkNorm = _textNormalizer.SafeDecimal(workNormText);
            }

            // Фильтруем по минимальной работе (как в Python)
            if (minWorkThreshold > 0 && locomotiveCoeff.WorkFact.HasValue)
            {
                if ((double)locomotiveCoeff.WorkFact.Value < minWorkThreshold)
                {
                    _logger.LogTrace("Пропущен локомотив {Series}-{Number}: работа {Work} ниже порога {Threshold}", 
                        series, number, locomotiveCoeff.WorkFact, minWorkThreshold);
                    return null;
                }
            }

            // Обновляем вычисляемые поля
            locomotiveCoeff.UpdateCalculatedFields();

            return locomotiveCoeff;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка парсинга строки {Row} коэффициентов", row);
            return null;
        }
    }

    /// <summary>
    /// Сохраняет коэффициенты в базу данных
    /// </summary>
    private async Task SaveCoefficientsToDatabase(List<LocomotiveCoefficient> coefficients)
    {
        if (!coefficients.Any()) return;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Удаляем существующие коэффициенты
            var existingSeries = coefficients.Select(c => c.SeriesNormalized).Distinct().ToList();
            var existingCoefficients = await _context.LocomotiveCoefficients
                .Where(lc => existingSeries.Contains(lc.SeriesNormalized))
                .ToListAsync();

            if (existingCoefficients.Any())
            {
                _context.LocomotiveCoefficients.RemoveRange(existingCoefficients);
                _logger.LogDebug("Удалено {Count} существующих коэффициентов", existingCoefficients.Count);
            }

            // Добавляем новые коэффициенты
            await _context.LocomotiveCoefficients.AddRangeAsync(coefficients);
            await _context.SaveChangesAsync();
            
            await transaction.CommitAsync();
            
            _logger.LogInformation("Сохранено {Count} коэффициентов в базу данных", coefficients.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Ошибка сохранения коэффициентов в базу данных");
            throw;
        }
    }

    /// <summary>
    /// Получает коэффициент для локомотива
    /// Соответствует get_coefficient из Python
    /// </summary>
    public async Task<decimal> GetCoefficientAsync(string series, int number)
    {
        try
        {
            var normalizedSeries = LocomotiveCoefficient.NormalizeSeries(series);
            
            var coefficient = await _context.LocomotiveCoefficients
                .Where(lc => lc.SeriesNormalized == normalizedSeries && lc.Number == number)
                .Select(lc => lc.Coefficient)
                .FirstOrDefaultAsync();

            if (coefficient.HasValue)
            {
                _logger.LogTrace("Найден коэффициент для {Series}-{Number}: {Coefficient}", 
                    series, number, coefficient.Value);
                return coefficient.Value;
            }

            _logger.LogTrace("Коэффициент для {Series}-{Number} не найден, используем 1.0", series, number);
            return 1.0m; // Коэффициент по умолчанию (как в Python)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения коэффициента для {Series}-{Number}", series, number);
            return 1.0m;
        }
    }

    /// <summary>
    /// Получает статистику коэффициентов
    /// </summary>
    public async Task<CoefficientStatistics> GetStatisticsAsync()
    {
        try
        {
            var coefficients = await _context.LocomotiveCoefficients.ToListAsync();
            
            if (!coefficients.Any())
            {
                return new CoefficientStatistics
                {
                    TotalCount = 0,
                    SeriesCount = 0,
                    AverageCoefficient = 1.0m
                };
            }

            var stats = new CoefficientStatistics
            {
                TotalCount = coefficients.Count,
                SeriesCount = coefficients.Select(c => c.SeriesNormalized).Distinct().Count(),
                AverageCoefficient = coefficients.Average(c => c.Coefficient),
                MinCoefficient = coefficients.Min(c => c.Coefficient),
                MaxCoefficient = coefficients.Max(c => c.Coefficient),
                SeriesBreakdown = coefficients
                    .GroupBy(c => c.SeriesNormalized)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статистики коэффициентов");
            return new CoefficientStatistics();
        }
    }

    /// <summary>
    /// Применяет коэффициенты к маршрутам
    /// Соответствует apply_coefficients из Python CoefficientsApplier
    /// </summary>
    public async Task ApplyCoefficientsAsync(IEnumerable<Route> routes)
    {
        var routesList = routes.ToList();
        if (!routesList.Any()) return;

        _logger.LogDebug("Применяем коэффициенты к {Count} маршрутам", routesList.Count);

        var appliedCount = 0;
        var notFoundCount = 0;

        foreach (var route in routesList)
        {
            if (string.IsNullOrEmpty(route.LocomotiveSeries) || !route.LocomotiveNumber.HasValue)
            {
                notFoundCount++;
                continue;
            }

            var coefficient = await GetCoefficientAsync(route.LocomotiveSeries, route.LocomotiveNumber.Value);
            
            if (coefficient != 1.0m) // Найден коэффициент отличный от дефолтного
            {
                // Сохраняем оригинальное значение
                route.FactUdOriginal = route.FactUd;
                route.Coefficient = coefficient;
                
                // Применяем коэффициент (как в Python)
                if (route.FactUd.HasValue)
                {
                    route.FactUd = route.FactUd.Value * coefficient;
                }

                if (route.FactConsumption.HasValue)
                {
                    route.FactConsumption = route.FactConsumption.Value * coefficient;
                }

                // Пересчитываем отклонение
                if (route.NormConsumption.HasValue && route.NormConsumption.Value > 0)
                {
                    var deviation = (route.FactConsumption!.Value - route.NormConsumption.Value) / route.NormConsumption.Value * 100;
                    route.DeviationPercent = Math.Round(deviation, 2);
                    route.Status = DeviationStatus.GetStatus(deviation);
                }

                appliedCount++;
            }
            else
            {
                notFoundCount++;
            }
        }

        _logger.LogDebug("Коэффициенты применены: {AppliedCount} успешно, {NotFoundCount} не найдены", 
            appliedCount, notFoundCount);
    }
}

/// <summary>
/// Сервис фильтрации локомотивов
/// Соответствует LocomotiveFilter из Python filter.py
/// </summary>
public class LocomotiveFilterService : ILocomotiveFilterService
{
    private readonly ILogger<LocomotiveFilterService> _logger;
    private readonly AnalysisNormDbContext _context;

    public LocomotiveFilterService(ILogger<LocomotiveFilterService> logger, AnalysisNormDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Создает фильтр на основе маршрутов
    /// Соответствует созданию фильтра в Python LocomotiveFilter
    /// </summary>
    public LocomotiveFilter CreateFilter(IEnumerable<Route> routes)
    {
        var routesList = routes.ToList();
        _logger.LogDebug("Создаем фильтр локомотивов на основе {Count} маршрутов", routesList.Count);

        var locomotives = routesList
            .Where(r => !string.IsNullOrEmpty(r.LocomotiveSeries) && r.LocomotiveNumber.HasValue)
            .Select(r => (Series: r.LocomotiveSeries!, Number: r.LocomotiveNumber!.Value))
            .Distinct()
            .OrderBy(l => l.Series)
            .ThenBy(l => l.Number)
            .ToList();

        _logger.LogDebug("Найдено {Count} уникальных локомотивов", locomotives.Count);

        return new LocomotiveFilter
        {
            SelectedLocomotives = locomotives,
            UseCoefficients = false,
            ExcludeLowWork = false
        };
    }

    /// <summary>
    /// Фильтрует маршруты по выбранным локомотивам
    /// </summary>
    public IEnumerable<Route> FilterRoutes(IEnumerable<Route> routes, LocomotiveFilter filter)
    {
        var routesList = routes.ToList();
        if (!filter.SelectedLocomotives.Any())
        {
            _logger.LogDebug("Фильтр пуст, возвращаем все маршруты");
            return routesList;
        }

        var selectedSet = filter.SelectedLocomotives.ToHashSet();
        
        var filteredRoutes = routesList.Where(route =>
        {
            if (string.IsNullOrEmpty(route.LocomotiveSeries) || !route.LocomotiveNumber.HasValue)
                return false;

            return selectedSet.Contains((route.LocomotiveSeries, route.LocomotiveNumber.Value));
        }).ToList();

        _logger.LogDebug("Фильтрация завершена: {FilteredCount}/{TotalCount} маршрутов", 
            filteredRoutes.Count, routesList.Count);

        return filteredRoutes;
    }

    /// <summary>
    /// Получает локомотивы сгруппированные по сериям
    /// </summary>
    public async Task<Dictionary<string, List<int>>> GetLocomotivesBySeriesAsync()
    {
        try
        {
            var locomotives = await _context.Routes
                .Where(r => !string.IsNullOrEmpty(r.LocomotiveSeries) && r.LocomotiveNumber.HasValue)
                .Select(r => new { r.LocomotiveSeries, r.LocomotiveNumber })
                .Distinct()
                .ToListAsync();

            var result = locomotives
                .Where(l => l.LocomotiveSeries != null && l.LocomotiveNumber.HasValue)
                .GroupBy(l => l.LocomotiveSeries!)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(l => l.LocomotiveNumber!.Value).OrderBy(n => n).ToList()
                );

            _logger.LogDebug("Получено {SeriesCount} серий локомотивов", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения списка локомотивов по сериям");
            return new Dictionary<string, List<int>>();
        }
    }
}

/// <summary>
/// Информация о заголовках в Excel файле
/// </summary>
public class ExcelHeaderInfo
{
    public int HeaderRow { get; set; }
    public int SeriesColumn { get; set; }
    public int NumberColumn { get; set; }
    public int CoefficientColumn { get; set; }
    public int WorkFactColumn { get; set; }
    public int WorkNormColumn { get; set; }
}

/// <summary>
/// Статистика коэффициентов
/// </summary>
public record CoefficientStatistics
{
    public int TotalCount { get; init; }
    public int SeriesCount { get; init; }
    public decimal AverageCoefficient { get; init; } = 1.0m;
    public decimal MinCoefficient { get; init; } = 1.0m;
    public decimal MaxCoefficient { get; init; } = 1.0m;
    public Dictionary<string, int> SeriesBreakdown { get; init; } = new();
}