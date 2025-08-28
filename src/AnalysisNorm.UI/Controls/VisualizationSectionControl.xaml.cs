using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace AnalysisNorm.UI.Controls;

/// <summary>
/// UserControl для визуализации и информации - аналог Python VisualizationSection
/// Отображает управляющие кнопки и информацию о графиках
/// </summary>
public partial class VisualizationSectionControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty PlotInfoTextProperty =
        DependencyProperty.Register(nameof(PlotInfoText), typeof(string), typeof(VisualizationSectionControl));

    /// <summary>
    /// Текст информации о графике - аналог Python plot_info content
    /// </summary>
    public string PlotInfoText
    {
        get => (string)GetValue(PlotInfoTextProperty);
        set => SetValue(PlotInfoTextProperty, value);
    }

    #endregion

    public VisualizationSectionControl()
    {
        InitializeComponent();
        
        // Устанавливаем инструкции по умолчанию - аналог Python show_default_instructions
        ShowDefaultInstructions();
    }

    #region Методы обновления информации - аналоги Python методов

    /// <summary>
    /// Показывает инструкции по умолчанию - аналог Python show_default_instructions
    /// </summary>
    public void ShowDefaultInstructions()
    {
        var instructions = new StringBuilder();
        
        instructions.AppendLine("АНАЛИЗАТОР НОРМ С ПОДСЧЕТОМ МАРШРУТОВ");
        instructions.AppendLine("=======================================================");
        instructions.AppendLine();
        instructions.AppendLine("Новые возможности:");
        instructions.AppendLine();
        instructions.AppendLine("1. Подсчет маршрутов по нормам");
        instructions.AppendLine("   • В списке норм отображается количество маршрутов");
        instructions.AppendLine("   • Формат: 'Норма 123 (45 маршрутов)'");
        instructions.AppendLine("   • Обновляется автоматически при смене фильтров");
        instructions.AppendLine();
        instructions.AppendLine("2. Фильтр по одному участку");
        instructions.AppendLine("   • Галка 'Только маршруты с одним участком'");
        instructions.AppendLine("   • Позволяет анализировать только маршруты");
        instructions.AppendLine("     которые проходят только один участок");
        instructions.AppendLine("   • Полезно для 'чистого' анализа норм");
        instructions.AppendLine();
        instructions.AppendLine("3. Динамическое обновление информации");
        instructions.AppendLine("   • Количество маршрутов обновляется при изменении фильтра");
        instructions.AppendLine("   • Отображается общее количество маршрутов участка");
        instructions.AppendLine("   • Показывается количество после фильтрации");
        instructions.AppendLine();
        instructions.AppendLine("Для начала работы:");
        instructions.AppendLine();
        instructions.AppendLine("1. Выберите и загрузите HTML файлы маршрутов");
        instructions.AppendLine("2. Выберите и загрузите HTML файлы норм (опционально)");
        instructions.AppendLine("3. Выберите участок для анализа");
        instructions.AppendLine("4. Настройте фильтр 'только один участок' при необходимости");
        instructions.AppendLine("5. Выберите конкретную норму или оставьте 'Все нормы'");
        instructions.AppendLine("6. Анализируйте результаты на интерактивном графике");
        instructions.AppendLine();
        instructions.AppendLine("ДОПОЛНИТЕЛЬНЫЕ ФУНКЦИИ:");
        instructions.AppendLine("- Подробная информация о нормах (кнопка 'Инфо о норме')");
        instructions.AppendLine("- Фильтрация локомотивов с коэффициентами");
        instructions.AppendLine("- Экспорт в Excel с форматированием");
        instructions.AppendLine("- Интерактивные графики с hover-эффектами");
        instructions.AppendLine("- Расширенная статистика по всем категориям отклонений");
        
        PlotInfoText = instructions.ToString();
    }

    /// <summary>
    /// Обновляет информацию о графике - аналог Python update_plot_info
    /// </summary>
    /// <param name="sectionName">Название участка</param>
    /// <param name="stats">Статистические данные</param>
    /// <param name="normId">ID нормы (если выбрана конкретная)</param>
    /// <param name="singleSectionOnly">Применен ли фильтр одного участка</param>
    public void UpdatePlotInfo(string sectionName, Dictionary<string, object> stats, 
                              string? normId = null, bool singleSectionOnly = false)
    {
        Dispatcher.Invoke(() =>
        {
            var info = new StringBuilder();
            
            // Заголовок с информацией о выбранных параметрах
            info.AppendLine($"АНАЛИЗ УЧАСТКА: {sectionName.ToUpper()}");
            info.AppendLine("=".PadRight(50 + sectionName.Length, '='));
            
            if (!string.IsNullOrEmpty(normId))
                info.AppendLine($"Анализируемая норма: {normId}");
            
            if (singleSectionOnly)
                info.AppendLine("ФИЛЬТР: только маршруты с одним участком");
            
            info.AppendLine();
            
            // Основная статистика
            if (stats.Any())
            {
                info.AppendLine("РЕЗУЛЬТАТЫ АНАЛИЗА:");
                info.AppendLine("-".PadRight(30, '-'));
                
                var total = GetStatValue(stats, "total", 0);
                var processed = GetStatValue(stats, "processed", 0);
                
                info.AppendLine($"Всего маршрутов в участке: {total}");
                info.AppendLine($"Обработано для анализа: {processed}");
                
                if (processed > 0)
                {
                    var economy = GetStatValue(stats, "economy", 0);
                    var normal = GetStatValue(stats, "normal", 0);
                    var overrun = GetStatValue(stats, "overrun", 0);
                    
                    info.AppendLine();
                    info.AppendLine("РАСПРЕДЕЛЕНИЕ ОТКЛОНЕНИЙ:");
                    info.AppendLine($"  Экономия:    {economy,4} ({economy / (double)processed * 100:F1}%)");
                    info.AppendLine($"  В норме:     {normal,4} ({normal / (double)processed * 100:F1}%)");
                    info.AppendLine($"  Перерасход:  {overrun,4} ({overrun / (double)processed * 100:F1}%)");
                    
                    var meanDev = GetStatDoubleValue(stats, "mean_deviation", 0);
                    var medianDev = GetStatDoubleValue(stats, "median_deviation", 0);
                    
                    info.AppendLine();
                    info.AppendLine("СРЕДНИЕ ПОКАЗАТЕЛИ:");
                    info.AppendLine($"  Среднее отклонение:   {meanDev:F2}%");
                    info.AppendLine($"  Медианное отклонение: {medianDev:F2}%");
                }
                
                // Детальная статистика если доступна
                if (stats.ContainsKey("detailed_stats") && 
                    stats["detailed_stats"] is Dictionary<string, int> detailed)
                {
                    info.AppendLine();
                    info.AppendLine("ДЕТАЛЬНАЯ СТАТИСТИКА:");
                    info.AppendLine("-".PadRight(25, '-'));
                    
                    var categories = new Dictionary<string, string>
                    {
                        { "economy_strong", "Экономия сильная (>30%)" },
                        { "economy_medium", "Экономия средняя (20-30%)" },
                        { "economy_weak", "Экономия слабая (5-20%)" },
                        { "normal", "Норма (±5%)" },
                        { "overrun_weak", "Перерасход слабый (5-20%)" },
                        { "overrun_medium", "Перерасход средний (20-30%)" },
                        { "overrun_strong", "Перерасход сильный (>30%)" }
                    };
                    
                    foreach (var (key, name) in categories)
                    {
                        if (detailed.TryGetValue(key, out var count) && count > 0)
                        {
                            var percent = count / (double)processed * 100;
                            info.AppendLine($"  {name}: {count} ({percent:F1}%)");
                        }
                    }
                }
            }
            else
            {
                info.AppendLine("Статистика будет отображена после выполнения анализа.");
                info.AppendLine();
                info.AppendLine("Для получения результатов:");
                info.AppendLine("1. Убедитесь, что загружены HTML файлы маршрутов");
                info.AppendLine("2. Выберите участок для анализа");
                info.AppendLine("3. Нажмите кнопку 'Выполнить анализ'");
            }
            
            info.AppendLine();
            info.AppendLine("=".PadRight(50, '='));
            info.AppendLine($"Обновлено: {DateTime.Now:HH:mm:ss dd.MM.yyyy}");
            
            PlotInfoText = info.ToString();
        });
    }

    /// <summary>
    /// Показывает информацию о хранилище норм - аналог Python norm storage info display
    /// </summary>
    /// <param name="storageInfo">Информация о хранилище</param>
    /// <param name="normStats">Статистика норм</param>
    public void ShowNormStorageInfo(Dictionary<string, object> storageInfo, Dictionary<string, object> normStats)
    {
        Dispatcher.Invoke(() =>
        {
            var info = new StringBuilder();
            
            info.AppendLine("ИНФОРМАЦИЯ О ХРАНИЛИЩЕ НОРМ");
            info.AppendLine("=".PadRight(50, '='));
            info.AppendLine();
            
            info.AppendLine($"Файл хранилища: {GetStatStringValue(storageInfo, "storage_file", "N/A")}");
            info.AppendLine($"Размер файла: {GetStatDoubleValue(storageInfo, "file_size_mb", 0):F2} MB");
            info.AppendLine($"Версия: {GetStatStringValue(storageInfo, "version", "N/A")}");
            info.AppendLine($"Последнее обновление: {GetStatStringValue(storageInfo, "last_updated", "N/A")}");
            info.AppendLine();
            
            info.AppendLine("СТАТИСТИКА НОРМ:");
            info.AppendLine("-".PadRight(30, '-'));
            info.AppendLine($"Всего норм: {GetStatValue(normStats, "total_norms", 0)}");
            info.AppendLine($"Кэшированных функций: {GetStatValue(storageInfo, "cached_functions", 0)}");
            info.AppendLine($"Среднее количество точек на норму: {GetStatDoubleValue(normStats, "avg_points_per_norm", 0):F1}");
            
            if (normStats.ContainsKey("by_type") && normStats["by_type"] is Dictionary<string, int> byType)
            {
                info.AppendLine();
                info.AppendLine("По типам норм:");
                foreach (var (normType, count) in byType)
                {
                    info.AppendLine($"  {normType}: {count}");
                }
            }
            
            PlotInfoText = info.ToString();
        });
    }

    #endregion

    #region Вспомогательные методы извлечения данных

    /// <summary>
    /// Безопасно извлекает целочисленное значение из словаря
    /// </summary>
    private static int GetStatValue(Dictionary<string, object> stats, string key, int defaultValue)
    {
        if (stats.TryGetValue(key, out var value))
        {
            return value switch
            {
                int intValue => intValue,
                double doubleValue => (int)doubleValue,
                long longValue => (int)longValue,
                string stringValue when int.TryParse(stringValue, out var parsed) => parsed,
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    /// <summary>
    /// Безопасно извлекает дробное значение из словаря
    /// </summary>
    private static double GetStatDoubleValue(Dictionary<string, object> stats, string key, double defaultValue)
    {
        if (stats.TryGetValue(key, out var value))
        {
            return value switch
            {
                double doubleValue => doubleValue,
                int intValue => intValue,
                long longValue => longValue,
                float floatValue => floatValue,
                decimal decimalValue => (double)decimalValue,
                string stringValue when double.TryParse(stringValue, out var parsed) => parsed,
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    /// <summary>
    /// Безопасно извлекает строковое значение из словаря
    /// </summary>
    private static string GetStatStringValue(Dictionary<string, object> stats, string key, string defaultValue)
    {
        if (stats.TryGetValue(key, out var value))
        {
            return value?.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    #endregion
}