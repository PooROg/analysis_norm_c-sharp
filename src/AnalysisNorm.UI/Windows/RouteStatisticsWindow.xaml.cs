using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.UI.Commands;

namespace AnalysisNorm.UI.Windows;

/// <summary>
/// CHAT 6: Complete Route Statistics Window Implementation
/// Окно статистики маршрутов - полный аналог Python _show_routes_statistics
/// </summary>
public partial class RouteStatisticsWindow : Window
{
    public RouteStatisticsWindow(RouteStatisticsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseWindow_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

/// <summary>
/// CHAT 6: Complete ViewModel для статистики маршрутов
/// Полный аналог Python routes statistics processing + Excel export
/// </summary>
public class RouteStatisticsViewModel : INotifyPropertyChanged
{
    #region Поля

    private readonly ILogger<RouteStatisticsViewModel> _logger;
    private readonly List<Route> _routes;
    private readonly string _sectionName;
    private readonly bool _singleSectionOnly;

    private string _generalInfo = string.Empty;
    private string _generalStatistics = string.Empty;
    private string _sectionStatistics = string.Empty;
    private string _locomotiveStatistics = string.Empty;
    private string _deviationStatistics = string.Empty;

    #endregion

    #region Конструктор

    public RouteStatisticsViewModel(
        ILogger<RouteStatisticsViewModel> logger,
        List<Route> routes,
        string sectionName,
        bool singleSectionOnly = false)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _sectionName = sectionName;
        _singleSectionOnly = singleSectionOnly;

        // Инициализируем команды
        ExportCommand = new AsyncCommand<object?>(ExportStatisticsAsync);

        // Генерируем статистики
        GenerateAllStatistics();

        _logger.LogInformation("Создана статистика для {Count} маршрутов участка: {SectionName}", 
            _routes.Count, _sectionName);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Общая информация - аналог Python general info header
    /// </summary>
    public string GeneralInfo
    {
        get => _generalInfo;
        private set => SetProperty(ref _generalInfo, value);
    }

    /// <summary>
    /// Общая статистика - аналог Python general statistics
    /// </summary>
    public string GeneralStatistics
    {
        get => _generalStatistics;
        private set => SetProperty(ref _generalStatistics, value);
    }

    /// <summary>
    /// Статистика по участкам - аналог Python sections statistics
    /// </summary>
    public string SectionStatistics
    {
        get => _sectionStatistics;
        private set => SetProperty(ref _sectionStatistics, value);
    }

    /// <summary>
    /// Статистика по локомотивам - аналог Python locomotives statistics
    /// </summary>
    public string LocomotiveStatistics
    {
        get => _locomotiveStatistics;
        private set => SetProperty(ref _locomotiveStatistics, value);
    }

    /// <summary>
    /// Статистика отклонений - аналог Python deviations statistics
    /// </summary>
    public string DeviationStatistics
    {
        get => _deviationStatistics;
        private set => SetProperty(ref _deviationStatistics, value);
    }

    /// <summary>
    /// Команда экспорта статистики
    /// </summary>
    public ICommand ExportCommand { get; }

    #endregion

    #region Private Methods - Generation

    /// <summary>
    /// Генерирует всю статистику - аналог Python statistics generation
    /// </summary>
    private void GenerateAllStatistics()
    {
        try
        {
            GenerateGeneralInfo();
            GenerateGeneralStatistics();
            GenerateSectionStatistics();
            GenerateLocomotiveStatistics();
            GenerateDeviationStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации статистики");
        }
    }

    /// <summary>
    /// Общая информация - аналог Python general info
    /// </summary>
    private void GenerateGeneralInfo()
    {
        var info = new StringBuilder();
        info.AppendLine($"Участок: {_sectionName}");
        info.AppendLine($"Всего маршрутов: {_routes.Count}");
        
        if (_singleSectionOnly)
        {
            info.AppendLine("Фильтр: только маршруты с одним участком");
        }
        
        info.AppendLine($"Период: {GetDateRange()}");
        info.AppendLine($"Сгенерировано: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        GeneralInfo = info.ToString();
    }

    /// <summary>
    /// Общая статистика - аналог Python general statistics calculation
    /// </summary>
    private void GenerateGeneralStatistics()
    {
        var stats = new StringBuilder();
        stats.AppendLine("ОБЩАЯ СТАТИСТИКА");
        stats.AppendLine("=".PadRight(40, '='));
        stats.AppendLine();

        if (!_routes.Any())
        {
            stats.AppendLine("Нет данных для анализа");
            GeneralStatistics = stats.ToString();
            return;
        }

        // Основные показатели - аналог Python basic statistics
        stats.AppendLine($"Общее количество маршрутов: {_routes.Count:N0}");
        stats.AppendLine($"Средний расход электроэнергии: {_routes.Average(r => r.ElectricConsumption):F2} кВт⋅час");
        stats.AppendLine($"Средняя механическая работа: {_routes.Average(r => r.MechanicalWork):F2} кВт⋅час");
        stats.AppendLine($"Средний удельный расход: {_routes.Average(r => r.SpecificConsumption):F3} кВт⋅час/ткм⋅км");
        stats.AppendLine();

        // Статистика отклонений - аналог Python deviation statistics
        var avgDeviation = _routes.Average(r => r.DeviationPercent);
        var maxDeviation = _routes.Max(r => r.DeviationPercent);
        var minDeviation = _routes.Min(r => r.DeviationPercent);
        
        stats.AppendLine($"Среднее отклонение: {avgDeviation:+F2;-F2;0.00}%");
        stats.AppendLine($"Максимальное отклонение: {maxDeviation:+F2;-F2;0.00}%");
        stats.AppendLine($"Минимальное отклонение: {minDeviation:+F2;-F2;0.00}%");
        stats.AppendLine();

        // Распределение по дистанции и времени
        var totalDistance = _routes.Sum(r => r.Distance);
        var avgDistance = _routes.Average(r => r.Distance);
        stats.AppendLine($"Общая дистанция: {totalDistance:F1} км");
        stats.AppendLine($"Средняя дистанция: {avgDistance:F1} км");
        
        var routesWithTime = _routes.Where(r => r.TravelTime.HasValue).ToList();
        if (routesWithTime.Any())
        {
            var avgTime = TimeSpan.FromTicks((long)routesWithTime.Average(r => r.TravelTime!.Value.Ticks));
            stats.AppendLine($"Среднее время в пути: {avgTime:hh\\:mm}");
        }

        GeneralStatistics = stats.ToString();
    }

    /// <summary>
    /// Статистика по участкам - аналог Python sections analysis
    /// </summary>
    private void GenerateSectionStatistics()
    {
        var stats = new StringBuilder();
        stats.AppendLine("СТАТИСТИКА ПО УЧАСТКАМ");
        stats.AppendLine("=".PadRight(40, '='));
        stats.AppendLine();

        var sectionGroups = _routes
            .SelectMany(r => r.SectionNames.Select(s => new { Section = s, Route = r }))
            .GroupBy(x => x.Section)
            .OrderByDescending(g => g.Count())
            .ToList();

        foreach (var group in sectionGroups.Take(10)) // Топ 10 участков
        {
            var routes = group.Select(x => x.Route).ToList();
            var avgDeviation = routes.Average(r => r.DeviationPercent);
            
            stats.AppendLine($"📍 {group.Key}:");
            stats.AppendLine($"   Маршрутов: {routes.Count}");
            stats.AppendLine($"   Среднее отклонение: {avgDeviation:+F1;-F1;0.0}%");
            stats.AppendLine($"   Средний расход: {routes.Average(r => r.ElectricConsumption):F0} кВт⋅час");
            stats.AppendLine();
        }

        if (sectionGroups.Count > 10)
        {
            stats.AppendLine($"... и еще {sectionGroups.Count - 10} участков");
        }

        SectionStatistics = stats.ToString();
    }

    /// <summary>
    /// Статистика по локомотивам - аналог Python locomotives analysis
    /// </summary>
    private void GenerateLocomotiveStatistics()
    {
        var stats = new StringBuilder();
        stats.AppendLine("СТАТИСТИКА ПО ЛОКОМОТИВАМ");
        stats.AppendLine("=".PadRight(40, '='));
        stats.AppendLine();

        // Группировка по сериям - аналог Python locomotive grouping
        var seriesGroups = _routes
            .Where(r => !string.IsNullOrEmpty(r.LocomotiveSeries))
            .GroupBy(r => r.LocomotiveSeries!)
            .OrderByDescending(g => g.Count())
            .ToList();

        stats.AppendLine($"Всего серий локомотивов: {seriesGroups.Count}");
        stats.AppendLine();

        foreach (var group in seriesGroups.Take(15)) // Топ 15 серий
        {
            var avgDeviation = group.Average(r => r.DeviationPercent);
            var locomotiveCount = group.Select(r => r.LocomotiveNumber).Distinct().Count();
            
            stats.AppendLine($"🚂 Серия {group.Key}:");
            stats.AppendLine($"   Маршрутов: {group.Count()}");
            stats.AppendLine($"   Локомотивов: {locomotiveCount}");
            stats.AppendLine($"   Среднее отклонение: {avgDeviation:+F1;-F1;0.0}%");
            stats.AppendLine($"   Средний расход: {group.Average(r => r.ElectricConsumption):F0} кВт⋅час");
            stats.AppendLine();
        }

        LocomotiveStatistics = stats.ToString();
    }

    /// <summary>
    /// Статистика отклонений - аналог Python deviations categorization
    /// </summary>
    private void GenerateDeviationStatistics()
    {
        var stats = new StringBuilder();
        stats.AppendLine("АНАЛИЗ ОТКЛОНЕНИЙ ОТ НОРМ");
        stats.AppendLine("=".PadRight(40, '='));
        stats.AppendLine();

        // Категоризация отклонений - аналог Python deviation categories
        var categories = new[]
        {
            ("Сильная экономия (< -15%)", _routes.Count(r => r.DeviationPercent < -15)),
            ("Средняя экономия (-15% до -10%)", _routes.Count(r => r.DeviationPercent >= -15 && r.DeviationPercent < -10)),
            ("Слабая экономия (-10% до -5%)", _routes.Count(r => r.DeviationPercent >= -10 && r.DeviationPercent < -5)),
            ("В норме (-5% до +5%)", _routes.Count(r => Math.Abs(r.DeviationPercent) <= 5)),
            ("Слабый перерасход (+5% до +10%)", _routes.Count(r => r.DeviationPercent > 5 && r.DeviationPercent <= 10)),
            ("Средний перерасход (+10% до +15%)", _routes.Count(r => r.DeviationPercent > 10 && r.DeviationPercent <= 15)),
            ("Сильный перерасход (> +15%)", _routes.Count(r => r.DeviationPercent > 15))
        };

        foreach (var (category, count) in categories)
        {
            var percentage = _routes.Count > 0 ? (double)count / _routes.Count * 100 : 0;
            var icon = GetCategoryIcon(category);
            
            stats.AppendLine($"{icon} {category}:");
            stats.AppendLine($"   Количество: {count} ({percentage:F1}%)");
            stats.AppendLine();
        }

        // Дополнительный анализ - аналог Python additional insights
        var economyRoutes = _routes.Count(r => r.DeviationPercent < -5);
        var overrunRoutes = _routes.Count(r => r.DeviationPercent > 5);
        var normalRoutes = _routes.Count(r => Math.Abs(r.DeviationPercent) <= 5);

        stats.AppendLine("СВОДКА:");
        stats.AppendLine($"✅ Экономичные маршруты: {economyRoutes} ({(double)economyRoutes / _routes.Count * 100:F1}%)");
        stats.AppendLine($"⚠️  Неэкономичные маршруты: {overrunRoutes} ({(double)overrunRoutes / _routes.Count * 100:F1}%)");
        stats.AppendLine($"📊 Нормальные маршруты: {normalRoutes} ({(double)normalRoutes / _routes.Count * 100:F1}%)");

        DeviationStatistics = stats.ToString();
    }

    #endregion

    #region Export Implementation - CHAT 6 Complete

    /// <summary>
    /// CHAT 6: Полная реализация экспорта статистики - аналог Python export functionality
    /// </summary>
    private async Task ExportStatisticsAsync(object? parameter)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Экспорт статистики",
                Filter = "Excel файлы (*.xlsx)|*.xlsx|Текстовые файлы (*.txt)|*.txt",
                DefaultExt = "xlsx",
                FileName = $"Статистика_{_sectionName?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                await ExportToFile(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта статистики");
            MessageBox.Show(
                $"Ошибка экспорта: {ex.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    /// <summary>
    /// Экспорт в файл - выбор формата
    /// </summary>
    private async Task ExportToFile(string filePath)
    {
        await Task.Run(() =>
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                
                if (extension == ".xlsx")
                {
                    ExportToExcel(filePath);
                }
                else
                {
                    ExportToText(filePath);
                }

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"Статистика успешно экспортирована:\n{filePath}",
                        "Экспорт завершен",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка записи файла статистики");
                throw;
            }
        });
    }

    /// <summary>
    /// CHAT 6: ЗАВЕРШЕНА - Полная реализация экспорта в Excel через EPPlus
    /// </summary>
    private void ExportToExcel(string filePath)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        using var package = new ExcelPackage();
        
        // Создаем листы
        CreateSummarySheet(package);
        CreateDetailedStatisticsSheet(package);
        CreateRoutesDataSheet(package);

        // Сохраняем файл
        package.SaveAs(new FileInfo(filePath));
        
        _logger.LogInformation("Статистика экспортирована в Excel: {FilePath}", filePath);
    }

    /// <summary>
    /// Создает лист сводки
    /// </summary>
    private void CreateSummarySheet(ExcelPackage package)
    {
        var worksheet = package.Workbook.Worksheets.Add("Сводка");
        
        // Заголовок
        worksheet.Cells[1, 1].Value = $"СВОДКА СТАТИСТИКИ: {_sectionName}";
        worksheet.Cells[1, 1].Style.Font.Size = 16;
        worksheet.Cells[1, 1].Style.Font.Bold = true;
        
        // Общая информация
        int row = 3;
        var infoLines = GeneralInfo.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in infoLines)
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2)
            {
                worksheet.Cells[row, 1].Value = parts[0].Trim();
                worksheet.Cells[row, 2].Value = parts[1].Trim();
                worksheet.Cells[row, 1].Style.Font.Bold = true;
            }
            row++;
        }

        // Ключевые метрики
        row += 2;
        worksheet.Cells[row, 1].Value = "КЛЮЧЕВЫЕ ПОКАЗАТЕЛИ";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        row++;

        if (_routes.Any())
        {
            var metrics = new[]
            {
                ("Средний расход", $"{_routes.Average(r => r.ElectricConsumption):F2} кВт⋅час"),
                ("Среднее отклонение", $"{_routes.Average(r => r.DeviationPercent):+F2;-F2;0.00}%"),
                ("Экономичные маршруты", $"{_routes.Count(r => r.DeviationPercent < -5)} ({_routes.Count(r => r.DeviationPercent < -5) * 100.0 / _routes.Count:F1}%)"),
                ("Перерасход", $"{_routes.Count(r => r.DeviationPercent > 5)} ({_routes.Count(r => r.DeviationPercent > 5) * 100.0 / _routes.Count:F1}%)")
            };

            foreach (var (metric, value) in metrics)
            {
                worksheet.Cells[row, 1].Value = metric;
                worksheet.Cells[row, 2].Value = value;
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                row++;
            }
        }

        // Форматирование
        worksheet.Cells.AutoFitColumns(0);
        worksheet.Cells[1, 1, row, 2].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
    }

    /// <summary>
    /// Создает лист детальной статистики
    /// </summary>
    private void CreateDetailedStatisticsSheet(ExcelPackage package)
    {
        var worksheet = package.Workbook.Worksheets.Add("Детальная статистика");
        
        int row = 1;
        
        // Добавляем каждую секцию статистики
        var sections = new[]
        {
            ("ОБЩАЯ СТАТИСТИКА", GeneralStatistics),
            ("ПО УЧАСТКАМ", SectionStatistics),
            ("ПО ЛОКОМОТИВАМ", LocomotiveStatistics),
            ("АНАЛИЗ ОТКЛОНЕНИЙ", DeviationStatistics)
        };

        foreach (var (title, content) in sections)
        {
            worksheet.Cells[row, 1].Value = title;
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.Font.Size = 14;
            row += 2;
            
            var lines = content.Split(Environment.NewLine, StringSplitOptions.None);
            foreach (var line in lines)
            {
                worksheet.Cells[row, 1].Value = line;
                if (line.Contains(":"))
                {
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                }
                row++;
            }
            row += 2;
        }
        
        worksheet.Cells.AutoFitColumns(0);
    }

    /// <summary>
    /// Создает лист с данными маршрутов
    /// </summary>
    private void CreateRoutesDataSheet(ExcelPackage package)
    {
        var worksheet = package.Workbook.Worksheets.Add("Данные маршрутов");
        
        // Заголовки
        var headers = new[] { "№ Маршрута", "Дата", "Участки", "Локомотив", "Расход факт", "Расход норма", "Отклонение %", "Статус" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            worksheet.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
        }

        // Данные
        for (int i = 0; i < _routes.Count; i++)
        {
            var route = _routes[i];
            var row = i + 2;
            
            worksheet.Cells[row, 1].Value = route.RouteNumber;
            worksheet.Cells[row, 2].Value = route.Date.ToString("yyyy-MM-dd");
            worksheet.Cells[row, 3].Value = string.Join(", ", route.SectionNames);
            worksheet.Cells[row, 4].Value = $"{route.LocomotiveSeries} {route.LocomotiveNumber}";
            worksheet.Cells[row, 5].Value = route.ElectricConsumption;
            worksheet.Cells[row, 6].Value = route.NormConsumption;
            worksheet.Cells[row, 7].Value = route.DeviationPercent;
            worksheet.Cells[row, 8].Value = GetDeviationCategory(route.DeviationPercent);
            
            // Форматирование числовых данных
            worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.0";
            worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.0";
            worksheet.Cells[row, 7].Style.Numberformat.Format = "+0.0%;-0.0%;0.0%";
            
            // Цветовое кодирование строки по отклонению
            var color = GetDeviationRowColor(route.DeviationPercent);
            if (color != Color.Transparent)
            {
                for (int col = 1; col <= headers.Length; col++)
                {
                    worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(color);
                }
            }
        }
        
        worksheet.Cells.AutoFitColumns(0);
        
        // Добавляем границы
        var dataRange = worksheet.Cells[1, 1, _routes.Count + 1, headers.Length];
        dataRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
        dataRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
        dataRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
        dataRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
    }

    /// <summary>
    /// Экспорт в текстовый файл - простой формат
    /// </summary>
    private void ExportToText(string filePath)
    {
        var content = new StringBuilder();
        content.AppendLine(GeneralInfo);
        content.AppendLine();
        content.AppendLine(GeneralStatistics);
        content.AppendLine();
        content.AppendLine(SectionStatistics);
        content.AppendLine();
        content.AppendLine(LocomotiveStatistics);
        content.AppendLine();
        content.AppendLine(DeviationStatistics);
        
        File.WriteAllText(filePath, content.ToString(), Encoding.UTF8);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Определяет диапазон дат
    /// </summary>
    private string GetDateRange()
    {
        if (!_routes.Any()) return "Нет данных";
        
        var minDate = _routes.Min(r => r.Date);
        var maxDate = _routes.Max(r => r.Date);
        
        return minDate.Date == maxDate.Date 
            ? minDate.ToString("yyyy-MM-dd") 
            : $"{minDate:yyyy-MM-dd} — {maxDate:yyyy-MM-dd}";
    }

    /// <summary>
    /// Получает иконку для категории
    /// </summary>
    private string GetCategoryIcon(string category)
    {
        return category switch
        {
            var c when c.Contains("Сильная экономия") => "💚",
            var c when c.Contains("Средняя экономия") => "🟢",
            var c when c.Contains("Слабая экономия") => "🟡",
            var c when c.Contains("В норме") => "🔵",
            var c when c.Contains("Слабый перерасход") => "🟠",
            var c when c.Contains("Средний перерасход") => "🔴",
            var c when c.Contains("Сильный перерасход") => "🚨",
            _ => "📊"
        };
    }

    /// <summary>
    /// Определяет категорию отклонения для Excel
    /// </summary>
    private string GetDeviationCategory(double deviationPercent)
    {
        return deviationPercent switch
        {
            < -15 => "Сильная экономия",
            < -10 => "Средняя экономия",
            < -5 => "Слабая экономия",
            >= -5 and <= 5 => "В норме",
            > 5 and <= 10 => "Слабый перерасход",
            > 10 and <= 15 => "Средний перерасход",
            _ => "Сильный перерасход"
        };
    }

    /// <summary>
    /// Определяет цвет строки по отклонению
    /// </summary>
    private Color GetDeviationRowColor(double deviationPercent)
    {
        return deviationPercent switch
        {
            < -15 => Color.FromArgb(200, 0, 100, 0),    // Темно-зеленый
            < -5 => Color.FromArgb(200, 144, 238, 144),  // Светло-зеленый
            >= -5 and <= 5 => Color.Transparent,        // Без подсветки
            > 5 and <= 15 => Color.FromArgb(200, 255, 165, 0),  // Оранжевый
            _ => Color.FromArgb(200, 220, 20, 60)        // Красный
        };
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}