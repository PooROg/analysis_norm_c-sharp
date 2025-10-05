// Core/NormStorage.cs
// Миграция из: core/norm_storage.py
// Высокопроизводительное хранилище норм с гиперболической интерполяцией

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.Optimization;
using Newtonsoft.Json;
using Serilog;

namespace AnalysisNorm.Core
{
    #region Модели данных для норм
    
    /// <summary>
    /// Точка нормы: (нагрузка на ось, расход энергии)
    /// Python: tuple (float, float)
    /// </summary>
    public class NormPoint
    {
        public double LoadValue { get; set; }  // Нагрузка на ось (т/ось)
        public double EnergyValue { get; set; } // Расход энергии (кВт·ч/10⁴ ткм)

        public NormPoint(double load, double energy)
        {
            LoadValue = load;
            EnergyValue = energy;
        }

        public override bool Equals(object obj)
        {
            return obj is NormPoint other &&
                   Math.Abs(LoadValue - other.LoadValue) < 1e-9 &&
                   Math.Abs(EnergyValue - other.EnergyValue) < 1e-9;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(LoadValue, EnergyValue);
        }
    }

    /// <summary>
    /// Данные одной нормы
    /// Python: dict с ключами points, norm_type, description, base_data
    /// </summary>
    public class NormData
    {
        public List<NormPoint> Points { get; set; } = new List<NormPoint>();
        public string NormType { get; set; } = "Нажатие"; // "Нажатие" или другие типы
        public string Description { get; set; } = "";
        public Dictionary<string, object> BaseData { get; set; } = new Dictionary<string, object>();

        [JsonIgnore]
        public Func<double, double> InterpolationFunction { get; set; }
    }

    /// <summary>
    /// Метаданные хранилища
    /// Python: dict metadata
    /// </summary>
    public class StorageMetadata
    {
        public string Version { get; set; } = "1.0";
        public int TotalNorms { get; set; } = 0;
        public DateTime? LastUpdated { get; set; }
        public Dictionary<string, int> NormTypes { get; set; } = new Dictionary<string, int>();
    }

    #endregion

    /// <summary>
    /// Высокопроизводительное хранилище норм с кэшированием интерполяционных функций
    /// Python класс: NormStorage (norm_storage.py)
    /// 
    /// Особенности:
    /// - JSON сериализация вместо pickle
    /// - Гиперболическая интерполяция: 1 точка → константа, 2+ → гипербола y=A/x+B
    /// - Кэш функций интерполяции
    /// </summary>
    public class NormStorage
    {
        private readonly string _storageFile;
        private Dictionary<string, NormData> _normsData;
        private StorageMetadata _metadata;

        public NormStorage(string storageFile = "norms_storage.json")
        {
            _storageFile = storageFile;
            _normsData = new Dictionary<string, NormData>();
            _metadata = new StorageMetadata();
            
            LoadStorage();
        }

        #region Загрузка и сохранение хранилища
        // Python: load_storage(), save_storage()

        /// <summary>
        /// Загружает данные из JSON файла
        /// Python: load_storage()
        /// </summary>
        public void LoadStorage()
        {
            if (!File.Exists(_storageFile))
            {
                Log.Information($"Файл хранилища {_storageFile} не найден, создаем новое");
                return;
            }

            try
            {
                var json = File.ReadAllText(_storageFile);
                var data = JsonConvert.DeserializeObject<StorageContainer>(json);

                _normsData = data?.NormsData ?? new Dictionary<string, NormData>();
                _metadata = data?.Metadata ?? new StorageMetadata();

                // Восстанавливаем интерполяционные функции
                RebuildInterpolationFunctions();

                Log.Information($"Загружено {_normsData.Count} норм из {_storageFile}");
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка загрузки хранилища норм: {ex.Message}");
                _normsData = new Dictionary<string, NormData>();
            }
        }

        /// <summary>
        /// Сохраняет данные в JSON файл
        /// Python: save_storage()
        /// </summary>
        public void SaveStorage()
        {
            try
            {
                _metadata.TotalNorms = _normsData.Count;
                _metadata.LastUpdated = DateTime.UtcNow;

                // Подсчет норм по типам
                _metadata.NormTypes = _normsData
                    .GroupBy(kv => kv.Value.NormType ?? "Unknown")
                    .ToDictionary(g => g.Key, g => g.Count());

                var container = new StorageContainer
                {
                    NormsData = _normsData,
                    Metadata = _metadata
                };

                var json = JsonConvert.SerializeObject(container, Formatting.Indented);
                File.WriteAllText(_storageFile, json);

                Log.Information($"Хранилище норм сохранено в {_storageFile}");
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка сохранения хранилища норм: {ex.Message}");
            }
        }

        // Вспомогательный класс для сериализации
        private class StorageContainer
        {
            public Dictionary<string, NormData> NormsData { get; set; }
            public StorageMetadata Metadata { get; set; }
        }

        #endregion

        #region Управление нормами
        // Python: add_or_update_norms(), get_norm(), get_all_norms()

        /// <summary>
        /// Добавляет или обновляет нормы
        /// Python: add_or_update_norms()
        /// </summary>
        /// <returns>Словарь norm_id → статус ("new", "updated", "unchanged")</returns>
        public Dictionary<string, string> AddOrUpdateNorms(Dictionary<string, NormData> newNorms)
        {
            Log.Information($"Добавление/обновление {newNorms.Count} норм");
            var updateResults = new Dictionary<string, string>();

            foreach (var kvp in newNorms)
            {
                string normId = kvp.Key;
                NormData normData = kvp.Value;

                if (_normsData.ContainsKey(normId))
                {
                    if (NormsAreDifferent(normData, _normsData[normId]))
                    {
                        _normsData[normId] = normData;
                        updateResults[normId] = "updated";
                    }
                    else
                    {
                        updateResults[normId] = "unchanged";
                    }
                }
                else
                {
                    _normsData[normId] = normData;
                    updateResults[normId] = "new";
                }
            }

            // Пересоздаем функции только для измененных норм
            var changedIds = updateResults
                .Where(kv => kv.Value == "new" || kv.Value == "updated")
                .Select(kv => kv.Key)
                .ToList();

            RebuildInterpolationFunctions(changedIds);
            SaveStorage();

            // Логирование результатов
            var counts = updateResults.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.Count());
            Log.Information($"Результат обновления: {string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}"))}");

            return updateResults;
        }

        /// <summary>
        /// Получить норму по ID
        /// Python: get_norm()
        /// </summary>
        public NormData GetNorm(string normId)
        {
            return _normsData.TryGetValue(normId, out var normData) ? normData : null;
        }

        /// <summary>
        /// Получить все нормы
        /// Python: get_all_norms()
        /// </summary>
        public Dictionary<string, NormData> GetAllNorms()
        {
            return new Dictionary<string, NormData>(_normsData);
        }

        #endregion

        #region Интерполяция норм
        // Python: _create_interpolation_function(), get_norm_function(), interpolate_norm_value()

        /// <summary>
        /// Создает функцию гиперболической интерполяции
        /// Python: _create_interpolation_function()
        /// 
        /// Логика:
        /// - 1 точка  → константа y = c
        /// - 2 точки  → гипербола y = A/x + B
        /// - 3+ точек → подгонка гиперболы МНК (с fallback на 2 точки)
        /// </summary>
        private Func<double, double> CreateInterpolationFunction(List<NormPoint> points)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentException("Недостаточно точек для интерполяции");

            // Сортируем по нагрузке
            var sortedPoints = points.OrderBy(p => p.LoadValue).ToList();
            
            // Проверка: X должен быть > 0 для гиперболы
            if (sortedPoints.Any(p => p.LoadValue <= 0))
                throw new ArgumentException("Значения нагрузки (X) должны быть положительными для гиперболы");

            // Случай 1: одна точка → константа
            if (sortedPoints.Count == 1)
            {
                double constantValue = sortedPoints[0].EnergyValue;
                return x => constantValue;
            }

            // Случай 2: две точки → гипербола y = A/x + B
            if (sortedPoints.Count == 2)
            {
                return CreateTwoPointHyperbola(sortedPoints);
            }

            // Случай 3+: подгонка гиперболы МНК
            try
            {
                return CreateMultiPointHyperbola(sortedPoints);
            }
            catch (Exception ex)
            {
                Log.Warning($"Ошибка подгонки гиперболы (3+ точки): {ex.Message}, fallback на 2 точки");
                return CreateTwoPointHyperbola(sortedPoints.Take(2).ToList());
            }
        }

        /// <summary>
        /// Создает гиперболу по 2 точкам: y = A/x + B
        /// Python: случай len(pts) == 2 в _create_interpolation_function()
        /// </summary>
        private Func<double, double> CreateTwoPointHyperbola(List<NormPoint> points)
        {
            double x1 = points[0].LoadValue;
            double y1 = points[0].EnergyValue;
            double x2 = points[1].LoadValue;
            double y2 = points[1].EnergyValue;

            try
            {
                // Если точки слишком близко по X - используем среднее значение Y
                if (Math.Abs(x2 - x1) < 1e-12)
                {
                    double avg = (y1 + y2) / 2.0;
                    return x => avg;
                }

                // Вычисляем коэффициенты гиперболы
                // y1 = A/x1 + B  =>  A = (y1 - B) * x1
                // y2 = A/x2 + B  =>  A = (y2 - B) * x2
                // (y1 - B) * x1 = (y2 - B) * x2
                // y1*x1 - B*x1 = y2*x2 - B*x2
                // B(x2 - x1) = y2*x2 - y1*x1
                double A = (y1 - y2) * x1 * x2 / (x2 - x1);
                double B = (y2 * x2 - y1 * x1) / (x2 - x1);

                return x =>
                {
                    if (x <= 0) return y1; // Защита от деления на 0
                    return A / x + B;
                };
            }
            catch (Exception ex)
            {
                Log.Warning($"Ошибка гиперболы (2 точки): {ex.Message}, используем линейную интерполяцию");
                // Fallback: линейная интерполяция
                double k = (y2 - y1) / (x2 - x1);
                return x => y1 + k * (x - x1);
            }
        }

        /// <summary>
        /// Подгонка гиперболы по 3+ точкам методом наименьших квадратов
        /// Python: случай len(pts) >= 3 с использованием scipy.optimize.curve_fit
        /// </summary>
        private Func<double, double> CreateMultiPointHyperbola(List<NormPoint> points)
        {
            // Извлекаем массивы X и Y
            double[] xData = points.Select(p => p.LoadValue).ToArray();
            double[] yData = points.Select(p => p.EnergyValue).ToArray();

            // Модель: y = A/x + B
            // Преобразуем в линейную форму: y = A * (1/x) + B
            // Создаем матрицу для МНК
            double[] invX = xData.Select(x => 1.0 / x).ToArray();

            // Используем простой МНК для нахождения A и B
            // Система: [sum(1/x²)  sum(1/x)] [A]   [sum(y/x)]
            //          [sum(1/x)   n      ] [B] = [sum(y)  ]
            
            int n = xData.Length;
            double sum_inv_x = invX.Sum();
            double sum_inv_x2 = invX.Select(v => v * v).Sum();
            double sum_y = yData.Sum();
            double sum_y_inv_x = yData.Zip(invX, (y, ix) => y * ix).Sum();

            double det = sum_inv_x2 * n - sum_inv_x * sum_inv_x;
            
            if (Math.Abs(det) < 1e-10)
                throw new InvalidOperationException("Вырожденная матрица при подгонке гиперболы");

            double A = (sum_y_inv_x * n - sum_y * sum_inv_x) / det;
            double B = (sum_inv_x2 * sum_y - sum_inv_x * sum_y_inv_x) / det;

            return x =>
            {
                if (x <= 0) return yData[0]; // Защита
                return A / x + B;
            };
        }

        /// <summary>
        /// Получает функцию интерполяции для нормы (из кэша или создает новую)
        /// Python: get_norm_function()
        /// </summary>
        public Func<double, double> GetNormFunction(string normId)
        {
            var normData = GetNorm(normId);
            if (normData == null || normData.Points.Count == 0)
                return null;

            // Если функция уже закэширована - возвращаем её
            if (normData.InterpolationFunction != null)
                return normData.InterpolationFunction;

            // Создаем и кэшируем функцию
            try
            {
                normData.InterpolationFunction = CreateInterpolationFunction(normData.Points);
                return normData.InterpolationFunction;
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка создания функции интерполяции для нормы {normId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Интерполирует значение нормы для заданной нагрузки
        /// Python: interpolate_norm_value()
        /// </summary>
        public double? InterpolateNormValue(string normId, double loadValue)
        {
            var func = GetNormFunction(normId);
            if (func == null)
                return null;

            try
            {
                return func(loadValue);
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка интерполяции для нормы {normId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Пересоздает интерполяционные функции
        /// Python: _rebuild_interpolation_functions()
        /// </summary>
        private void RebuildInterpolationFunctions(List<string> normIds = null)
        {
            var idsToRebuild = normIds ?? _normsData.Keys.ToList();
            
            Log.Debug($"Пересоздание функций интерполяции: {idsToRebuild.Count} норм");

            foreach (var normId in idsToRebuild)
            {
                if (!_normsData.TryGetValue(normId, out var normData))
                    continue;

                try
                {
                    if (normData.Points.Count > 0)
                    {
                        normData.InterpolationFunction = CreateInterpolationFunction(normData.Points);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"Ошибка создания функции для {normId}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Поиск и фильтрация норм
        // Python: search_norms(), get_norms_by_type()

        /// <summary>
        /// Поиск норм по критериям
        /// Python: search_norms()
        /// </summary>
        public Dictionary<string, NormData> SearchNorms(
            string normType = null,
            string normIdPattern = null,
            Dictionary<string, object> baseDataCriteria = null)
        {
            var results = new Dictionary<string, NormData>();

            foreach (var kvp in _normsData)
            {
                string normId = kvp.Key;
                NormData normData = kvp.Value;
                bool matches = true;

                // Проверка по типу нормы
                if (normType != null && normData.NormType != normType)
                {
                    matches = false;
                }

                // Проверка по паттерну ID
                if (normIdPattern != null && !normId.Contains(normIdPattern))
                {
                    matches = false;
                }

                // Проверка по базовым данным
                if (baseDataCriteria != null && matches)
                {
                    foreach (var criterion in baseDataCriteria)
                    {
                        if (!normData.BaseData.TryGetValue(criterion.Key, out var value) ||
                            !value.Equals(criterion.Value))
                        {
                            matches = false;
                            break;
                        }
                    }
                }

                if (matches)
                {
                    results[normId] = normData;
                }
            }

            return results;
        }

        /// <summary>
        /// Получает нормы по типу
        /// Python: get_norms_by_type()
        /// </summary>
        public Dictionary<string, NormData> GetNormsByType(string normType)
        {
            return SearchNorms(normType: normType);
        }

        #endregion

        #region Валидация и статистика
        // Python: validate_norms(), get_norm_statistics(), get_storage_info()

        /// <summary>
        /// Валидация норм в хранилище
        /// Python: validate_norms()
        /// </summary>
        public ValidationResults ValidateNorms()
        {
            var results = new ValidationResults();

            foreach (var kvp in _normsData)
            {
                string normId = kvp.Key;
                NormData normData = kvp.Value;

                try
                {
                    var points = normData.Points;

                    // Проверка: минимум 1 точка
                    if (points.Count < 1)
                    {
                        results.Invalid.Add($"Норма {normId}: нет точек");
                        continue;
                    }

                    // Проверка: X > 0, Y > 0
                    if (points.Any(p => p.LoadValue <= 0 || p.EnergyValue <= 0))
                    {
                        results.Invalid.Add($"Норма {normId}: отрицательные или нулевые значения");
                        continue;
                    }

                    // Проверка: можно ли построить функцию
                    try
                    {
                        CreateInterpolationFunction(points);
                        results.Valid.Add(normId);
                    }
                    catch (Exception ex)
                    {
                        results.Invalid.Add($"Норма {normId}: ошибка интерполяции — {ex.Message}");
                        continue;
                    }

                    // Предупреждения
                    if (points.Count > 20)
                    {
                        results.Warnings.Add($"Норма {normId}: много точек ({points.Count})");
                    }
                    else if (points.Count == 1)
                    {
                        results.Warnings.Add($"Норма {normId}: только одна точка (константа)");
                    }
                }
                catch (Exception ex)
                {
                    results.Invalid.Add($"Норма {normId}: ошибка валидации — {ex.Message}");
                }
            }

            Log.Information($"Валидация: валидных={results.Valid.Count}, " +
                          $"невалидных={results.Invalid.Count}, " +
                          $"предупреждений={results.Warnings.Count}");

            return results;
        }

        /// <summary>
        /// Статистика по нормам
        /// Python: get_norm_statistics()
        /// </summary>
        public NormStatistics GetNormStatistics()
        {
            var stats = new NormStatistics
            {
                TotalNorms = _normsData.Count,
                ByType = _normsData.GroupBy(kv => kv.Value.NormType ?? "Unknown")
                                   .ToDictionary(g => g.Key, g => g.Count())
            };

            if (_normsData.Count > 0)
            {
                var allPoints = _normsData.SelectMany(kv => kv.Value.Points).ToList();
                stats.AvgPointsPerNorm = (double)allPoints.Count / _normsData.Count;

                if (allPoints.Any())
                {
                    stats.LoadRange = (allPoints.Min(p => p.LoadValue), allPoints.Max(p => p.LoadValue));
                    stats.EnergyRange = (allPoints.Min(p => p.EnergyValue), allPoints.Max(p => p.EnergyValue));
                }
            }

            return stats;
        }

        /// <summary>
        /// Информация о хранилище
        /// Python: get_storage_info()
        /// </summary>
        public StorageInfo GetStorageInfo()
        {
            long fileSize = File.Exists(_storageFile) ? new FileInfo(_storageFile).Length : 0;

            return new StorageInfo
            {
                StorageFile = _storageFile,
                FileSizeMB = fileSize / (1024.0 * 1024.0),
                Version = _metadata.Version,
                TotalNorms = _metadata.TotalNorms,
                LastUpdated = _metadata.LastUpdated,
                NormTypes = _metadata.NormTypes,
                CachedFunctions = _normsData.Count(kv => kv.Value.InterpolationFunction != null)
            };
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Сравнивает две нормы на предмет различий
        /// Python: _norms_are_different()
        /// </summary>
        private bool NormsAreDifferent(NormData norm1, NormData norm2)
        {
            try
            {
                // Сравниваем точки
                var points1 = new HashSet<NormPoint>(norm1.Points);
                var points2 = new HashSet<NormPoint>(norm2.Points);
                if (!points1.SetEquals(points2))
                    return true;

                // Сравниваем базовые данные
                var allKeys = norm1.BaseData.Keys.Union(norm2.BaseData.Keys);
                foreach (var key in allKeys)
                {
                    var val1 = norm1.BaseData.TryGetValue(key, out var v1) ? v1 : null;
                    var val2 = norm2.BaseData.TryGetValue(key, out var v2) ? v2 : null;
                    if (!Equals(val1, val2))
                        return true;
                }

                // Сравниваем тип и описание
                if (norm1.NormType != norm2.NormType || norm1.Description != norm2.Description)
                    return true;

                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"Ошибка сравнения норм: {ex.Message}");
                return true; // В случае ошибки считаем нормы разными
            }
        }

        #endregion
    }

    #region Вспомогательные классы для результатов

    public class ValidationResults
    {
        public List<string> Valid { get; set; } = new List<string>();
        public List<string> Invalid { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public class NormStatistics
    {
        public int TotalNorms { get; set; }
        public Dictionary<string, int> ByType { get; set; } = new Dictionary<string, int>();
        public double AvgPointsPerNorm { get; set; }
        public (double Min, double Max) LoadRange { get; set; }
        public (double Min, double Max) EnergyRange { get; set; }
    }

    public class StorageInfo
    {
        public string StorageFile { get; set; }
        public double FileSizeMB { get; set; }
        public string Version { get; set; }
        public int TotalNorms { get; set; }
        public DateTime? LastUpdated { get; set; }
        public Dictionary<string, int> NormTypes { get; set; }
        public int CachedFunctions { get; set; }
    }

    #endregion
}
