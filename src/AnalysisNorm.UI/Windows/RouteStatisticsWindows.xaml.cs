    /// <summary>
    /// Генерирует статистику по участкам - аналог Python section-wise stats
    /// </summary>
    private string GenerateSectionStatistics()
    {
        var stats = new StringBuilder();
        
        stats.AppendLine("СТАТИСТИКА ПО УЧАСТКАМ");
        stats.AppendLine("=".PadRight(50, '='));
        stats.AppendLine();
        
        // Группируем маршруты по участкам
        var sectionGroups = _routes
            .SelectMany(r => r.SectionNames.Select(sn => new { Route = r, SectionName = sn }))
            .GroupBy(x => x.SectionName)
            .OrderBy(g => g.Key);
        
        foreach (var sectionGroup in sectionGroups)
        {
            var sectionRoutes = sectionGroup.Select(x => x.Route).ToList();
            
            stats.AppendLine($"УЧАСТОК: {sectionGroup.Key}");
            stats.AppendLine("-".PadRight(30, '-'));
            stats.AppendLine($"Маршрутов: {sectionRoutes.Count}");
            
            if (sectionRoutes.Any(r => r.MechanicalWork > 0))
            {
                var avgMechWork = sectionRoutes.Where(r => r.MechanicalWork > 0).Average(r => r.MechanicalWork);
                var avgElecCons = sectionRoutes.Where(r => r.ElectricConsumption > 0).Average(r => r.ElectricConsumption);
                var avgDeviation = sectionRoutes.Where(r => r.DeviationPercent != 0).Average(r => r.DeviationPercent);
                
                stats.AppendLine($"Средняя мех. работа: {avgMechWork:F0} кВт⋅час");
                stats.AppendLine($"Средний расход: {avgElecCons:F0} кВт⋅час");
                stats.AppendLine($"Среднее отклонение: {avgDeviation:+F1}%");
                
                // Распределение по статусам для участка
                var economyCount = sectionRoutes.Count(r => r.DeviationPercent < -5);
                var normalCount = sectionRoutes.Count(r => Math.Abs(r.DeviationPercent) <= 5);
                var overrunCount = sectionRoutes.Count(r => r.DeviationPercent > 5);
                
                stats.AppendLine("Распределение отклонений:");
                if (economyCount > 0) stats.AppendLine($"  Экономия: {economyCount} ({economyCount / (double)sectionRoutes.Count * 100:F1}%)");
                if (normalCount > 0) stats.AppendLine($"  В норме: {normalCount} ({normalCount / (double)sectionRoutes.Count * 100:F1}%)");
                if (overrunCount > 0) stats.AppendLine($"  Перерасход: {overrunCount} ({overrunCount / (double)sectionRoutes.Count * 100:F1}%)");
            }
            
            stats.AppendLine();
        }
        
        return stats.ToString();
    }

    /// <summary>
    /// Генерирует статистику по локомотивам - аналог Python locomotive-wise stats
    /// </summary>
    private string GenerateLocomotiveStatistics()
    {
        var stats = new StringBuilder();
        
        stats.AppendLine("СТАТИСТИКА ПО ЛОКОМОТИВАМ");
        stats.AppendLine("=".PadRight(50, '='));
        stats.AppendLine();
        
        // Статистика по сериям локомотивов
        var seriesGroups = _routes
            .Where(r => !string.IsNullOrEmpty(r.LocomotiveSeries))
            .GroupBy(r => r.LocomotiveSeries)
            .OrderByDescending(g => g.Count())
            .Take(20); // Топ-20 серий
        
        stats.AppendLine("ТОП-20 СЕРИЙ ПО КОЛИЧЕСТВУ ПОЕЗДОК:");
        stats.AppendLine("-".PadRight(40, '-'));
        
        foreach (var seriesGroup in seriesGroups)
        {
            var seriesRoutes = seriesGroup.ToList();
            
            stats.AppendLine($"СЕРИЯ {seriesGroup.Key}:");
            stats.AppendLine($"  Поездок: {seriesRoutes.Count}");
            stats.AppendLine($"  Уникальных локомотивов: {seriesRoutes.Select(r => r.LocomotiveNumber).Distinct().Count()}");
            
            if (seriesRoutes.Any(r => r.DeviationPercent != 0))
            {
                var deviations = seriesRoutes.Where(r => r.DeviationPercent != 0).Select(r => r.DeviationPercent);
                var avgDeviation = deviations.Average();
                var medianDeviation = GetMedian(deviations.ToList());
                
                stats.AppendLine($"  Среднее отклонение: {avgDeviation:+F1}%");
                stats.AppendLine($"  Медианное отклонение: {medianDeviation:+F1}%");
                
                // Категории отклонений для данной серии
                var economyCount = seriesRoutes.Count(r => r.DeviationPercent < -5);
                var normalCount = seriesRoutes.Count(r => Math.Abs(r.DeviationPercent) <= 5);
                var overrunCount = seriesRoutes.Count(r => r.DeviationPercent > 5);
                
                stats.AppendLine("  Распределение:");
                if (economyCount > 0) stats.AppendLine($"    Экономия: {economyCount} ({economyCount / (double)seriesRoutes.Count * 100:F1}%)");
                if (normalCount > 0) stats.AppendLine($"    В норме: {normalCount} ({normalCount / (double)seriesRoutes.Count * 100:F1}%)");
                if (overrunCount > 0) stats.AppendLine($"    Перерасход: {overrunCount} ({overrunCount / (double)seriesRoutes.Count * 100:F1}%)");
            }
            
            stats.AppendLine();
        }
        
        // Общая статистика по типам локомотивов
        stats.AppendLine("СТАТИСТИКА ПО ТИПАМ:");
        stats.AppendLine("-".PadRight(25, '-'));
        
        var typeGroups = _routes
            .Where(r => !string.IsNullOrEmpty(r.LocomotiveType))
            .GroupBy(r => r.LocomotiveType)
            .OrderByDescending(g => g.Count());
        
        foreach (var typeGroup in typeGroups)
        {
            var typeRoutes = typeGroup.ToList();
            var avgDeviation = typeRoutes.Where(r => r.DeviationPercent != 0).Average(r => r.DeviationPercent);
            
            stats.AppendLine($"{typeGroup.Key}: {typeRoutes.Count} поездок, среднее отклонение {avgDeviation:+F1}%");
        }
        
        return stats.ToString();
    }using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.UI.Commands;

namespace AnalysisNorm.UI.Windows;

/// <summary>
/// Окно статистики маршрутов - аналог Python _show_routes_statistics
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
/// ViewModel для статистики маршрутов - аналог Python routes statistics processing
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

    /// <summary>
    /// Конструктор ViewModel статистики
    /// </summary>
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
        ExportStatisticsCommand = new AsyncCommand(ExportStatisticsAsync);

        // Генерируем статистику
        GenerateAllStatistics();
    }

    #endregion

    #region Properties

    public string GeneralInfo
    {
        get => _generalInfo;
        private set => SetProperty(ref _generalInfo, value);
    }

    public string GeneralStatistics
    {
        get => _generalStatistics;
        private set => SetProperty(ref _generalStatistics, value);
    }

    public string SectionStatistics
    {
        get => _sectionStatistics;
        private set => SetProperty(ref _sectionStatistics, value);
    }

    public string LocomotiveStatistics
    {
        get => _locomotiveStatistics;
        private set => SetProperty(ref _locomotiveStatistics, value);
    }

    public string DeviationStatistics
    {
        get => _deviationStatistics;
        private set => SetProperty(ref _deviationStatistics, value);
    }

    public ICommand ExportStatisticsCommand { get; }

    #endregion

    #region Генерация статистики - аналоги Python statistics methods

    /// <summary>
    /// Генерирует всю статистику - аналог Python comprehensive statistics
    /// </summary>
    private void GenerateAllStatistics()
    {
        try
        {
            var filterSuffix = _singleSectionOnly ? " [фильтр: один участок]" : "";
            GeneralInfo = $"Анализ участка: {_sectionName}{filterSuffix} | Маршрутов: {_routes.Count} | Обновлено: {DateTime.Now:HH:mm dd.MM.yyyy}";

            GeneralStatistics = GenerateGeneralStatistics();
            SectionStatistics = GenerateSectionStatistics();
            LocomotiveStatistics = GenerateLocomotiveStatistics();
            DeviationStatistics = GenerateDeviationStatistics();

            _logger.LogInformation("Статистика сгенерирована для {RouteCount} маршрутов", _routes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка генерации статистики");
        }
    }

    /// <summary>
    /// Генерирует общую статистику - аналог Python general stats
    /// </summary>
    private string GenerateGeneralStatistics()
    {
        var stats = new StringBuilder();
        
        stats.AppendLine("ОБЩАЯ СТАТИСТИКА МАРШРУТОВ");
        stats.AppendLine("=".PadRight(50, '='));
        stats.AppendLine();
        
        // Основные показатели
        stats.AppendLine($"Всего маршрутов: {_routes.Count}");
        stats.AppendLine($"Уникальных номеров: {_routes.Select(r => r.RouteNumber).Distinct().Count()}");
        stats.AppendLine();
        
        // Временной диапазон
        if (_routes.Any(r => r.TripDate != default))
        {
            var minDate = _routes.Where(r => r.TripDate != default).Min(r => r.TripDate);
            var maxDate = _routes.Where(r => r.TripDate != default).Max(r => r.TripDate);
            stats.AppendLine($"Период данных: {minDate:dd.MM.yyyy} - {maxDate:dd.MM.yyyy}");
            stats.AppendLine();
        }
        
        // Энергетические показатели
        if (_routes.Any(r => r.MechanicalWork > 0))
        {
            stats.AppendLine("ЭНЕРГЕТИЧЕСКИЕ ПОКАЗАТЕЛИ:");
            stats.AppendLine("-".PadRight(30, '-'));
            
            var mechWorkValues = _routes.Where(r => r.MechanicalWork > 0).Select(r => r.MechanicalWork);
            var elecConsValues = _routes.Where(r => r.ElectricConsumption > 0).Select(r => r.ElectricConsumption);
            var deviationValues = _routes.Where(r => r.DeviationPercent != 0).Select(r => r.DeviationPercent);
            
            stats.AppendLine($"Механическая работа:");
            stats.AppendLine($"  Среднее: {mechWorkValues.Average():F0} кВт⋅час");
            stats.AppendLine($"  Диапазон: {mechWorkValues.Min():F0} - {mechWorkValues.Max():F0} кВт⋅час");
            stats.AppendLine();
            
            stats.AppendLine($"Расход электроэнергии:");
            stats.AppendLine($"  Среднее: {elecConsValues.Average():F0} кВт⋅час");
            stats.AppendLine($"  Диапазон: {elecConsValues.Min():F0} - {elecConsValues.Max():F0} кВт⋅час");
            stats.AppendLine();
            
            if (deviationValues.Any())
            {
                stats.AppendLine($"Отклонение от норм:");
                stats.AppendLine($"  Среднее: {deviationValues.Average():+F1}%");
                stats.AppendLine($"  Медиана: {GetMedian(deviationValues.ToList()):+F1}%");
                stats.AppendLine($"  Диапазон: {deviationValues.Min():+F1}% до {deviationValues.Max():+F1}%");
            }
        }
        
        return stats.ToString();
    }

    /// <summary>
    /// Генерирует статистику по участкам - аналог Python section-wise stats
    /// </summary>
    private string GenerateSectionStatistics()
    {
        var stats = new StringBuilder();
        
        stats.AppendLine("СТАТИСТИКА ПО УЧАСТКАМ");
        stats.AppendLine("=".PadRight(50, '='));
        stats.AppendLine();
        
        // Группируем маршруты по участкам
        var sectionGroups = _routes
            .SelectMany(r => r.SectionNames.Select(sn => new { Route = r, SectionName = sn }))
            .GroupBy(x => x.SectionName)
            .OrderBy(g => g.Key);
        
        foreach (var sectionGroup in sectionGroups)
        {
            var sectionRoutes = sectionGroup.Select(x => x.Route).ToList();
            
            stats.AppendLine($"УЧАСТОК: {sectionGroup.Key}");
            stats.AppendLine("-".PadRight(30, '-'));
            stats.AppendLine($"Маршрутов: {sectionRoutes.Count}");
            
            if (sectionRoutes.Any(r => r.MechanicalWork > 0))
            {
                var avgMechWork = sectionRoutes.Where(r => r.MechanicalWork > 0).Average(r => r.MechanicalWork);
                var avgElecCons = sectionRoutes.Where(r => r.ElectricConsumption > 0).Average(r => r.ElectricConsumption);
                var avgDeviation = sectionRoutes.Where(r => r.DeviationPercent != 0).Average(r => r.DeviationPercent);
                
                stats.AppendLine($"Средняя мех. работа: {avgMechWork:F0} кВт⋅час");
                stats.AppendLine($"Средний расход: {avgElecCons:F0} кВт⋅час");
                stats.AppendLine($"Среднее отклонение: {avgDeviation:+F1}%");
            }
            
            stats.AppendLine();
        }
        
        return stats.ToString();
    }

    /// <summary>
    /// Генерирует статистику по локомотивам - аналог Python locomotive-wise stats
    /// </summary>
    private string GenerateLocomotiveStatistics()
    {
        var stats = new StringBuilder();
        
        stats.AppendLine("СТАТИСТИКА ПО ЛОКОМОТИВАМ");
        stats.AppendLine("=".PadRight(50, '='));
        stats.AppendLine();
        
        // Группируем по сериям локомотивов
        var seriesGroups = _routes
            .Where(r => !string.IsNullOrEmpty(r.LocomotiveSeries))
            .GroupBy(r => r.LocomotiveSeries)
            .OrderByDescending(g => g.Count())
            .Take(20); // Топ-20 серий
        
        foreach (var seriesGroup in seriesGroups)
        {
            var seriesRoutes = seriesGroup.ToList();
            
            stats.AppendLine($"СЕРИЯ: {seriesGroup.Key}");
            stats.AppendLine("-".PadRight(25, '-'));
            stats.AppendLine($"Поездок: {seriesRoutes.Count}");
            
            if (seriesRoutes.Any(r => r.DeviationPercent != 0))
            {
                var deviations = seriesRoutes.Where(r => r.DeviationPercent != 0).Select(r => r.DeviationPercent);
                var avgDeviation = deviations.Average();
                
                stats.AppendLine($"Среднее отклонение: {avgDeviation:+F1}%");
                
                // Категории отклонений для данной серии
                var economyCount = seriesRoutes.Count(r => r.DeviationPercent < -5);
                var normalCount = seriesRoutes.Count(r => Math.Abs(r.DeviationPercent) <= 5);
                var overrunCount = seriesRoutes.Count(r => r.DeviationPercent > 5);
                
                if (economyCount > 0) stats.AppendLine($"  Экономия: {economyCount} ({economyCount / (double)seriesRoutes.Count * 100:F1}%)");
                if (normalCount > 0) stats.AppendLine($"  В норме: {normalCount} ({normalCount / (double)seriesRoutes.Count * 100:F1}%)");
                if (overrunCount > 0) stats.AppendLine($"  Перерасход: {overrunCount} ({overrunCount / (double)seriesRoutes.Count * 100:F1}%)");
            }
            
            stats.AppendLine();
        }
        
        return stats.ToString();
    }

    /// <summary>
    /// Генерирует статистику отклонений - аналог Python detailed deviation stats
    /// </summary>
    private string GenerateDeviationStatistics()
    {
        var stats = new StringBuilder();
        
        stats.AppendLine("ДЕТАЛЬНАЯ СТАТИСТИКА ОТКЛОНЕНИЙ");
        stats.AppendLine("=".PadRight(50, '='));
        stats.AppendLine();
        
        var routesWithDeviations = _routes.Where(r => r.DeviationPercent != 0).ToList();
        
        if (!routesWithDeviations.Any())
        {
            stats.AppendLine("Нет данных об отклонениях для анализа.");
            return stats.ToString();
        }
        
        // Категории отклонений - аналог Python StatusClassifier categories
        var categories = new Dictionary<string, (Func<double, bool> condition, string description)>
        {
            { "economy_strong", (d => d < -30, "Экономия сильная (< -30%)") },
            { "economy_medium", (d => d >= -30 && d < -20, "Экономия средняя (-30% до -20%)") },
            { "economy_weak", (d => d >= -20 && d < -5, "Экономия слабая (-20% до -5%)") },
            { "normal", (d => d >= -5 && d <= 5, "В норме (-5% до +5%)") },
            { "overrun_weak", (d => d > 5 && d <= 20, "Перерасход слабый (+5% до +20%)") },
            { "overrun_medium", (d => d > 20 && d <= 30, "Перерасход средний (+20% до +30%)") },
            { "overrun_strong", (d => d > 30, "Перерасход сильный (> +30%)") }
        };

        var totalRoutes = routesWithDeviations.Count;
        
        foreach (var (key, (condition, description)) in categories)
        {
            var categoryRoutes = routesWithDeviations.Where(r => condition(r.DeviationPercent)).ToList();
            
            if (categoryRoutes.Any())
            {
                var percentage = categoryRoutes.Count / (double)totalRoutes * 100;
                var avgDeviation = categoryRoutes.Average(r => r.DeviationPercent);
                
                stats.AppendLine($"{description}:");
                stats.AppendLine($"  Количество: {categoryRoutes.Count} ({percentage:F1}%)");
                stats.AppendLine($"  Среднее отклонение: {avgDeviation:+F1}%");
                
                // Топ маршрутов в категории (по отклонению)
                var topRoutes = categoryRoutes
                    .OrderBy(r => Math.Abs(r.DeviationPercent))
                    .Take(3);
                    
                stats.AppendLine("  Примеры маршрутов:");
                foreach (var route in topRoutes)
                {
                    stats.AppendLine($"    {route.RouteNumber}: {route.DeviationPercent:+F1}%");
                }
                
                stats.AppendLine();
            }
        }
        
        // Общие показатели отклонений
        stats.AppendLine("ОБЩИЕ ПОКАЗАТЕЛИ ОТКЛОНЕНИЙ:");
        stats.AppendLine("-".PadRight(35, '-'));
        
        var allDeviations = routesWithDeviations.Select(r => r.DeviationPercent).ToList();
        var economyRoutes = allDeviations.Count(d => d < -5);
        var normalRoutes = allDeviations.Count(d => Math.Abs(d) <= 5);
        var overrunRoutes = allDeviations.Count(d => d > 5);
        
        stats.AppendLine($"Всего с отклонениями: {totalRoutes}");
        stats.AppendLine($"Экономия (< -5%): {economyRoutes} ({economyRoutes / (double)totalRoutes * 100:F1}%)");
        stats.AppendLine($"В норме (±5%): {normalRoutes} ({normalRoutes / (double)totalRoutes * 100:F1}%)");
        stats.AppendLine($"Перерасход (> +5%): {overrunRoutes} ({overrunRoutes / (double)totalRoutes * 100:F1}%)");
        stats.AppendLine();
        stats.AppendLine($"Среднее отклонение: {allDeviations.Average():+F2}%");
        stats.AppendLine($"Медианное отклонение: {GetMedian(allDeviations):+F2}%");
        stats.AppendLine($"Стандартное отклонение: {GetStandardDeviation(allDeviations):F2}%");
        
        return stats.ToString();
    }

    #endregion

    #region Вспомогательные математические методы

    /// <summary>
    /// Вычисляет медиану списка значений
    /// </summary>
    private double GetMedian(List<double> values)
    {
        if (!values.Any()) return 0;
        
        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;
        
        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        
        return sorted[count / 2];
    }

    /// <summary>
    /// Вычисляет стандартное отклонение
    /// </summary>
    private double GetStandardDeviation(List<double> values)
    {
        if (!values.Any()) return 0;
        
        var average = values.Average();
        var sumOfSquaredDiffs = values.Sum(v => Math.Pow(v - average, 2));
        
        return Math.Sqrt(sumOfSquaredDiffs / values.Count);
    }

    #endregion

    #region Commands

    /// <summary>
    /// Экспорт статистики в Excel - аналог Python export functionality
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
                FileName = $"Статистика_{_sectionName}_{DateTime.Now:yyyyMMdd_HHmmss}"
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
    /// Экспорт в файл
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

    private void ExportToExcel(string filePath)
    {
        // TODO: Реализация экспорта в Excel через EPPlus
        // Пока экспортируем как текст
        ExportToText(filePath.Replace(".xlsx", ".txt"));
    }

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