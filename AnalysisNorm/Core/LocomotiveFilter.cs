// Core/LocomotiveFilter.cs
// Миграция из: core/filter.py
// Фильтр выбора локомотивов с поддержкой разных форматов входных данных
// ВЕРСИЯ ЧАТА 2: Упрощенная версия с заглушками для DataFrame операций

using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;

namespace AnalysisNorm.Core
{
    /// <summary>
    /// Режимы работы фильтра
    /// Python: строковые значения _mode
    /// </summary>
    public enum FilterMode
    {
        Standard,   // Стандартные колонки "Серия локомотива" + "Номер локомотива"
        Guess,      // Угадывание колонок
        Depot,      // Фиктивный ключ (депо/маршрут)
        Minimal     // Минимальный набор (HTML1, HTML2, ...)
    }

    /// <summary>
    /// Фильтр выбора локомотивов с поддержкой разных форматов входных данных
    /// Python класс: LocomotiveFilter (filter.py)
    /// 
    /// ВАЖНО: В Чате 2 это упрощенная версия
    /// Полная векторизованная фильтрация с DataFrame будет в Чате 3
    /// </summary>
    public class LocomotiveFilter
    {
        private FilterMode _mode;
        private string _seriesColumn;
        private string _numberColumn;

        // Список доступных локомотивов: (серия, номер)
        private List<(string Series, int Number)> _availableLocomotives;
        
        // Набор выбранных локомотивов
        private HashSet<(string Series, int Number)> _selectedLocomotives;

        /// <summary>
        /// Текущий режим работы фильтра
        /// </summary>
        public FilterMode Mode => _mode;

        /// <summary>
        /// Доступные локомотивы
        /// Python: self.avl
        /// </summary>
        public List<(string Series, int Number)> AvailableLocomotives => 
            new List<(string, int)>(_availableLocomotives);

        /// <summary>
        /// Выбранные локомотивы
        /// Python: self.sel
        /// </summary>
        public HashSet<(string Series, int Number)> SelectedLocomotives => 
            new HashSet<(string, int)>(_selectedLocomotives);

        /// <summary>
        /// Конструктор (упрощенная версия для Чата 2)
        /// Python: __init__(df: pd.DataFrame)
        /// 
        /// TODO Чат 3: Добавить полную поддержку DataFrame из Microsoft.Data.Analysis
        /// </summary>
        public LocomotiveFilter()
        {
            _mode = FilterMode.Minimal;
            _seriesColumn = null;
            _numberColumn = null;
            _availableLocomotives = new List<(string, int)>();
            _selectedLocomotives = new HashSet<(string, int)>();

            Log.Debug("LocomotiveFilter создан (режим: {Mode})", _mode);
        }

        #region Извлечение локомотивов
        // Python: _extract_locomotives()

        /// <summary>
        /// Извлекает локомотивы из данных (ЗАГЛУШКА для Чата 2)
        /// Python: _extract_locomotives()
        /// 
        /// TODO Чат 3: Реализовать полную логику с DataFrame:
        /// - Режим standard: колонки "Серия локомотива", "Номер локомотива"
        /// - Режим guess: автоопределение колонок
        /// - Режим depot: создание фиктивных ключей из депо/маршрута
        /// - Режим minimal: создание заглушек HTML1, HTML2, ...
        /// </summary>
        public void ExtractLocomotivesFromData(object data)
        {
            // ЗАГЛУШКА: В Чате 2 создаем минимальный набор
            _mode = FilterMode.Minimal;
            _availableLocomotives = Enumerable.Range(1, 5)
                .Select(i => ("HTML", i))
                .ToList();

            // По умолчанию все выбраны
            _selectedLocomotives = new HashSet<(string, int)>(_availableLocomotives);

            Log.Information("Извлечено {Count} локомотивов (режим: {Mode})", 
                _availableLocomotives.Count, _mode);
            Log.Debug("Примеры: {Examples}", 
                string.Join(", ", _availableLocomotives.Take(5).Select(l => $"({l.Series},{l.Number})")));
        }

        /// <summary>
        /// Устанавливает доступные локомотивы вручную
        /// Полезно для тестирования до реализации полного парсинга
        /// </summary>
        public void SetAvailableLocomotives(List<(string Series, int Number)> locomotives)
        {
            _availableLocomotives = new List<(string, int)>(locomotives);
            _selectedLocomotives = new HashSet<(string, int)>(_availableLocomotives);
            
            _mode = FilterMode.Standard; // Предполагаем стандартный режим
            
            Log.Information("Установлено {Count} доступных локомотивов", locomotives.Count);
        }

        #endregion

        #region Группировка по сериям
        // Python: get_locomotives_by_series()

        /// <summary>
        /// Группировка доступных локомотивов по сериям
        /// Python: get_locomotives_by_series()
        /// </summary>
        public Dictionary<string, List<int>> GetLocomotivesBySeries()
        {
            var result = new Dictionary<string, List<int>>();

            foreach (var (series, number) in _availableLocomotives)
            {
                if (!result.ContainsKey(series))
                    result[series] = new List<int>();
                
                result[series].Add(number);
            }

            // Сортируем номера в каждой серии
            foreach (var series in result.Keys.ToList())
            {
                result[series].Sort();
            }

            return result;
        }

        /// <summary>
        /// Получить все уникальные серии
        /// </summary>
        public List<string> GetAllSeries()
        {
            return _availableLocomotives
                .Select(l => l.Series)
                .Distinct()
                .OrderBy(s => s)
                .ToList();
        }

        #endregion

        #region Управление выбором
        // Python: set_selected_locomotives(), select_all(), deselect_all()

        /// <summary>
        /// Установка выбранных локомотивов
        /// Python: set_selected_locomotives()
        /// </summary>
        public void SetSelectedLocomotives(List<(string Series, int Number)> selected)
        {
            _selectedLocomotives = new HashSet<(string, int)>(selected);
            Log.Debug("Установлено {Count} выбранных локомотивов", _selectedLocomotives.Count);
        }

        /// <summary>
        /// Выбрать все доступные локомотивы
        /// Python: select_all()
        /// </summary>
        public void SelectAll()
        {
            _selectedLocomotives = new HashSet<(string, int)>(_availableLocomotives);
            Log.Debug("Выбраны все локомотивы: {Count}", _selectedLocomotives.Count);
        }

        /// <summary>
        /// Снять выбор со всех локомотивов
        /// Python: deselect_all()
        /// </summary>
        public void DeselectAll()
        {
            _selectedLocomotives.Clear();
            Log.Debug("Сброшен выбор всех локомотивов");
        }

        /// <summary>
        /// Инвертировать выбор
        /// </summary>
        public void InvertSelection()
        {
            var newSelection = new HashSet<(string, int)>();
            
            foreach (var locomotive in _availableLocomotives)
            {
                if (!_selectedLocomotives.Contains(locomotive))
                    newSelection.Add(locomotive);
            }

            _selectedLocomotives = newSelection;
            Log.Debug("Инвертирован выбор: {Count} локомотивов выбрано", _selectedLocomotives.Count);
        }

        /// <summary>
        /// Выбрать всю серию
        /// </summary>
        public void SelectSeries(string series)
        {
            var seriesLocomotives = _availableLocomotives
                .Where(l => l.Series == series);

            foreach (var locomotive in seriesLocomotives)
            {
                _selectedLocomotives.Add(locomotive);
            }

            Log.Debug("Выбрана серия {Series}", series);
        }

        /// <summary>
        /// Снять выбор с серии
        /// </summary>
        public void DeselectSeries(string series)
        {
            var toRemove = _selectedLocomotives
                .Where(l => l.Series == series)
                .ToList();

            foreach (var locomotive in toRemove)
            {
                _selectedLocomotives.Remove(locomotive);
            }

            Log.Debug("Снят выбор с серии {Series}", series);
        }

        #endregion

        #region Фильтрация данных
        // Python: filter_routes()

        /// <summary>
        /// Фильтрация маршрутов по выбранным локомотивам (ЗАГЛУШКА для Чата 2)
        /// Python: filter_routes(df: pd.DataFrame)
        /// 
        /// TODO Чат 3: Реализовать полную векторизованную фильтрацию с DataFrame
        /// - Поддержка всех 4 режимов (standard, guess, depot, minimal)
        /// - Векторизованная фильтрация через join
        /// - Обработка разных типов колонок
        /// </summary>
        /// <returns>Отфильтрованные данные (пока возвращает null - заглушка)</returns>
        public object FilterRoutes(object dataFrame)
        {
            // ЗАГЛУШКА для Чата 2
            Log.Warning("FilterRoutes() вызван, но полная реализация будет в Чате 3");
            Log.Information("Режим фильтрации: {Mode}, Выбрано локомотивов: {Count}", 
                _mode, _selectedLocomotives.Count);

            // TODO Чат 3: Вернуть отфильтрованный DataFrame
            throw new NotImplementedException(
                "Полная фильтрация DataFrame будет реализована в Чате 3. " +
                "Используйте GetFilterPredicate() для ручной фильтрации."
            );
        }

        /// <summary>
        /// Возвращает предикат для фильтрации
        /// Временное решение до реализации полной фильтрации DataFrame
        /// </summary>
        public Func<string, int, bool> GetFilterPredicate()
        {
            if (_selectedLocomotives.Count == 0)
            {
                // Если ничего не выбрано - ничего не проходит
                return (series, number) => false;
            }

            return (series, number) => _selectedLocomotives.Contains((series, number));
        }

        #endregion

        #region Статистика

        /// <summary>
        /// Получить статистику по фильтру
        /// </summary>
        public FilterStatistics GetStatistics()
        {
            var bySeries = GetLocomotivesBySeries();
            var selectedBySeries = _selectedLocomotives
                .GroupBy(l => l.Series)
                .ToDictionary(g => g.Key, g => g.Count());

            return new FilterStatistics
            {
                Mode = _mode,
                TotalAvailable = _availableLocomotives.Count,
                TotalSelected = _selectedLocomotives.Count,
                SeriesCount = bySeries.Count,
                AvailableBySeries = bySeries.ToDictionary(kv => kv.Key, kv => kv.Value.Count),
                SelectedBySeries = selectedBySeries
            };
        }

        #endregion
    }

    /// <summary>
    /// Статистика фильтра
    /// </summary>
    public class FilterStatistics
    {
        public FilterMode Mode { get; set; }
        public int TotalAvailable { get; set; }
        public int TotalSelected { get; set; }
        public int SeriesCount { get; set; }
        public Dictionary<string, int> AvailableBySeries { get; set; }
        public Dictionary<string, int> SelectedBySeries { get; set; }

        public override string ToString()
        {
            return $"Режим: {Mode}, Доступно: {TotalAvailable}, " +
                   $"Выбрано: {TotalSelected}, Серий: {SeriesCount}";
        }
    }
}
