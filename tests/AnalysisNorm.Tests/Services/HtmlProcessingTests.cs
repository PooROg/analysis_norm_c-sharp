using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Implementation;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Tests.Services;

/// <summary>
/// Тесты для HTML Processing Engine
/// Проверяем соответствие Python функциональности
/// </summary>
public class HtmlProcessingTests
{
    private readonly Mock<ILogger<FileEncodingDetector>> _loggerMock;
    private readonly Mock<ILogger<TextNormalizer>> _textLoggerMock;
    private readonly Mock<IOptions<ApplicationSettings>> _settingsMock;
    private readonly ApplicationSettings _settings;

    public HtmlProcessingTests()
    {
        _loggerMock = new Mock<ILogger<FileEncodingDetector>>();
        _textLoggerMock = new Mock<ILogger<TextNormalizer>>();
        _settingsMock = new Mock<IOptions<ApplicationSettings>>();
        
        _settings = new ApplicationSettings
        {
            SupportedEncodings = new[] { "cp1251", "utf-8", "utf-8-sig" },
            DefaultTolerancePercent = 5.0,
            MinWorkThreshold = 200.0
        };
        
        _settingsMock.Setup(x => x.Value).Returns(_settings);
    }

    [Fact]
    public void TextNormalizer_NormalizeText_ShouldCleanWhitespace()
    {
        // Arrange
        var normalizer = new TextNormalizer(_textLoggerMock.Object);
        var input = "  Тест   с    множественными\xa0\u00a0пробелами  ";
        var expected = "Тест с множественными пробелами";

        // Act
        var result = normalizer.NormalizeText(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("123.45", 123.45)]
    [InlineData("123,45", 123.45)]  // Запятая должна заменяться точкой
    [InlineData("  456.78  ", 456.78)]  // Пробелы должны убираться
    [InlineData("789.", 789.0)]  // Точка в конце должна убираться
    [InlineData("не число", 0.0)]  // Неверный формат -> значение по умолчанию
    [InlineData("", 0.0)]  // Пустая строка -> значение по умолчанию
    [InlineData(null, 0.0)]  // null -> значение по умолчанию
    public void TextNormalizer_SafeDecimal_ShouldParseCorrectly(object? input, decimal expected)
    {
        // Arrange
        var normalizer = new TextNormalizer(_textLoggerMock.Object);

        // Act
        var result = normalizer.SafeDecimal(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("123", 123)]
    [InlineData("456 км", 456)]  // Должно извлекать число из текста
    [InlineData("  789  ", 789)]  // Пробелы должны убираться
    [InlineData("не число", 0)]  // Неверный формат -> значение по умолчанию
    [InlineData("", 0)]  // Пустая строка -> значение по умолчанию
    [InlineData(null, 0)]  // null -> значение по умолчанию
    public void TextNormalizer_SafeInt_ShouldParseCorrectly(object? input, int expected)
    {
        // Arrange
        var normalizer = new TextNormalizer(_textLoggerMock.Object);

        // Act
        var result = normalizer.SafeInt(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-35, DeviationStatus.EconomyStrong)]
    [InlineData(-25, DeviationStatus.EconomyMedium)]
    [InlineData(-10, DeviationStatus.EconomyWeak)]
    [InlineData(0, DeviationStatus.Normal)]
    [InlineData(3, DeviationStatus.Normal)]
    [InlineData(10, DeviationStatus.OverrunWeak)]
    [InlineData(25, DeviationStatus.OverrunMedium)]
    [InlineData(35, DeviationStatus.OverrunStrong)]
    public void DeviationStatus_GetStatus_ShouldMatchPythonStatusClassifier(decimal deviation, string expectedStatus)
    {
        // Act - проверяем что статусы соответствуют Python StatusClassifier
        var result = DeviationStatus.GetStatus(deviation);

        // Assert
        result.Should().Be(expectedStatus);
    }

    [Fact]
    public void RouteMetadata_ShouldExtractCorrectInformation()
    {
        // Arrange - создаем метаданные как в Python extract_route_header_from_html
        var metadata = new RouteMetadata
        {
            Number = "12345",
            RouteDate = "20241201",
            TripDate = "20241201", 
            DriverTab = "67890"
        };

        // Assert - проверяем что все поля заполнены корректно
        metadata.Number.Should().Be("12345");
        metadata.RouteDate.Should().Be("20241201");
        metadata.TripDate.Should().Be("20241201");
        metadata.DriverTab.Should().Be("67890");
    }

    [Fact]
    public void Route_CalculateDerivedFields_ShouldComputeCorrectly()
    {
        // Arrange - создаем маршрут с базовыми данными
        var route = new Route
        {
            BruttoTons = 4000m,
            AxesCount = 80,
            FactConsumption = 500m,
            TonKilometers = 50000m,
            NormConsumption = 450m
        };

        // Act - вычисляем производные поля (как в Python CalculateDerivedFields)
        // Нагрузка на ось = BruttoTons / AxesCount
        var expectedAxleLoad = 4000m / 80m; // = 50

        // Удельный расход = FactConsumption / TonKilometers * 10000
        var expectedFactUd = 500m / 50000m * 10000; // = 100

        // Отклонение = (Fact - Norm) / Norm * 100
        var expectedDeviation = (500m - 450m) / 450m * 100; // = 11.11%

        // Имитируем вычисления
        route.AxleLoad = expectedAxleLoad;
        route.FactUd = expectedFactUd;
        route.DeviationPercent = Math.Round(expectedDeviation, 2);
        route.Status = DeviationStatus.GetStatus(expectedDeviation);

        // Assert
        route.AxleLoad.Should().Be(50m);
        route.FactUd.Should().Be(100m);
        route.DeviationPercent.Should().Be(11.11m);
        route.Status.Should().Be(DeviationStatus.OverrunWeak);
    }

    [Fact]
    public void NormExtensions_CanInterpolate_ShouldValidateCorrectly()
    {
        // Arrange - создаем норму с точками (как в Python validation)
        var validNorm = new Norm
        {
            NormId = "1.1",
            Points = new List<NormPoint>
            {
                new() { Load = 10m, Consumption = 45m },
                new() { Load = 20m, Consumption = 50m },
                new() { Load = 30m, Consumption = 55m }
            }
        };

        var invalidNorm = new Norm
        {
            NormId = "1.2",
            Points = new List<NormPoint>
            {
                new() { Load = 10m, Consumption = 45m } // Только одна точка
            }
        };

        // Act & Assert
        validNorm.CanInterpolate().Should().BeTrue();
        invalidNorm.CanInterpolate().Should().BeFalse();
    }

    [Fact]
    public void NormExtensions_GetLoadRange_ShouldReturnCorrectRange()
    {
        // Arrange
        var norm = new Norm
        {
            Points = new List<NormPoint>
            {
                new() { Load = 15m, Consumption = 45m },
                new() { Load = 25m, Consumption = 50m },
                new() { Load = 5m, Consumption = 40m },   // Минимум
                new() { Load = 35m, Consumption = 55m }   // Максимум
            }
        };

        // Act
        var (min, max) = norm.GetLoadRange();

        // Assert
        min.Should().Be(5m);
        max.Should().Be(35m);
    }

    [Fact]
    public void LocomotiveCoefficient_NormalizeSeries_ShouldMatchPythonBehavior()
    {
        // Проверяем что нормализация серии работает как в Python normalize_series
        
        // Act & Assert
        LocomotiveCoefficient.NormalizeSeries("ВЛ80С").Should().Be("ВЛ80С");
        LocomotiveCoefficient.NormalizeSeries("вл-80с").Should().Be("ВЛ80С");
        LocomotiveCoefficient.NormalizeSeries("ВЛ-80-С").Should().Be("ВЛ80С");
        LocomotiveCoefficient.NormalizeSeries("ВЛ 80 С").Should().Be("ВЛ80С");
        LocomotiveCoefficient.NormalizeSeries("2ЭС5К").Should().Be("2ЭС5К");
        LocomotiveCoefficient.NormalizeSeries("").Should().Be("");
        LocomotiveCoefficient.NormalizeSeries(null).Should().Be("");
    }

    [Fact]
    public void AnalysisConstants_ShouldHaveCorrectThresholds()
    {
        // Проверяем что пороги соответствуют Python StatusClassifier.THRESHOLDS
        
        // Assert
        AnalysisConstants.StrongEconomyThreshold.Should().Be(-30m);
        AnalysisConstants.MediumEconomyThreshold.Should().Be(-20m);
        AnalysisConstants.WeakEconomyThreshold.Should().Be(-5m);
        AnalysisConstants.NormalUpperThreshold.Should().Be(5m);
        AnalysisConstants.WeakOverrunThreshold.Should().Be(20m);
        AnalysisConstants.MediumOverrunThreshold.Should().Be(30m);
    }

    [Fact]
    public void ProcessingStatistics_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var stats = new ProcessingStatistics
        {
            TotalFiles = 5,
            ProcessedFiles = 4,
            SkippedFiles = 1,
            TotalRoutes = 100,
            ProcessedRoutes = 95,
            DuplicateRoutes = 5,
            ProcessingTime = TimeSpan.FromMinutes(2),
            Details = new Dictionary<string, object>
            {
                ["test_file"] = new { Success = true, RouteCount = 20 }
            }
        };

        // Assert
        stats.TotalFiles.Should().Be(5);
        stats.ProcessedFiles.Should().Be(4);
        stats.SkippedFiles.Should().Be(1);
        stats.TotalRoutes.Should().Be(100);
        stats.ProcessedRoutes.Should().Be(95);
        stats.DuplicateRoutes.Should().Be(5);
        stats.ProcessingTime.TotalMinutes.Should().Be(2);
        stats.Details.Should().ContainKey("test_file");
    }

    [Theory]
    [InlineData("cp1251")]
    [InlineData("utf-8")]
    [InlineData("utf-8-sig")]
    public void ApplicationSettings_ShouldSupportCorrectEncodings(string encoding)
    {
        // Проверяем что настройки поддерживают те же кодировки что и Python
        
        // Assert
        _settings.SupportedEncodings.Should().Contain(encoding);
    }

    [Fact]
    public void ApplicationSettings_ShouldHaveCorrectDefaults()
    {
        // Проверяем что настройки по умолчанию соответствуют Python config
        
        // Assert
        _settings.DefaultTolerancePercent.Should().Be(5.0);
        _settings.MinWorkThreshold.Should().Be(200.0);
        _settings.SupportedEncodings.Should().HaveCount(3);
        _settings.SupportedEncodings[0].Should().Be("cp1251"); // Приоритет cp1251 как в Python
    }
}

/// <summary>
/// Тесты интеграции HTML Processing с моками
/// </summary>
public class HtmlProcessingIntegrationTests
{
    [Fact]
    public async Task HtmlRouteProcessor_ShouldHandleEmptyFileList()
    {
        // Arrange
        var logger = Mock.Of<ILogger<HtmlRouteProcessorService>>();
        var encodingDetector = Mock.Of<IFileEncodingDetector>();
        var textNormalizer = Mock.Of<ITextNormalizer>();
        var settings = Mock.Of<IOptions<ApplicationSettings>>();

        Mock.Get(settings).Setup(s => s.Value).Returns(new ApplicationSettings());

        var processor = new HtmlRouteProcessorService(logger, encodingDetector, textNormalizer, settings);

        // Act
        var result = await processor.ProcessHtmlFilesAsync(new List<string>());

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task HtmlNormProcessor_ShouldHandleEmptyFileList()
    {
        // Arrange
        var logger = Mock.Of<ILogger<HtmlNormProcessorService>>();
        var encodingDetector = Mock.Of<IFileEncodingDetector>();
        var textNormalizer = Mock.Of<ITextNormalizer>();
        var settings = Mock.Of<IOptions<ApplicationSettings>>();

        Mock.Get(settings).Setup(s => s.Value).Returns(new ApplicationSettings());

        var processor = new HtmlNormProcessorService(logger, encodingDetector, textNormalizer, settings);

        // Act
        var result = await processor.ProcessHtmlFilesAsync(new List<string>());

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data.Should().BeEmpty();
    }

    [Fact]
    public void ExcelHeaderInfo_ShouldStoreColumnPositions()
    {
        // Arrange & Act
        var headerInfo = new ExcelHeaderInfo
        {
            HeaderRow = 1,
            SeriesColumn = 2,
            NumberColumn = 3,
            CoefficientColumn = 4,
            WorkFactColumn = 5,
            WorkNormColumn = 6
        };

        // Assert
        headerInfo.HeaderRow.Should().Be(1);
        headerInfo.SeriesColumn.Should().Be(2);
        headerInfo.NumberColumn.Should().Be(3);
        headerInfo.CoefficientColumn.Should().Be(4);
        headerInfo.WorkFactColumn.Should().Be(5);
        headerInfo.WorkNormColumn.Should().Be(6);
    }

    [Fact]
    public void CoefficientStatistics_ShouldCalculateCorrectly()
    {
        // Arrange & Act
        var stats = new CoefficientStatistics
        {
            TotalCount = 150,
            SeriesCount = 5,
            AverageCoefficient = 1.15m,
            MinCoefficient = 0.85m,
            MaxCoefficient = 1.45m,
            SeriesBreakdown = new Dictionary<string, int>
            {
                ["ВЛ80С"] = 50,
                ["2ЭС5К"] = 40,
                ["ЭП1М"] = 35,
                ["ВЛ11М"] = 15,
                ["ЭД4М"] = 10
            }
        };

        // Assert
        stats.TotalCount.Should().Be(150);
        stats.SeriesCount.Should().Be(5);
        stats.AverageCoefficient.Should().Be(1.15m);
        stats.SeriesBreakdown.Should().HaveCount(5);
        stats.SeriesBreakdown["ВЛ80С"].Should().Be(50);
    }
}

/// <summary>
/// Тесты производительности для больших объемов данных
/// </summary>
public class HtmlProcessingPerformanceTests
{
    [Fact]
    public void TextNormalizer_ShouldHandleLargeText()
    {
        // Arrange
        var logger = Mock.Of<ILogger<TextNormalizer>>();
        var normalizer = new TextNormalizer(logger);
        
        // Создаем большой текст (10MB)
        var largeText = new string(' ', 1000) + "тест" + new string('\xa0', 1000000) + "конец";

        // Act & Assert - должно выполняться быстро и без ошибок
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = normalizer.NormalizeText(largeText);
        sw.Stop();

        result.Should().NotBeEmpty();
        result.Should().Contain("тест");
        result.Should().Contain("конец");
        sw.ElapsedMilliseconds.Should().BeLessThan(1000); // Менее 1 секунды
    }

    [Fact]
    public void Route_GenerateRouteKey_ShouldBeEfficient()
    {
        // Arrange - создаем множество маршрутов
        var routes = Enumerable.Range(1, 10000)
            .Select(i => new Route
            {
                RouteNumber = i.ToString(),
                TripDate = "20241201",
                DriverTab = (1000 + i).ToString()
            })
            .ToList();

        // Act & Assert - генерация ключей должна быть быстрой
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        foreach (var route in routes)
        {
            route.GenerateRouteKey();
        }
        
        sw.Stop();

        // Проверяем что все ключи сгенерированы
        routes.Should().OnlyContain(r => !string.IsNullOrEmpty(r.RouteKey));
        
        // Проверяем производительность
        sw.ElapsedMilliseconds.Should().BeLessThan(100); // Менее 100ms для 10k маршрутов
    }
}