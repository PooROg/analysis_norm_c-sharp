// === AnalysisNorm.Core/Entities/DeviationStatus.cs ===
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace AnalysisNorm.Core.Entities;

/// <summary>
/// Единый enum для статусов отклонений расхода от нормы
/// Соответствует StatusClassifier из Python utils.py
/// Заменяет все дублированные static классы DeviationStatus в проекте
/// </summary>
public enum DeviationStatus : byte // byte для экономии памяти - всего 7 значений
{
    [Display(Name = "Экономия сильная")]
    EconomyStrong = 0,
    
    [Display(Name = "Экономия средняя")]
    EconomyMedium = 1,
    
    [Display(Name = "Экономия слабая")]
    EconomyWeak = 2,
    
    [Display(Name = "Норма")]
    Normal = 3,
    
    [Display(Name = "Перерасход слабый")]
    OverrunWeak = 4,
    
    [Display(Name = "Перерасход средний")]
    OverrunMedium = 5,
    
    [Display(Name = "Перерасход сильный")]
    OverrunStrong = 6
}

/// <summary>
/// Высокопроизводительные методы для работы со статусами отклонений
/// Консолидирует функциональность из всех дублированных DeviationStatus static классов
/// Оптимизирован для минимального allocation и максимальной скорости
/// </summary>
public static class DeviationStatusHelper
{
    #region Константы порогов (из Python StatusClassifier)
    
    private const decimal StrongEconomyThreshold = -30m;
    private const decimal MediumEconomyThreshold = -20m;
    private const decimal WeakEconomyThreshold = -5m;
    private const decimal NormalUpperThreshold = 5m;
    private const decimal WeakOverrunThreshold = 20m;
    private const decimal MediumOverrunThreshold = 30m;
    
    #endregion

    #region Кэш для производительности
    
    // Предварительно вычисленные значения для избежания рефлексии в runtime
    private static readonly Dictionary<DeviationStatus, string> DisplayNameCache;
    private static readonly Dictionary<DeviationStatus, string> ColorCache;
    
    // Массив для O(1) доступа к цветам по byte значению enum
    private static readonly string[] ColorArray;
    
    static DeviationStatusHelper()
    {
        // Инициализируем кэши при загрузке типа - выполняется только один раз
        DisplayNameCache = InitializeDisplayNameCache();
        ColorCache = InitializeColorCache();
        ColorArray = InitializeColorArray();
    }
    
    #endregion

    #region Основные методы

    /// <summary>
    /// Определяет статус отклонения по проценту отклонения
    /// Соответствует get_status из Python StatusClassifier
    /// Оптимизировано: использует прямые сравнения вместо switch для лучшей производительности
    /// </summary>
    /// <param name="deviationPercent">Процент отклонения от нормы</param>
    /// <returns>Статус отклонения</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DeviationStatus GetStatus(decimal deviationPercent)
    {
        // Упорядочены по убыванию вероятности для branch prediction optimization
        if (deviationPercent <= NormalUpperThreshold && deviationPercent > WeakEconomyThreshold)
            return DeviationStatus.Normal;
            
        if (deviationPercent <= WeakOverrunThreshold)
            return DeviationStatus.OverrunWeak;
            
        if (deviationPercent <= WeakEconomyThreshold && deviationPercent > MediumEconomyThreshold)
            return DeviationStatus.EconomyWeak;
            
        if (deviationPercent <= MediumOverrunThreshold)
            return DeviationStatus.OverrunMedium;
            
        if (deviationPercent <= MediumEconomyThreshold && deviationPercent > StrongEconomyThreshold)
            return DeviationStatus.EconomyMedium;
            
        if (deviationPercent <= StrongEconomyThreshold)
            return DeviationStatus.EconomyStrong;
            
        return DeviationStatus.OverrunStrong;
    }

    /// <summary>
    /// Получает отображаемое имя статуса для UI
    /// Zero-allocation доступ через предварительно заполненный кэш
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetDisplayName(this DeviationStatus status)
    {
        return DisplayNameCache[(DeviationStatus)((byte)status)]; // Безопасное приведение для валидных enum значений
    }

    /// <summary>
    /// Возвращает цвет для статуса (соответствует Python get_status_color)
    /// Максимально быстрый O(1) доступ через массив
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetStatusColor(DeviationStatus status)
    {
        var index = (byte)status;
        return index < ColorArray.Length ? ColorArray[index] : "gray";
    }

    /// <summary>
    /// Определяет, требует ли статус особого внимания (красные статусы)
    /// Используется для быстрой фильтрации критических отклонений
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCritical(this DeviationStatus status)
    {
        return status is DeviationStatus.EconomyStrong or DeviationStatus.OverrunStrong;
    }

    /// <summary>
    /// Возвращает числовой вес статуса для сортировки и приоритизации
    /// Отрицательные значения для экономии, положительные для перерасхода
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSeverityWeight(this DeviationStatus status)
    {
        return status switch
        {
            DeviationStatus.EconomyStrong => -3,
            DeviationStatus.EconomyMedium => -2,
            DeviationStatus.EconomyWeak => -1,
            DeviationStatus.Normal => 0,
            DeviationStatus.OverrunWeak => 1,
            DeviationStatus.OverrunMedium => 2,
            DeviationStatus.OverrunStrong => 3,
            _ => 0
        };
    }

    #endregion

    #region Инициализация кэшей
    
    /// <summary>
    /// Инициализирует кэш отображаемых имен из Display атрибутов
    /// Выполняется один раз при загрузке типа
    /// </summary>
    private static Dictionary<DeviationStatus, string> InitializeDisplayNameCache()
    {
        var cache = new Dictionary<DeviationStatus, string>();
        
        foreach (DeviationStatus status in Enum.GetValues<DeviationStatus>())
        {
            var field = typeof(DeviationStatus).GetField(status.ToString());
            var displayAttribute = field?.GetCustomAttribute<DisplayAttribute>();
            cache[status] = displayAttribute?.Name ?? status.ToString();
        }
        
        return cache;
    }

    /// <summary>
    /// Инициализирует кэш цветов для статусов
    /// </summary>
    private static Dictionary<DeviationStatus, string> InitializeColorCache()
    {
        return new Dictionary<DeviationStatus, string>
        {
            [DeviationStatus.EconomyStrong] = "darkgreen",
            [DeviationStatus.EconomyMedium] = "green",
            [DeviationStatus.EconomyWeak] = "lightgreen", 
            [DeviationStatus.Normal] = "blue",
            [DeviationStatus.OverrunWeak] = "orange",
            [DeviationStatus.OverrunMedium] = "darkorange",
            [DeviationStatus.OverrunStrong] = "red"
        };
    }

    /// <summary>
    /// Инициализирует массив цветов для O(1) доступа по byte индексу
    /// </summary>
    private static string[] InitializeColorArray()
    {
        var colors = new string[7]; // Точное количество enum значений
        colors[(byte)DeviationStatus.EconomyStrong] = "darkgreen";
        colors[(byte)DeviationStatus.EconomyMedium] = "green";
        colors[(byte)DeviationStatus.EconomyWeak] = "lightgreen";
        colors[(byte)DeviationStatus.Normal] = "blue";
        colors[(byte)DeviationStatus.OverrunWeak] = "orange";
        colors[(byte)DeviationStatus.OverrunMedium] = "darkorange";
        colors[(byte)DeviationStatus.OverrunStrong] = "red";
        return colors;
    }
    
    #endregion

    #region Константы для внешнего использования
    
    /// <summary>
    /// Публичные константы порогов для использования в других частях системы
    /// </summary>
    public static class Thresholds
    {
        public const decimal StrongEconomy = StrongEconomyThreshold;
        public const decimal MediumEconomy = MediumEconomyThreshold;
        public const decimal WeakEconomy = WeakEconomyThreshold;
        public const decimal NormalUpper = NormalUpperThreshold;
        public const decimal WeakOverrun = WeakOverrunThreshold;
        public const decimal MediumOverrun = MediumOverrunThreshold;
    }
    
    #endregion

    #region Batch операции для производительности
    
    /// <summary>
    /// Обрабатывает массив отклонений за один проход
    /// Оптимизировано для bulk операций в аналитических отчетах
    /// </summary>
    public static DeviationStatus[] GetStatusesBatch(ReadOnlySpan<decimal> deviations)
    {
        var results = new DeviationStatus[deviations.Length];
        
        for (int i = 0; i < deviations.Length; i++)
        {
            results[i] = GetStatus(deviations[i]);
        }
        
        return results;
    }

    /// <summary>
    /// Подсчитывает статистику по статусам для dashboard
    /// Возвращает количество каждого типа отклонения
    /// </summary>
    public static Dictionary<DeviationStatus, int> GetStatusStatistics(IEnumerable<DeviationStatus> statuses)
    {
        var stats = new Dictionary<DeviationStatus, int>();
        
        // Инициализируем все статусы нулями
        foreach (DeviationStatus status in Enum.GetValues<DeviationStatus>())
        {
            stats[status] = 0;
        }
        
        // Подсчитываем
        foreach (var status in statuses)
        {
            stats[status]++;
        }
        
        return stats;
    }
    
    #endregion
}