// EXAMPLES_CHAT2.cs
// Примеры использования классов, реализованных в Чате 2
// Скопируйте нужные примеры в Program.Main() под #if DEBUG

using System;
using System.Collections.Generic;
using AnalysisNorm.Core;
using Serilog;

namespace AnalysisNorm.Examples
{
    public static class Chat2Examples
    {
        /// <summary>
        /// Пример 1: Работа с NormStorage
        /// </summary>
        public static void ExampleNormStorage()
        {
            Log.Information("=== Пример 1: NormStorage ===");

            // Создание хранилища
            var storage = new NormStorage("examples_norms.json");

            // Создание тестовых норм
            var testNorms = new Dictionary<string, NormData>
            {
                // Норма с двумя точками (гипербола)
                ["101"] = new NormData
                {
                    Points = new List<NormPoint>
                    {
                        new NormPoint(50, 100),  // При 50 т/ось → 100 кВт·ч
                        new NormPoint(60, 90)    // При 60 т/ось → 90 кВт·ч
                    },
                    NormType = "Нажатие",
                    Description = "Тестовая норма №101"
                },

                // Норма с одной точкой (константа)
                ["102"] = new NormData
                {
                    Points = new List<NormPoint>
                    {
                        new NormPoint(55, 95)
                    },
                    NormType = "Нажатие",
                    Description = "Тестовая норма №102 (константа)"
                },

                // Норма с тремя точками (МНК)
                ["103"] = new NormData
                {
                    Points = new List<NormPoint>
                    {
                        new NormPoint(45, 110),
                        new NormPoint(55, 95),
                        new NormPoint(65, 85)
                    },
                    NormType = "Автоведение",
                    Description = "Тестовая норма №103 (3 точки)"
                }
            };

            // Добавление норм
            var results = storage.AddOrUpdateNorms(testNorms);
            Log.Information("Результаты добавления норм: {Results}", 
                string.Join(", ", results.Select(kv => $"{kv.Key}={kv.Value}")));

            // Интерполяция значений
            Log.Information("Интерполяция для нормы 101:");
            for (double load = 50; load <= 60; load += 2)
            {
                var value = storage.InterpolateNormValue("101", load);
                Log.Information("  Нагрузка {Load} т/ось → Расход {Value:F2} кВт·ч", load, value);
            }

            // Валидация норм
            var validation = storage.ValidateNorms();
            Log.Information("Валидация: Valid={Valid}, Invalid={Invalid}, Warnings={Warnings}",
                validation.Valid.Count, validation.Invalid.Count, validation.Warnings.Count);

            // Статистика
            var stats = storage.GetNormStatistics();
            Log.Information("Статистика: Всего норм={Total}, Средн. точек={Avg:F1}",
                stats.TotalNorms, stats.AvgPointsPerNorm);

            // Информация о хранилище
            var info = storage.GetStorageInfo();
            Log.Information("Хранилище: Файл={File}, Размер={Size:F2} MB, Кэш={Cache}",
                info.StorageFile, info.FileSizeMB, info.CachedFunctions);
        }

        /// <summary>
        /// Пример 2: Работа с CoefficientsManager
        /// </summary>
        public static void ExampleCoefficientsManager()
        {
            Log.Information("=== Пример 2: CoefficientsManager ===");

            var manager = new CoefficientsManager();

            // ВАЖНО: Для этого примера нужен реальный Excel файл!
            // Создайте test_coefficients.xlsx со структурой:
            // Лист "ВЛ80": колонки "Заводской номер локомотива", "Коэффициент"
            // Несколько строк с данными

            string excelPath = "test_coefficients.xlsx";
            
            if (System.IO.File.Exists(excelPath))
            {
                // Загрузка без фильтрации
                bool loaded = manager.LoadCoefficients(excelPath, minWorkThreshold: 0);
                Log.Information("Загрузка коэффициентов: {Result}", loaded ? "Успешно" : "Ошибка");

                if (loaded)
                {
                    // Получение коэффициента для конкретного локомотива
                    double coef1 = manager.GetCoefficient("ВЛ80", 1234);
                    double coef2 = manager.GetCoefficient("2ЭС6", 567);
                    Log.Information("Коэффициенты: ВЛ80-1234={Coef1:F3}, 2ЭС6-567={Coef2:F3}", 
                        coef1, coef2);

                    // Получение всех локомотивов серии
                    var vl80Locos = manager.GetLocomotivesBySeries("ВЛ80");
                    Log.Information("Локомотивов серии ВЛ80: {Count}", vl80Locos.Count);
                    foreach (var loco in vl80Locos.Take(5))
                    {
                        Log.Information("  №{Number}: Коэф={Coef:F3}, Откл={Dev:F1}%",
                            loco.Number, loco.Coefficient, loco.DeviationPercent);
                    }

                    // Статистика
                    var stats = manager.GetStatistics();
                    if (stats != null)
                    {
                        Log.Information("Статистика коэффициентов:");
                        Log.Information("  Всего локомотивов: {Total}", stats.TotalLocomotives);
                        Log.Information("  Серий: {Series}", stats.SeriesCount);
                        Log.Information("  Средний коэф.: {Avg:F3}", stats.AvgCoefficient);
                        Log.Information("  Диапазон: {Min:F3} - {Max:F3}", 
                            stats.MinCoefficient, stats.MaxCoefficient);
                        Log.Information("  Среднее отклонение: {Avg:F1}%", stats.AvgDeviationPercent);
                        Log.Information("  Выше нормы: {Above}, Ниже: {Below}, В норме: {At}",
                            stats.LocomotivesAboveNorm, stats.LocomotivesBelowNorm, 
                            stats.LocomotivesAtNorm);
                    }

                    // Все серии
                    var allSeries = manager.GetAllSeries();
                    Log.Information("Найдено серий: {Series}", string.Join(", ", allSeries));
                }
            }
            else
            {
                Log.Warning("Файл {File} не найден. Создайте тестовый Excel файл.", excelPath);
                Log.Information("Структура файла:");
                Log.Information("  Лист 1: Название серии (например, 'ВЛ80')");
                Log.Information("  Колонки: 'Заводской номер локомотива', 'Коэффициент'");
                Log.Information("  Данные: Несколько строк с номерами и коэффициентами");
            }
        }

        /// <summary>
        /// Пример 3: Работа с LocomotiveFilter
        /// </summary>
        public static void ExampleLocomotiveFilter()
        {
            Log.Information("=== Пример 3: LocomotiveFilter ===");

            // Создание фильтра
            var filter = new LocomotiveFilter();

            // Установка тестовых локомотивов вручную
            var locomotives = new List<(string Series, int Number)>
            {
                ("ВЛ80", 1001),
                ("ВЛ80", 1002),
                ("ВЛ80", 1003),
                ("2ЭС6", 501),
                ("2ЭС6", 502),
                ("ЭП1М", 301)
            };

            filter.SetAvailableLocomotives(locomotives);

            // Группировка по сериям
            var bySeries = filter.GetLocomotivesBySeries();
            Log.Information("Локомотивы по сериям:");
            foreach (var series in bySeries)
            {
                Log.Information("  {Series}: {Numbers}",
                    series.Key, string.Join(", ", series.Value));
            }

            // Управление выбором
            Log.Information("Изначально выбрано: {Count}", filter.SelectedLocomotives.Count);

            filter.DeselectAll();
            Log.Information("После DeselectAll: {Count}", filter.SelectedLocomotives.Count);

            filter.SelectSeries("ВЛ80");
            Log.Information("После SelectSeries('ВЛ80'): {Count}", filter.SelectedLocomotives.Count);

            filter.InvertSelection();
            Log.Information("После InvertSelection: {Count}", filter.SelectedLocomotives.Count);

            // Статистика
            var stats = filter.GetStatistics();
            Log.Information("Статистика фильтра: {Stats}", stats.ToString());

            // Предикат для фильтрации
            var predicate = filter.GetFilterPredicate();
            Log.Information("Проверка предиката:");
            foreach (var loco in locomotives.Take(3))
            {
                bool passes = predicate(loco.Series, loco.Number);
                Log.Information("  {Series}-{Number}: {Result}", 
                    loco.Series, loco.Number, passes ? "ПРОХОДИТ" : "НЕ ПРОХОДИТ");
            }
        }

        /// <summary>
        /// Пример 4: Нормализация серий
        /// </summary>
        public static void ExampleSeriesNormalization()
        {
            Log.Information("=== Пример 4: Нормализация серий ===");

            var testCases = new[]
            {
                "ВЛ-80С",
                "2 ЭС6",
                "эп1м",
                "ВЛ 80 К",
                "2эс5к",
                "ЧС7-123"
            };

            foreach (var original in testCases)
            {
                string normalized = CoefficientsManager.NormalizeSeries(original);
                Log.Information("'{Original}' → '{Normalized}'", original, normalized);
            }
        }

        /// <summary>
        /// Пример 5: Комплексный сценарий
        /// </summary>
        public static void ExampleComplexScenario()
        {
            Log.Information("=== Пример 5: Комплексный сценарий ===");

            // 1. Загружаем нормы
            var storage = new NormStorage("complex_scenario.json");
            var norms = new Dictionary<string, NormData>
            {
                ["201"] = new NormData
                {
                    Points = new List<NormPoint>
                    {
                        new NormPoint(50, 105),
                        new NormPoint(60, 92),
                        new NormPoint(70, 83)
                    },
                    NormType = "Нажатие"
                }
            };
            storage.AddOrUpdateNorms(norms);

            // 2. Настраиваем фильтр локомотивов
            var filter = new LocomotiveFilter();
            filter.SetAvailableLocomotives(new List<(string, int)>
            {
                ("ВЛ80", 2001),
                ("ВЛ80", 2002)
            });

            // 3. Применяем коэффициенты (имитация)
            var baseConsumption = 95.0; // кВт·ч
            var coefficient = 1.05;     // 5% перерасход
            var adjustedConsumption = baseConsumption * coefficient;

            Log.Information("Базовый расход: {Base} кВт·ч", baseConsumption);
            Log.Information("Коэффициент: {Coef:F2}", coefficient);
            Log.Information("С учетом коэф.: {Adjusted:F2} кВт·ч", adjustedConsumption);

            // 4. Интерполяция нормы для сравнения
            var normValue = storage.InterpolateNormValue("201", 55);
            var deviation = (adjustedConsumption - normValue.Value) / normValue.Value * 100;

            Log.Information("Норма (для 55 т/ось): {Norm:F2} кВт·ч", normValue);
            Log.Information("Отклонение от нормы: {Dev:F1}%", deviation);

            // 5. Определение статуса
            string status = deviation switch
            {
                < -10 => "Экономия",
                > 10 => "Перерасход",
                _ => "В норме"
            };

            Log.Information("Статус: {Status}", status);
        }

        /// <summary>
        /// Запуск всех примеров
        /// </summary>
        public static void RunAllExamples()
        {
            try
            {
                ExampleNormStorage();
                Log.Information("");

                ExampleCoefficientsManager();
                Log.Information("");

                ExampleLocomotiveFilter();
                Log.Information("");

                ExampleSeriesNormalization();
                Log.Information("");

                ExampleComplexScenario();

                Log.Information("");
                Log.Information("=== Все примеры выполнены успешно! ===");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Ошибка при выполнении примеров");
            }
        }
    }
}
