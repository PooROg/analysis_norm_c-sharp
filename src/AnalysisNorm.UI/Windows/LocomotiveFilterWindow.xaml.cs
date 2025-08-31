using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Extensions.Logging;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.UI.Windows;

/// <summary>
/// ИСПРАВЛЕНО: Окно фильтрации локомотивов с правильными обработчиками событий
/// Устранены проблемы с типами данных и отсутствующими методами
/// </summary>
public partial class LocomotiveFilterWindow : Window, INotifyPropertyChanged
{
    #region Fields

    private readonly ILogger<LocomotiveFilterWindow> _logger;
    private readonly ILocomotiveCoefficientService _coefficientService;
    private readonly ILocomotiveFilterService _filterService;

    private ObservableCollection<LocomotiveDisplayItem> _availableLocomotives = new();
    private ObservableCollection<LocomotiveDisplayItem> _filteredLocomotives = new();
    private string _searchText = string.Empty;
    private bool _isLoading = false;
    private string _statusMessage = string.Empty;

    // ИСПРАВЛЕНО: Правильный тип для результата диалога
    private LocomotiveFilterResult? _dialogResult;

    #endregion

    #region Constructor

    public LocomotiveFilterWindow(
        ILogger<LocomotiveFilterWindow> logger,
        ILocomotiveCoefficientService coefficientService,
        ILocomotiveFilterService filterService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _coefficientService = coefficientService ?? throw new ArgumentNullException(nameof(coefficientService));
        _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));

        InitializeComponent();
        DataContext = this;

        Loaded += OnWindowLoaded;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Доступные локомотивы для выбора
    /// </summary>
    public ObservableCollection<LocomotiveDisplayItem> AvailableLocomotives
    {
        get => _availableLocomotives;
        set => SetProperty(ref _availableLocomotives, value);
    }

    /// <summary>
    /// Отфильтрованный список локомотивов
    /// </summary>
    public ObservableCollection<LocomotiveDisplayItem> FilteredLocomotives
    {
        get => _filteredLocomotives;
        set => SetProperty(ref _filteredLocomotives, value);
    }

    /// <summary>
    /// Текст поиска
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// Флаг загрузки данных
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// Сообщение о статусе
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Результат диалога с правильным типом
    /// </summary>
    public LocomotiveFilterResult? FilterResult => _dialogResult;

    #endregion

    #region Window Events

    /// <summary>
    /// Обработчик загрузки окна
    /// </summary>
    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await LoadLocomotivesAsync();
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Обработчик кнопки OK
    /// </summary>
    private void OK_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedLocomotives = FilteredLocomotives
                .Where(item => item.IsSelected)
                .ToList();

            if (!selectedLocomotives.Any())
            {
                MessageBox.Show("Выберите хотя бы один локомотив", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ИСПРАВЛЕНО: Создаем правильный результат с корректными типами
            var selectedSeries = selectedLocomotives
                .Select(item => (item.Series, GetLocomotiveNumber(item)))
                .Where(pair => pair.Item2.HasValue)
                .Select(pair => (pair.Series, pair.Item2!.Value))
                .ToHashSet();

            _dialogResult = new LocomotiveFilterResult
            {
                IsConfirmed = true,
                SelectedLocomotives = selectedSeries, // ИСПРАВЛЕНО: правильный тип HashSet<(string, int)>
                FilterCriteria = CreateFilterCriteria(selectedLocomotives)
            };

            // ИСПРАВLЕНО: правильный возврат диалогового результата
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке выбора локомотивов");
            MessageBox.Show("Ошибка при обработке выбора", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Обработчик кнопки Отмена
    /// </summary>
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        // ИСПРАВЛЕНО: правильный возврат результата отмены
        _dialogResult = new LocomotiveFilterResult
        {
            IsConfirmed = false,
            SelectedLocomotives = new HashSet<(string Series, int Number)>(),
            FilterCriteria = new LocomotiveFilterCriteria()
        };

        DialogResult = false;
        Close();
    }

    /// <summary>
    /// ИСПРАВЛЕНО: Обработчик кнопки "Снять все"
    /// </summary>
    private void UnselectAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var item in FilteredLocomotives)
            {
                item.IsSelected = false;
            }

            StatusMessage = "Выбор снят со всех локомотивов";
            _logger.LogDebug("Снят выбор со всех локомотивов");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при снятии выбора с локомотивов");
        }
    }

    /// <summary>
    /// Обработчик кнопки "Выбрать все"
    /// </summary>
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var item in FilteredLocomotives)
            {
                item.IsSelected = true;
            }

            StatusMessage = $"Выбраны все локомотивы ({FilteredLocomotives.Count})";
            _logger.LogDebug("Выбраны все отфильтрованные локомотивы");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выборе всех локомотивов");
        }
    }

    #endregion

    #region Data Loading

    /// <summary>
    /// Загружает список доступных локомотивов
    /// </summary>
    private async Task LoadLocomotivesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Загрузка локомотивов...";

            _logger.LogInformation("Начало загрузки списка локомотивов");

            // ИСПРАВЛЕНО: правильная обработка ProcessingResult
            var coefficientsResult = await _coefficientService.GetAllCoefficientsAsync();

            if (!coefficientsResult.IsSuccess || coefficientsResult.Data == null)
            {
                StatusMessage = "Ошибка загрузки данных локомотивов";
                _logger.LogError("Не удалось получить коэффициенты локомотивов");
                return;
            }

            var coefficients = coefficientsResult.Data.ToList(); // ИСПРАВЛЕНО: правильная работа с результатом

            AvailableLocomotives.Clear();

            // Группируем коэффициенты по сериям и номерам
            var groupedCoefficients = coefficients
                .Where(c => c.IsValid)
                .GroupBy(c => new { c.LocomotiveSeries, c.LocomotiveNumber })
                .OrderBy(g => g.Key.LocomotiveSeries)
                .ThenBy(g => g.Key.LocomotiveNumber);

            foreach (var group in groupedCoefficients)
            {
                var firstCoeff = group.First();
                var displayItem = new LocomotiveDisplayItem
                {
                    Series = firstCoeff.LocomotiveSeries,
                    Number = firstCoeff.LocomotiveNumber,
                    DisplayName = firstCoeff.FullName,
                    Type = firstCoeff.Type,
                    // ИСПРАВЛЕНО: правильное получение значения коэффициента
                    CoefficientValue = firstCoeff.Value,
                    IsActive = firstCoeff.IsActive,
                    LastUsed = firstCoeff.LastUsed,
                    UsageCount = firstCoeff.UsageCount,
                    IsSelected = false
                };

                AvailableLocomotives.Add(displayItem);
            }

            // Инициализируем отфильтрованный список
            ApplyFilter();

            StatusMessage = $"Загружено локомотивов: {AvailableLocomotives.Count}";
            _logger.LogInformation("Загрузка локомотивов завершена. Всего: {Count}", AvailableLocomotives.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке локомотивов");
            StatusMessage = "Ошибка загрузки данных";
            MessageBox.Show("Ошибка при загрузке данных локомотивов", "Ошибка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Filtering

    /// <summary>
    /// Применяет фильтр к списку локомотивов
    /// </summary>
    private void ApplyFilter()
    {
        try
        {
            var filtered = AvailableLocomotives.AsEnumerable();

            // Применяем текстовый фильтр
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLowerInvariant();
                filtered = filtered.Where(item =>
                    item.Series.ToLowerInvariant().Contains(searchLower) ||
                    item.DisplayName.ToLowerInvariant().Contains(searchLower) ||
                    (item.Number?.ToString().Contains(searchLower) == true));
            }

            // Обновляем отфильтрованный список
            FilteredLocomotives.Clear();
            foreach (var item in filtered)
            {
                FilteredLocomotives.Add(item);
            }

            StatusMessage = $"Показано локомотивов: {FilteredLocomotives.Count} из {AvailableLocomotives.Count}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при применении фильтра");
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// ИСПРАВЛЕНО: Получает номер локомотива с правильной обработкой nullable типов
    /// </summary>
    private int? GetLocomotiveNumber(LocomotiveDisplayItem item)
    {
        // ИСПРАВЛЕНО: правильная работа с nullable int
        return item.Number;
    }

    /// <summary>
    /// Создает критерии фильтрации на основе выбранных локомотивов
    /// </summary>
    private LocomotiveFilterCriteria CreateFilterCriteria(List<LocomotiveDisplayItem> selectedItems)
    {
        var criteria = new LocomotiveFilterCriteria
        {
            IncludeOnlySelected = true,
            SelectedSeries = selectedItems.Select(i => i.Series).Distinct().ToList(),
            IncludeActiveOnly = true
        };

        // Добавляем номера локомотивов для каждой серии
        foreach (var series in criteria.SelectedSeries)
        {
            var numbersForSeries = selectedItems
                .Where(i => i.Series == series && i.Number.HasValue)
                .Select(i => i.Number!.Value) // ИСПРАВЛЕНО: правильная работа с nullable
                .ToList();

            if (numbersForSeries.Any())
            {
                criteria.SpecificNumbers[series] = numbersForSeries;
            }
        }

        return criteria;
    }

    #endregion

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}

/// <summary>
/// ИСПРАВЛЕНО: Элемент отображения локомотива с правильными типами данных
/// </summary>
public class LocomotiveDisplayItem : INotifyPropertyChanged
{
    private bool _isSelected;

    /// <summary>
    /// Серия локомотива
    /// </summary>
    public string Series { get; set; } = string.Empty;

    /// <summary>
    /// Номер локомотива (может быть null для общих серий)
    /// </summary>
    public int? Number { get; set; }

    /// <summary>
    /// Отображаемое название
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Тип локомотива
    /// </summary>
    public LocomotiveType Type { get; set; }

    /// <summary>
    /// Значение коэффициента
    /// </summary>
    public decimal CoefficientValue { get; set; }

    /// <summary>
    /// Активен ли коэффициент
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Последнее использование
    /// </summary>
    public DateTime? LastUsed { get; set; }

    /// <summary>
    /// Количество использований
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Выбран ли элемент
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}

/// <summary>
/// ИСПРАВЛЕНО: Результат диалога фильтрации локомотивов с правильными типами
/// </summary>
public class LocomotiveFilterResult
{
    /// <summary>
    /// Подтвержден ли выбор пользователем
    /// </summary>
    public bool IsConfirmed { get; set; }

    /// <summary>
    /// ИСПРАВЛЕНО: Выбранные локомотивы с правильным типом
    /// </summary>
    public HashSet<(string Series, int Number)> SelectedLocomotives { get; set; } = new();

    /// <summary>
    /// Критерии фильтрации
    /// </summary>
    public LocomotiveFilterCriteria FilterCriteria { get; set; } = new();

    /// <summary>
    /// Количество выбранных локомотивов
    /// </summary>
    public int SelectedCount => SelectedLocomotives.Count;

    /// <summary>
    /// Есть ли выбранные локомотивы
    /// </summary>
    public bool HasSelection => SelectedLocomotives.Any();
}

/// <summary>
/// Критерии фильтрации локомотивов
/// </summary>
public class LocomotiveFilterCriteria
{
    /// <summary>
    /// Включать только выбранные локомотивы
    /// </summary>
    public bool IncludeOnlySelected { get; set; }

    /// <summary>
    /// Выбранные серии локомотивов
    /// </summary>
    public List<string> SelectedSeries { get; set; } = new();

    /// <summary>
    /// Конкретные номера для каждой серии
    /// </summary>
    public Dictionary<string, List<int>> SpecificNumbers { get; set; } = new();

    /// <summary>
    /// Включать только активные коэффициенты
    /// </summary>
    public bool IncludeActiveOnly { get; set; } = true;

    /// <summary>
    /// Минимальное количество использований
    /// </summary>
    public int MinUsageCount { get; set; } = 0;

    /// <summary>
    /// Фильтр по типу локомотива
    /// </summary>
    public LocomotiveType? TypeFilter { get; set; }

    /// <summary>
    /// Проверяет соответствует ли локомотив критериям фильтра
    /// </summary>
    public bool Matches(LocomotiveCoefficient locomotive)
    {
        if (IncludeActiveOnly && !locomotive.IsActive)
            return false;

        if (locomotive.UsageCount < MinUsageCount)
            return false;

        if (TypeFilter.HasValue && locomotive.Type != TypeFilter.Value)
            return false;

        if (IncludeOnlySelected && SelectedSeries.Any())
        {
            if (!SelectedSeries.Contains(locomotive.LocomotiveSeries))
                return false;

            // Если есть конкретные номера для серии, проверяем их
            if (SpecificNumbers.ContainsKey(locomotive.LocomotiveSeries) && locomotive.LocomotiveNumber.HasValue)
            {
                var allowedNumbers = SpecificNumbers[locomotive.LocomotiveSeries];
                if (!allowedNumbers.Contains(locomotive.LocomotiveNumber.Value))
                    return false;
            }
        }

        return true;
    }
}