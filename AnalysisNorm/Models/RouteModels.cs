// Models/RouteModels.cs
// Модели данных для маршрутов
// Python: словари и DataFrame в html_route_processor.py
// ЧАТ 3

using System;
using System.Collections.Generic;

namespace AnalysisNorm.Models
{
    #region Основные модели маршрутов

    /// <summary>
    /// Метаданные маршрута (заголовок)
    /// Python: metadata словарь из extract_route_header_from_html()
    /// </summary>
    public class RouteMetadata
    {
        /// <summary>Номер маршрута</summary>
        public string? Number { get; set; }

        /// <summary>Дата поездки</summary>
        public string? TripDate { get; set; }

        /// <summary>Табельный номер машиниста</summary>
        public string? DriverTab { get; set; }

        /// <summary>Идентификатор (для отладки)</summary>
        public string? Identifier { get; set; }

        /// <summary>Серия локомотива</summary>
        public string? LocomotiveSeries { get; set; }

        /// <summary>Номер локомотива</summary>
        public string? LocomotiveNumber { get; set; }

        /// <summary>Депо</summary>
        public string? Depot { get; set; }

        /// <summary>Дополнительные данные</summary>
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Участок маршрута
    /// Python: словарь section в parse_route_sections()
    /// </summary>
    public class RouteSection
    {
        /// <summary>Название участка (например, "МОСКВА - ПЕТЕРБУРГ")</summary>
        public string? Name { get; set; }

        /// <summary>Двойная тяга (0 или 1)</summary>
        public int? DoubleTraction { get; set; }

        /// <summary>Тонно-километры брутто</summary>
        public double? TkmBrutto { get; set; }

        /// <summary>Километры</summary>
        public double? Km { get; set; }

        /// <summary>Признак "Пр." (проследование?)</summary>
        public string? Pr { get; set; }

        /// <summary>Расход фактический (кВт·ч)</summary>
        public double? RashodFact { get; set; }

        /// <summary>Расход по норме (кВт·ч)</summary>
        public double? RashodNorm { get; set; }

        /// <summary>Удельная норма (на 1 час маневровой работы)</summary>
        public double? UdNorma { get; set; }

        /// <summary>Нажатие на ось (тонны)</summary>
        public double? AxleLoad { get; set; }

        /// <summary>Норма на работу</summary>
        public double? NormaRabotu { get; set; }

        /// <summary>Факт удельный</summary>
        public double? FactUd { get; set; }

        /// <summary>Факт на работу</summary>
        public double? FactNaRabotu { get; set; }

        /// <summary>Норма на одиночное движение</summary>
        public double? NormaOdinochnoe { get; set; }

        /// <summary>Объединен ли участок с другими (merged)</summary>
        public bool IsMerged { get; set; }

        /// <summary>Данные станции (простои, маневры и т.д.)</summary>
        public StationData? Station { get; set; }
    }

    /// <summary>
    /// Данные станции (простои, маневры, трогания и т.д.)
    /// Python: словарь station_data в parse_station_table()
    /// </summary>
    public class StationData
    {
        public string? SectionName { get; set; }

        // Простой с бригадой
        public double? ProstoyVsego { get; set; }
        public double? ProstoyNorma { get; set; }

        // Маневры
        public double? ManevryVsego { get; set; }
        public double? ManevryNorma { get; set; }

        // Трогание с места
        public double? TroganieVsego { get; set; }
        public double? TroganieNorma { get; set; }

        // Нагон опозданий
        public double? NagonVsego { get; set; }
        public double? NagonNorma { get; set; }

        // Ограничения скорости
        public double? OgranichVsego { get; set; }
        public double? OgranichNorma { get; set; }

        // На пересылаемые локомотивы
        public double? PeresylVsego { get; set; }
        public double? PeresylNorma { get; set; }
    }

    /// <summary>
    /// Полная запись маршрута для DataFrame
    /// Python: результат build_output_rows()
    /// </summary>
    public class RouteRecord
    {
        // Метаданные маршрута
        public string? RouteNumber { get; set; }
        public string? TripDate { get; set; }
        public string? LocomotiveSeries { get; set; }
        public string? LocomotiveNumber { get; set; }
        public string? Depot { get; set; }
        public string? DriverTab { get; set; }

        // Данные участка
        public string? SectionName { get; set; }
        public int? DoubleTraction { get; set; }
        public double? TkmBrutto { get; set; }
        public double? Km { get; set; }
        public string? Pr { get; set; }
        public double? RashodFact { get; set; }
        public double? RashodNorm { get; set; }
        public double? UdNorma { get; set; }
        public double? AxleLoad { get; set; }
        public double? NormaRabotu { get; set; }
        public double? FactUd { get; set; }
        public double? FactNaRabotu { get; set; }
        public double? NormaOdinochnoe { get; set; }

        // Данные станции
        public double? ProstoyVsego { get; set; }
        public double? ProstoyNorma { get; set; }
        public double? ManevryVsego { get; set; }
        public double? ManevryNorma { get; set; }
        public double? TroganieVsego { get; set; }
        public double? TroganieNorma { get; set; }
        public double? NagonVsego { get; set; }
        public double? NagonNorma { get; set; }
        public double? OgranichVsego { get; set; }
        public double? OgranichNorma { get; set; }
        public double? PeresylVsego { get; set; }
        public double? PeresylNorma { get; set; }

        // Служебная информация
        public int? DuplicatesCount { get; set; }
        public bool? HasEqualRashod { get; set; }

        /// <summary>
        /// Преобразует в словарь для DataFrame
        /// </summary>
        public Dictionary<string, object?> ToDictionary()
        {
            return new Dictionary<string, object?>
            {
                ["Маршрут №"] = RouteNumber,
                ["Дата поездки"] = TripDate,
                ["Серия"] = LocomotiveSeries,
                ["Номер"] = LocomotiveNumber,
                ["Депо"] = Depot,
                ["Табельный машиниста"] = DriverTab,
                ["Участок"] = SectionName,
                ["Двойная тяга"] = DoubleTraction,
                ["Ткм брутто"] = TkmBrutto,
                ["Км"] = Km,
                ["Пр."] = Pr,
                ["Расход фактический"] = RashodFact,
                ["Расход по норме"] = RashodNorm,
                ["Уд. норма, норма на 1 час ман. раб."] = UdNorma,
                ["Нажатие на ось"] = AxleLoad,
                ["Норма на работу"] = NormaRabotu,
                ["Факт уд"] = FactUd,
                ["Факт на работу"] = FactNaRabotu,
                ["Норма на одиночное"] = NormaOdinochnoe,
                ["Простой с бригадой, мин., всего"] = ProstoyVsego,
                ["Простой с бригадой, мин., норма"] = ProstoyNorma,
                ["Маневры, мин., всего"] = ManevryVsego,
                ["Маневры, мин., норма"] = ManevryNorma,
                ["Трогание с места, случ., всего"] = TroganieVsego,
                ["Трогание с места, случ., норма"] = TroganieNorma,
                ["Нагон опозданий, мин., всего"] = NagonVsego,
                ["Нагон опозданий, мин., норма"] = NagonNorma,
                ["Ограничения скорости, случ., всего"] = OgranichVsego,
                ["Ограничения скорости, случ., норма"] = OgranichNorma,
                ["На пересылаемые л-вы, всего"] = PeresylVsego,
                ["На пересылаемые л-вы, норма"] = PeresylNorma,
                ["Количество дубликатов маршрута"] = DuplicatesCount,
                ["Н=Ф"] = HasEqualRashod.HasValue && HasEqualRashod.Value ? "Да" : null
            };
        }
    }

    #endregion

    #region Статистика обработки

    /// <summary>
    /// Статистика обработки маршрутов
    /// Python: словарь stats в _process_routes()
    /// </summary>
    public class ProcessingStats
    {
        public int TotalRoutesFound { get; set; }
        public int UniqueRoutes { get; set; }
        public int DuplicatesTotal { get; set; }
        public int RoutesWithEqualRashod { get; set; }
        public int RoutesSkipped { get; set; }
        public int RoutesProcessed { get; set; }
        public int OutputRows { get; set; }

        public Dictionary<string, DuplicateInfo> DuplicateDetails { get; set; } = new();

        public override string ToString()
        {
            return $"Найдено: {TotalRoutesFound}, " +
                   $"Уникальных: {UniqueRoutes}, " +
                   $"Дубликатов: {DuplicatesTotal}, " +
                   $"Обработано: {RoutesProcessed}, " +
                   $"Пропущено: {RoutesSkipped}, " +
                   $"Строк на выходе: {OutputRows}";
        }
    }

    /// <summary>
    /// Информация о дубликатах маршрута
    /// </summary>
    public class DuplicateInfo
    {
        public int Versions { get; set; }
        public int Duplicates { get; set; }
        public List<string> Identifiers { get; set; } = new();
    }

    #endregion
}
