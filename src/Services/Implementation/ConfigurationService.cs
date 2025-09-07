// Services/Implementation/ConfigurationService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using AnalysisNorm.Services.Interfaces;
using AnalysisNorm.Configuration;
using AnalysisNorm.Infrastructure.Logging;
using System.Text.Json;

namespace AnalysisNorm.Services.Implementation;

/// <summary>
/// CHAT 4: Система конфигурации приложения
/// Управляет настройками экспорта, парсинга, производительности и диагностики
/// Поддерживает горячую перезагрузку и валидацию конфигурации
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly IApplicationLogger _logger;
    private readonly Dictionary<Type, object> _configCache = new();
    private readonly object _lockObject = new();

    // Настройки по умолчанию для различных компонентов
    private static readonly Dictionary<Type, object> DefaultConfigurations = new()
    {
        { typeof(ExcelExportConfiguration), ExcelExportConfiguration.Default },
        { typeof(HtmlParsingConfiguration), HtmlParsingConfiguration.Default },
        { typeof(PerformanceConfiguration), PerformanceConfiguration.Default },
        { typeof(DiagnosticsConfiguration), DiagnosticsConfiguration.Default }
    };

    public ConfigurationService(IConfiguration configuration, IApplicationLogger logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.LogInformation("ConfigurationService инициализирован");
        ValidateConfiguration();
    }

    /// <summary>
    /// Получение конфигурации определенного типа с кэшированием
    /// </summary>
    public T GetConfiguration<T>() where T : class, new()
    {
        lock (_lockObject)
        {
            if (_configCache.TryGetValue(typeof(T), out var cachedConfig))
            {
                return (T)cachedConfig;
            }

            try
            {
                // Пытаемся загрузить из appsettings.json
                var sectionName = GetConfigurationSectionName<T>();
                var config = _configuration.GetSection(sectionName).Get<T>();
                
                // Если конфигурация не найдена, используем значения по умолчанию
                config ??= GetDefaultConfiguration<T>();
                
                // Кэшируем конфигурацию
                _configCache[typeof(T)] = config;
                
                _logger.LogDebug("Конфигурация {ConfigType} загружена из секции {SectionName}", 
                    typeof(T).Name, sectionName);
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ошибка загрузки конфигурации {ConfigType}, используются значения по умолчанию", 
                    typeof(T).Name);
                
                var defaultConfig = GetDefaultConfiguration<T>();
                _configCache[typeof(T)] = defaultConfig;
                return defaultConfig;
            }
        }
    }

    /// <summary>
    /// Обновление конфигурации с валидацией
    /// </summary>
    public async Task<bool> UpdateConfigurationAsync<T>(T newConfiguration) where T : class, new()
    {
        try
        {
            // Валидация конфигурации
            var validationResult = ValidateConfiguration(newConfiguration);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Конфигурация {ConfigType} не прошла валидацию: {Errors}", 
                    typeof(T).Name, string.Join(", ", validationResult.Errors));
                return false;
            }

            lock (_lockObject)
            {
                // Обновляем кэш
                _configCache[typeof(T)] = newConfiguration;
            }

            // Сохраняем в файл конфигурации (опционально)
            await SaveConfigurationToFileAsync(newConfiguration);
            
            _logger.LogInformation("Конфигурация {ConfigType} успешно обновлена", typeof(T).Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления конфигурации {ConfigType}", typeof(T).Name);
            return false;
        }
    }

    /// <summary>
    /// Сброс конфигурации к значениям по умолчанию
    /// </summary>
    public void ResetToDefaults<T>() where T : class, new()
    {
        lock (_lockObject)
        {
            var defaultConfig = GetDefaultConfiguration<T>();
            _configCache[typeof(T)] = defaultConfig;
        }
        
        _logger.LogInformation("Конфигурация {ConfigType} сброшена к значениям по умолчанию", typeof(T).Name);
    }

    /// <summary>
    /// Получение всех доступных конфигураций для диагностики
    /// </summary>
    public Dictionary<string, object> GetAllConfigurations()
    {
        lock (_lockObject)
        {
            return _configCache.ToDictionary(
                kvp => kvp.Key.Name,
                kvp => kvp.Value
            );
        }
    }

    /// <summary>
    /// Экспорт текущих настроек в JSON для резервного копирования
    /// </summary>
    public async Task<string> ExportConfigurationAsync()
    {
        try
        {
            var allConfigs = GetAllConfigurations();
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(allConfigs, jsonOptions);
            _logger.LogDebug("Конфигурация экспортирована в JSON");
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка экспорта конфигурации");
            throw;
        }
    }

    /// <summary>
    /// Импорт конфигураций из JSON
    /// </summary>
    public async Task<bool> ImportConfigurationAsync(string jsonConfiguration)
    {
        try
        {
            var configs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonConfiguration);
            if (configs == null)
            {
                _logger.LogWarning("Неверный формат JSON конфигурации");
                return false;
            }

            var importedCount = 0;
            foreach (var (configTypeName, configElement) in configs)
            {
                if (TryImportSpecificConfiguration(configTypeName, configElement))
                {
                    importedCount++;
                }
            }

            _logger.LogInformation("Импортировано конфигураций: {Count} из {Total}", 
                importedCount, configs.Count);
            return importedCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка импорта конфигурации");
            return false;
        }
    }

    /// <summary>
    /// Валидация общей конфигурации приложения
    /// </summary>
    private void ValidateConfiguration()
    {
        try
        {
            // Проверяем основные секции конфигурации
            var requiredSections = new[] { "ExcelExport", "HtmlParsing", "Performance", "Diagnostics" };
            var missingSections = new List<string>();

            foreach (var section in requiredSections)
            {
                if (!_configuration.GetSection(section).Exists())
                {
                    missingSections.Add(section);
                }
            }

            if (missingSections.Any())
            {
                _logger.LogWarning("Отсутствуют секции конфигурации: {MissingSections}, будут использованы значения по умолчанию", 
                    string.Join(", ", missingSections));
            }
            else
            {
                _logger.LogInformation("Все обязательные секции конфигурации найдены");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка валидации конфигурации");
        }
    }

    /// <summary>
    /// Валидация конкретной конфигурации
    /// </summary>
    private ConfigurationValidationResult ValidateConfiguration<T>(T configuration) where T : class
    {
        var errors = new List<string>();

        try
        {
            // Специфичная валидация для каждого типа конфигурации
            switch (configuration)
            {
                case ExcelExportConfiguration excelConfig:
                    if (string.IsNullOrWhiteSpace(excelConfig.DefaultOutputPath))
                        errors.Add("DefaultOutputPath не может быть пустым");
                    if (excelConfig.MaxRowsPerSheet <= 0)
                        errors.Add("MaxRowsPerSheet должно быть больше 0");
                    break;

                case HtmlParsingConfiguration htmlConfig:
                    if (htmlConfig.ParsingTimeoutMs <= 0)
                        errors.Add("ParsingTimeoutMs должно быть больше 0");
                    if (htmlConfig.MaxFileSizeMB <= 0)
                        errors.Add("MaxFileSizeMB должно быть больше 0");
                    break;

                case PerformanceConfiguration perfConfig:
                    if (perfConfig.MaxMemoryUsageMB <= 0)
                        errors.Add("MaxMemoryUsageMB должно быть больше 0");
                    if (perfConfig.MaxProcessingTimeSeconds <= 0)
                        errors.Add("MaxProcessingTimeSeconds должно быть больше 0");
                    break;

                case DiagnosticsConfiguration diagConfig:
                    if (string.IsNullOrWhiteSpace(diagConfig.LogLevel))
                        errors.Add("LogLevel не может быть пустым");
                    break;
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Ошибка валидации: {ex.Message}");
        }

        return new ConfigurationValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }

    /// <summary>
    /// Получение имени секции конфигурации по типу
    /// </summary>
    private string GetConfigurationSectionName<T>()
    {
        return typeof(T).Name.Replace("Configuration", "");
    }

    /// <summary>
    /// Получение конфигурации по умолчанию
    /// </summary>
    private T GetDefaultConfiguration<T>() where T : class, new()
    {
        if (DefaultConfigurations.TryGetValue(typeof(T), out var defaultConfig))
        {
            return (T)defaultConfig;
        }
        
        return new T();
    }

    /// <summary>
    /// Сохранение конфигурации в файл
    /// </summary>
    private async Task SaveConfigurationToFileAsync<T>(T configuration) where T : class
    {
        try
        {
            var sectionName = GetConfigurationSectionName<T>();
            var configFileName = $"config_{sectionName.ToLower()}.json";
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", configFileName);
            
            // Создаем папку если не существует
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            
            var jsonOptions = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var json = JsonSerializer.Serialize(configuration, jsonOptions);
            await File.WriteAllTextAsync(configPath, json);
            
            _logger.LogDebug("Конфигурация {ConfigType} сохранена в файл {ConfigPath}", 
                typeof(T).Name, configPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось сохранить конфигурацию {ConfigType} в файл", typeof(T).Name);
        }
    }

    /// <summary>
    /// Импорт конкретной конфигурации
    /// </summary>
    private bool TryImportSpecificConfiguration(string configTypeName, JsonElement configElement)
    {
        try
        {
            // Определяем тип конфигурации по имени
            var configType = configTypeName switch
            {
                "ExcelExportConfiguration" => typeof(ExcelExportConfiguration),
                "HtmlParsingConfiguration" => typeof(HtmlParsingConfiguration),
                "PerformanceConfiguration" => typeof(PerformanceConfiguration),
                "DiagnosticsConfiguration" => typeof(DiagnosticsConfiguration),
                _ => null
            };

            if (configType == null)
            {
                _logger.LogWarning("Неизвестный тип конфигурации: {ConfigTypeName}", configTypeName);
                return false;
            }

            // Десериализуем конфигурацию
            var config = JsonSerializer.Deserialize(configElement.GetRawText(), configType);
            if (config == null)
                return false;

            lock (_lockObject)
            {
                _configCache[configType] = config;
            }

            _logger.LogDebug("Конфигурация {ConfigType} импортирована", configTypeName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка импорта конфигурации {ConfigType}", configTypeName);
            return false;
        }
    }

    /// <summary>
    /// Получение диагностической информации о конфигурации
    /// </summary>
    public ConfigurationDiagnostics GetDiagnostics()
    {
        lock (_lockObject)
        {
            return new ConfigurationDiagnostics
            {
                LoadedConfigurationsCount = _configCache.Count,
                ConfigurationTypes = _configCache.Keys.Select(t => t.Name).ToList(),
                LastUpdateTime = DateTime.UtcNow, // В реальной реализации отслеживаем время обновления
                TotalConfigurationSize = EstimateConfigurationSize()
            };
        }
    }

    /// <summary>
    /// Оценка размера конфигурации в памяти
    /// </summary>
    private long EstimateConfigurationSize()
    {
        try
        {
            var allConfigs = GetAllConfigurations();
            var json = JsonSerializer.Serialize(allConfigs);
            return System.Text.Encoding.UTF8.GetByteCount(json);
        }
        catch
        {
            return 0;
        }
    }
}

/// <summary>
/// Результат валидации конфигурации
/// </summary>
public record ConfigurationValidationResult
{
    public bool IsValid { get; init; }
    public List<string> Errors { get; init; } = new();
}

/// <summary>
/// Диагностическая информация о конфигурации
/// </summary>
public record ConfigurationDiagnostics
{
    public int LoadedConfigurationsCount { get; init; }
    public List<string> ConfigurationTypes { get; init; } = new();
    public DateTime LastUpdateTime { get; init; }
    public long TotalConfigurationSize { get; init; }

    /// <summary>
    /// Размер конфигурации в человекочитаемом формате
    /// </summary>
    public string FormattedSize => TotalConfigurationSize switch
    {
        < 1024 => $"{TotalConfigurationSize} B",
        < 1024 * 1024 => $"{TotalConfigurationSize / 1024.0:F1} KB",
        _ => $"{TotalConfigurationSize / (1024.0 * 1024.0):F1} MB"
    };
}
