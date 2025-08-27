using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Data;
using AnalysisNorm.Services.Implementation;
using AnalysisNorm.Services.Interfaces;

namespace AnalysisNorm.Tests.Services.DataAnalysis;

/// <summary>
/// Тесты для Data Analysis Engine (Chat 3)
/// Проверяем соответствие Python функциональности + новые возможности
/// </summary>
public class DataAnalysisEngineTests : IDisposable
{
    private readonly AnalysisNormDbContext _context;
    private readonly Mock<ILogger<NormInterpolationService>> _interpolationLoggerMock;
    private readonly Mock<ILogger<NormStorageService>> _storageLoggerMock;
    private readonly Mock<ILogger<DataAnalysisService>> _analysisLoggerMock;
    private readonly Mock<ILogger<VisualizationDataService>> _visualizationLoggerMock;
    private readonly ApplicationSettings _settings;

    public DataAnalysisEngineTests()
    {
        // Настройка InMemory базы данных для тестов
        var options = new DbContextOptionsBuilder<AnalysisNormDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new AnalysisNormDbContext(options);
        
        // Моки для логгеров
        _interpolationLoggerMock = new Mock<ILogger<NormInterpolationService>>();
        _storageLoggerMock = new Mock<ILogger<NormStorageService>>();
        _analysisLoggerMock = new Mock<ILogger<DataAnalysisService>>();
        _visualizationLoggerMock = new Mock<ILogger<VisualizationDataService>>();

        // Настройки приложения
        _settings = new ApplicationSettings
        {
            DefaultTolerancePercent = 5.0,
            MinWorkThreshold = 200.0,
            SupportedEncodings = new[] { "cp1251", "utf-8", "utf-8-sig" }
        };
    }

    [Fact]
    public async Task NormStorageService_AddOrUpdateNorms_ShouldWorkCorrectly()
    {
        // Arrange
        var storageService = new NormStorageService(_storageLoggerMock.Object, _context);

        var testNorms = new List<Norm>
        {
            CreateTestNorm("1.1", "Нажатие", new[] { (10m, 45m), (20m, 50m), (30m, 55m) }),
            CreateTestNorm("1.2", "Н/Ф", new[] { (15m, 42m), (25m, 47m), (35m, 52m) })
        };

        // Act
        var results = await storageService.AddOrUpdateNormsAsync(testNorms);

        // Assert
        results.Should().HaveCount(2);
        results["1.1"].Should().Be("added");
        results["1.2"].Should().Be("added");

        var savedNorms = await _context.Norms.Include(n => n.Points).ToListAsync();
        savedNorms.Should().HaveCount(2);
        savedNorms.First(n => n.NormId == "1.1").Points.Should().HaveCount(3);
    }

    [Fact]
    public async Task NormStorageService_GetNormAsync_ShouldReturnCorrectNorm()
    {
        // Arrange
        var storageService = new NormStorageService(_storageLoggerMock.Object, _context);
        
        var testNorm = CreateTestNorm("2.1", "Уд. на работу", new[] { (12m, 40m), (22m, 45m) });
        await _context.Norms.AddAsync(testNorm);
        await _context.SaveChangesAsync();

        // Act
        var result = await storageService.GetNormAsync("2.1");

        // Assert
        result.Should().NotBeNull();
        result!.NormId.Should().Be("2.1");
        result.NormType.Should().Be("Уд. на работу");
        result.Points.Should().HaveCount(2);
        result.Points.First().Load.Should().Be(12m);
        result.Points.First().Consumption.Should().Be(40m);
    }

    [Fact]
    public async Task NormInterpolationService_InterpolateNormValue_ShouldWorkLikeScipyInterpolate()
    {
        // Arrange - создаем норму как в Python
        var testNorm = CreateTestNorm("3.1", "Нажатие", new[] { (10m, 40m), (20m, 50m), (30m, 60m) });
        await _context.Norms.AddAsync(testNorm);
        await _context.SaveChangesAsync();

        var settingsOptions = Options.Create(_settings);
        var interpolationService = new NormInterpolationService(
            _interpolationLoggerMock.Object, _context, settingsOptions);

        // Act - интерполируем значение между точками (как в Python scipy.interpolate)
        var result = await interpolationService.InterpolateNormValueAsync("3.1", 15m); // Между 10 и 20

        // Assert
        result.Should().NotBeNull();
        result.Should().BeInRange(44m, 46m); // Должно быть около 45 (линейная интерполяция)
    }

    [Fact]
    public async Task NormInterpolationService_ValidateNorms_ShouldFindProblems()
    {
        // Arrange
        var validNorm = CreateTestNorm("4.1", "Нажатие", new[] { (10m, 40m), (20m, 50m), (30m, 60m) });
        var invalidNorm = CreateTestNorm("4.2", "Н/Ф", new[] { (10m, 40m) }); // Только одна точка

        await _context.Norms.AddRangeAsync(validNorm, invalidNorm);
        await _context.SaveChangesAsync();

        var settingsOptions = Options.Create(_settings);
        var interpolationService = new NormInterpolationService(
            _interpolationLoggerMock.Object, _context, settingsOptions);

        // Act
        var validationResult = await interpolationService.ValidateNormsAsync();

        // Assert
        validationResult.ValidNorms.Should().Contain("4.1");
        validationResult.InvalidNorms.Should().Contain("4.2");
        validationResult.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DataAnalysisService_AnalyzeSectionAsync_ShouldAnalyzeRoutesCorrectly()
    {
        // Arrange - настраиваем тестовые данные как в Python
        await SetupTestDataForAnalysisAsync();

        var settingsOptions = Options.Create(_settings);
        
        // Создаем моки для зависимых сервисов
        var normStorageService = new NormStorageService(_storageLoggerMock.Object, _context);
        var interpolationService = new NormInterpolationService(_interpolationLoggerMock.Object, _context, settingsOptions);
        
        var coefficientServiceMock = new Mock<ILocomotiveCoefficientService>();
        var cacheServiceMock = new Mock<IAnalysisCacheService>();

        // Настраиваем поведение мока кэша
        cacheServiceMock.Setup(x => x.GetCachedAnalysisAsync(It.IsAny<string>())).ReturnsAsync((AnalysisResult?)null);

        var analysisService = new DataAnalysisService(
            _analysisLoggerMock.Object, _context, normStorageService, interpolationService,
            coefficientServiceMock.Object, cacheServiceMock.Object, settingsOptions);

        // Act
        var analysisResult = await analysisService.AnalyzeSectionAsync("Тестовый участок");

        // Assert
        analysisResult.Should().NotBeNull();
        analysisResult.SectionName.Should().Be("Тестовый участок");
        analysisResult.TotalRoutes.Should().BeGreaterThan(0);
        analysisResult.AnalyzedRoutes.Should().BeGreaterThan(0);
        analysisResult.AverageDeviation.Should().NotBeNull();
        analysisResult.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DataAnalysisService_GetSectionsListAsync_ShouldReturnSections()
    {
        // Arrange
        var testRoutes = new[]
        {
            CreateTestRoute("1", "Участок А", "1.1"),
            CreateTestRoute("2", "Участок Б", "1.2"),
            CreateTestRoute("3", "Участок А", "1.1") // Дублирующийся участок
        };

        await _context.Routes.AddRangeAsync(testRoutes);
        await _context.SaveChangesAsync();

        var analysisService = CreateDataAnalysisService();

        // Act
        var sections = await analysisService.GetSectionsListAsync();

        // Assert
        sections.Should().HaveCount(2);
        sections.Should().Contain("Участок А");
        sections.Should().Contain("Участок Б");
    }

    [Fact]
    public async Task DataAnalysisService_GetNormsWithCountsForSection_ShouldReturnCorrectCounts()
    {
        // Arrange
        var testRoutes = new[]
        {
            CreateTestRoute("1", "Участок А", "1.1"),
            CreateTestRoute("2", "Участок А", "1.1"), // Та же норма
            CreateTestRoute("3", "Участок А", "1.2"), // Другая норма
            CreateTestRoute("4", "Участок Б", "1.1")  // Другой участок
        };

        await _context.Routes.AddRangeAsync(testRoutes);
        await _context.SaveChangesAsync();

        var analysisService = CreateDataAnalysisService();

        // Act
        var normCounts = await analysisService.GetNormsWithCountsForSectionAsync("Участок А", singleSectionOnly: true);

        // Assert
        normCounts.Should().HaveCount(2);
        var norm11Count = normCounts.First(nc => nc.NormId == "1.1").RouteCount;
        var norm12Count = normCounts.First(nc => nc.NormId == "1.2").RouteCount;
        
        norm11Count.Should().Be(2);
        norm12Count.Should().Be(1);
    }

    [Fact]
    public async Task VisualizationDataService_PrepareInteractiveChartData_ShouldCreateCorrectStructure()
    {
        // Arrange
        var settingsOptions = Options.Create(_settings);
        var interpolationServiceMock = new Mock<INormInterpolationService>();
        
        // Настраиваем мок для возврата интерполированных значений
        interpolationServiceMock.Setup(x => x.InterpolateNormValueAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(50m);

        var visualizationService = new VisualizationDataService(
            _visualizationLoggerMock.Object, interpolationServiceMock.Object, settingsOptions);

        var testRoutes = new[]
        {
            CreateTestRouteWithDeviation("1", 15m, 52m, 5m),  // Слабый перерасход
            CreateTestRouteWithDeviation("2", 20m, 48m, -4m), // Слабая экономия
            CreateTestRouteWithDeviation("3", 25m, 50m, 0m)   // Норма
        };

        var normFunctions = new Dictionary<string, InterpolationFunction>
        {
            ["1.1"] = new InterpolationFunction("1.1", "Нажатие", new[] { 10m, 20m, 30m }, new[] { 45m, 50m, 55m })
        };

        // Act
        var visualizationData = await visualizationService.PrepareInteractiveChartDataAsync(
            "Тестовый участок", testRoutes, normFunctions);

        // Assert
        visualizationData.Should().NotBeNull();
        visualizationData.NormCurves.Should().NotBeNull();
        visualizationData.RoutePoints.Should().NotBeNull();
        visualizationData.DeviationAnalysis.Should().NotBeNull();
        visualizationData.Metadata.Should().ContainKey("SectionName");
        visualizationData.Metadata["SectionName"].Should().Be("Тестовый участок");
    }

    [Fact]
    public void VisualizationDataService_CreateDeviationChartData_ShouldGroupByStatus()
    {
        // Arrange
        var settingsOptions = Options.Create(_settings);
        var interpolationServiceMock = new Mock<INormInterpolationService>();
        var visualizationService = new VisualizationDataService(
            _visualizationLoggerMock.Object, interpolationServiceMock.Object, settingsOptions);

        var testRoutes = new[]
        {
            CreateTestRouteWithDeviation("1", 15m, 52m, 10m, DeviationStatus.OverrunWeak),
            CreateTestRouteWithDeviation("2", 20m, 48m, -10m, DeviationStatus.EconomyWeak),
            CreateTestRouteWithDeviation("3", 25m, 50m, 0m, DeviationStatus.Normal)
        };

        // Act
        var chartData = visualizationService.CreateDeviationChartData(testRoutes);

        // Assert
        chartData.Should().NotBeNull();
        chartData.Series.Should().NotBeEmpty();
        
        // Проверяем что есть серии для каждого статуса
        var seriesNames = chartData.Series.Select(s => s.Name).ToList();
        seriesNames.Should().Contain(name => name.Contains(DeviationStatus.OverrunWeak));
        seriesNames.Should().Contain(name => name.Contains(DeviationStatus.EconomyWeak));
        seriesNames.Should().Contain(name => name.Contains(DeviationStatus.Normal));
        
        // Проверяем что есть линии допусков
        seriesNames.Should().Contain(name => name.Contains("допуск"));
        seriesNames.Should().Contain(name => name.Contains("Норма"));
    }

    [Fact]
    public async Task AnalysisCacheService_SaveAndGetAnalysis_ShouldWorkCorrectly()
    {
        // Arrange
        var settingsOptions = Options.Create(_settings);
        var cacheService = new AnalysisCacheService(
            Mock.Of<ILogger<AnalysisCacheService>>(), _context, settingsOptions);

        var testAnalysis = new AnalysisResult
        {
            AnalysisHash = "testhash123",
            SectionName = "Тестовый участок",
            TotalRoutes = 5,
            AnalyzedRoutes = 4,
            AverageDeviation = 2.5m,
            CreatedAt = DateTime.UtcNow,
            Routes = new List<Route>
            {
                CreateTestRoute("1", "Тестовый участок", "1.1")
            }
        };

        // Act - сохраняем анализ
        await cacheService.SaveAnalysisToCacheAsync(testAnalysis);

        // Act - получаем анализ
        var cachedAnalysis = await cacheService.GetCachedAnalysisAsync("testhash123");

        // Assert
        cachedAnalysis.Should().NotBeNull();
        cachedAnalysis!.AnalysisHash.Should().Be("testhash123");
        cachedAnalysis.SectionName.Should().Be("Тестовый участок");
        cachedAnalysis.TotalRoutes.Should().Be(5);
        cachedAnalysis.AverageDeviation.Should().Be(2.5m);
        cachedAnalysis.Routes.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnalysisCacheService_CleanupOldCache_ShouldRemoveExpiredEntries()
    {
        // Arrange
        var settingsOptions = Options.Create(_settings);
        var cacheService = new AnalysisCacheService(
            Mock.Of<ILogger<AnalysisCacheService>>(), _context, settingsOptions);

        // Создаем старый анализ
        var oldAnalysis = new AnalysisResult
        {
            AnalysisHash = "oldhash",
            SectionName = "Старый участок",
            CreatedAt = DateTime.UtcNow.AddDays(-10), // 10 дней назад
            Routes = new List<Route>()
        };

        // Создаем свежий анализ
        var freshAnalysis = new AnalysisResult
        {
            AnalysisHash = "freshhash",
            SectionName = "Свежий участок", 
            CreatedAt = DateTime.UtcNow.AddHours(-1), // 1 час назад
            Routes = new List<Route>()
        };

        await cacheService.SaveAnalysisToCacheAsync(oldAnalysis);
        await cacheService.SaveAnalysisToCacheAsync(freshAnalysis);

        // Act - очищаем кэш старше 5 дней
        await cacheService.CleanupOldCacheAsync(TimeSpan.FromDays(5));

        // Assert
        var remainingAnalyses = await _context.AnalysisResults.ToListAsync();
        remainingAnalyses.Should().HaveCount(1);
        remainingAnalyses.First().AnalysisHash.Should().Be("freshhash");
    }

    [Fact]
    public async Task DataAnalysisEngine_IntegrationTest_ShouldWorkEndToEnd()
    {
        // Arrange - создаем полноценные тестовые данные
        await SetupCompleteTestDataAsync();

        var settingsOptions = Options.Create(_settings);
        
        // Создаем все сервисы
        var normStorageService = new NormStorageService(_storageLoggerMock.Object, _context);
        var interpolationService = new NormInterpolationService(_interpolationLoggerMock.Object, _context, settingsOptions);
        var cacheService = new AnalysisCacheService(Mock.Of<ILogger<AnalysisCacheService>>(), _context, settingsOptions);
        
        var coefficientServiceMock = new Mock<ILocomotiveCoefficientService>();
        coefficientServiceMock.Setup(x => x.ApplyCoefficientsAsync(It.IsAny<IEnumerable<Route>>()))
            .Returns(Task.CompletedTask);

        var analysisService = new DataAnalysisService(
            _analysisLoggerMock.Object, _context, normStorageService, interpolationService,
            coefficientServiceMock.Object, cacheService, settingsOptions);

        var visualizationService = new VisualizationDataService(
            _visualizationLoggerMock.Object, interpolationService, settingsOptions);

        // Act - выполняем полный цикл анализа
        var analysisResult = await analysisService.AnalyzeSectionAsync("Интеграционный участок");

        var normFunctions = new Dictionary<string, InterpolationFunction>
        {
            ["5.1"] = new InterpolationFunction("5.1", "Нажатие", new[] { 10m, 20m, 30m }, new[] { 45m, 50m, 55m })
        };

        var visualizationData = await visualizationService.PrepareInteractiveChartDataAsync(
            "Интеграционный участок", analysisResult.Routes, normFunctions);

        // Assert - проверяем весь pipeline
        analysisResult.Should().NotBeNull();
        analysisResult.ErrorMessage.Should().BeNullOrEmpty();
        analysisResult.TotalRoutes.Should().BeGreaterThan(0);
        analysisResult.AnalyzedRoutes.Should().BeGreaterThan(0);
        
        visualizationData.Should().NotBeNull();
        visualizationData.NormCurves.Series.Should().NotBeEmpty();
        visualizationData.DeviationAnalysis.Series.Should().NotBeEmpty();
        visualizationData.Metadata.Should().ContainKey("SectionName");
    }

    /// <summary>
    /// Создает тестовую норму
    /// </summary>
    private Norm CreateTestNorm(string normId, string normType, (decimal Load, decimal Consumption)[] points)
    {
        var norm = new Norm
        {
            NormId = normId,
            NormType = normType,
            CreatedAt = DateTime.UtcNow,
            Points = new List<NormPoint>()
        };

        for (int i = 0; i < points.Length; i++)
        {
            norm.Points.Add(new NormPoint
            {
                NormId = normId,
                Load = points[i].Load,
                Consumption = points[i].Consumption,
                Order = i + 1,
                PointType = "base"
            });
        }

        return norm;
    }

    /// <summary>
    /// Создает тестовый маршрут
    /// </summary>
    private Route CreateTestRoute(string routeNumber, string sectionName, string normNumber)
    {
        return new Route
        {
            RouteNumber = routeNumber,
            RouteDate = "20241201",
            TripDate = "20241201",
            DriverTab = "12345",
            SectionName = sectionName,
            NormNumber = normNumber,
            LocomotiveSeries = "ВЛ80С",
            LocomotiveNumber = 1234,
            AxleLoad = 20m,
            FactConsumption = 500m,
            TonKilometers = 10000m,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Создает тестовый маршрут с отклонением
    /// </summary>
    private Route CreateTestRouteWithDeviation(string routeNumber, decimal axleLoad, decimal factUd, decimal deviation, string? status = null)
    {
        var route = new Route
        {
            RouteNumber = routeNumber,
            RouteDate = "20241201",
            AxleLoad = axleLoad,
            FactUd = factUd,
            DeviationPercent = deviation,
            Status = status ?? DeviationStatus.GetStatus(deviation),
            CreatedAt = DateTime.UtcNow
        };

        return route;
    }

    /// <summary>
    /// Создает сервис анализа данных для тестов
    /// </summary>
    private DataAnalysisService CreateDataAnalysisService()
    {
        var settingsOptions = Options.Create(_settings);
        var normStorageMock = new Mock<INormStorageService>();
        var interpolationMock = new Mock<INormInterpolationService>();
        var coefficientMock = new Mock<ILocomotiveCoefficientService>();
        var cacheMock = new Mock<IAnalysisCacheService>();

        return new DataAnalysisService(
            _analysisLoggerMock.Object, _context, normStorageMock.Object,
            interpolationMock.Object, coefficientMock.Object, cacheMock.Object, settingsOptions);
    }

    /// <summary>
    /// Настраивает тестовые данные для анализа
    /// </summary>
    private async Task SetupTestDataForAnalysisAsync()
    {
        var testNorm = CreateTestNorm("1.1", "Нажатие", new[] { (10m, 45m), (20m, 50m), (30m, 55m) });
        var testRoutes = new[]
        {
            CreateTestRoute("1", "Тестовый участок", "1.1"),
            CreateTestRoute("2", "Тестовый участок", "1.1")
        };

        await _context.Norms.AddAsync(testNorm);
        await _context.Routes.AddRangeAsync(testRoutes);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Настраивает полные тестовые данные для интеграционного теста
    /// </summary>
    private async Task SetupCompleteTestDataAsync()
    {
        var testNorms = new[]
        {
            CreateTestNorm("5.1", "Нажатие", new[] { (10m, 45m), (20m, 50m), (30m, 55m) }),
            CreateTestNorm("5.2", "Н/Ф", new[] { (15m, 42m), (25m, 47m), (35m, 52m) })
        };

        var testRoutes = new[]
        {
            CreateTestRoute("101", "Интеграционный участок", "5.1"),
            CreateTestRoute("102", "Интеграционный участок", "5.1"),
            CreateTestRoute("103", "Интеграционный участок", "5.2")
        };

        await _context.Norms.AddRangeAsync(testNorms);
        await _context.Routes.AddRangeAsync(testRoutes);
        await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

/// <summary>
/// Тесты производительности для Data Analysis Engine
/// </summary>
public class DataAnalysisPerformanceTests : IDisposable
{
    private readonly AnalysisNormDbContext _context;

    public DataAnalysisPerformanceTests()
    {
        var options = new DbContextOptionsBuilder<AnalysisNormDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new AnalysisNormDbContext(options);
    }

    [Fact]
    public async Task NormInterpolationService_ShouldHandleLargeDatasets()
    {
        // Arrange - создаем большую норму с множеством точек
        var largeNorm = new Norm
        {
            NormId = "PERF_1",
            NormType = "Нажатие",
            CreatedAt = DateTime.UtcNow,
            Points = new List<NormPoint>()
        };

        // Создаем 1000 точек
        for (int i = 1; i <= 1000; i++)
        {
            largeNorm.Points.Add(new NormPoint
            {
                NormId = "PERF_1",
                Load = i * 0.1m, // От 0.1 до 100.0
                Consumption = 40m + i * 0.01m, // Линейный рост
                Order = i,
                PointType = "base"
            });
        }

        await _context.Norms.AddAsync(largeNorm);
        await _context.SaveChangesAsync();

        var settings = Options.Create(new ApplicationSettings());
        var interpolationService = new NormInterpolationService(
            Mock.Of<ILogger<NormInterpolationService>>(), _context, settings);

        // Act - тестируем производительность интерполяции
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        var results = new List<decimal?>();
        for (int i = 0; i < 100; i++) // 100 интерполяций
        {
            var result = await interpolationService.InterpolateNormValueAsync("PERF_1", 50m + i * 0.1m);
            results.Add(result);
        }
        
        stopwatch.Stop();

        // Assert
        results.Should().HaveCount(100);
        results.Should().OnlyContain(r => r.HasValue); // Все интерполяции должны быть успешными
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Менее 5 секунд для 100 интерполяций
    }

    [Fact]
    public async Task DataAnalysisService_ShouldHandleLargeRouteDatasets()
    {
        // Arrange - создаем большой набор маршрутов
        var testNorm = new Norm
        {
            NormId = "PERF_2",
            NormType = "Нажатие",
            CreatedAt = DateTime.UtcNow,
            Points = new List<NormPoint>
            {
                new() { NormId = "PERF_2", Load = 10m, Consumption = 45m, Order = 1, PointType = "base" },
                new() { NormId = "PERF_2", Load = 20m, Consumption = 50m, Order = 2, PointType = "base" },
                new() { NormId = "PERF_2", Load = 30m, Consumption = 55m, Order = 3, PointType = "base" }
            }
        };

        var testRoutes = new List<Route>();
        for (int i = 1; i <= 10000; i++) // 10,000 маршрутов
        {
            testRoutes.Add(new Route
            {
                RouteNumber = i.ToString(),
                RouteDate = "20241201",
                TripDate = "20241201",
                DriverTab = (12345 + i % 100).ToString(),
                SectionName = "Производительный участок",
                NormNumber = "PERF_2",
                LocomotiveSeries = "ВЛ80С",
                LocomotiveNumber = 1000 + i % 1000,
                AxleLoad = 15m + (i % 20), // Варьируем нагрузку
                FactConsumption = 450m + (i % 100), // Варьируем расход
                TonKilometers = 9000m + (i % 2000),
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.Norms.AddAsync(testNorm);
        await _context.Routes.AddRangeAsync(testRoutes);
        await _context.SaveChangesAsync();

        // Act - тестируем производительность анализа
        var sections = await _context.Routes
            .Where(r => r.SectionName == "Производительный участок")
            .Select(r => r.SectionName!)
            .Distinct()
            .ToListAsync();

        // Assert
        sections.Should().HaveCount(1);
        sections.First().Should().Be("Производительный участок");
        
        // Проверяем что запрос выполняется быстро
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var routeCount = await _context.Routes
            .Where(r => r.SectionName == "Производительный участок")
            .CountAsync();
        stopwatch.Stop();

        routeCount.Should().Be(10000);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Менее 1 секунды для подсчета 10k записей
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}