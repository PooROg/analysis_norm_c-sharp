using FluentAssertions;
using AnalysisNorm.Core.Entities;
using Xunit;

namespace AnalysisNorm.Tests.Core.Entities;

/// <summary>
/// Тесты для проверки корректности entity models
/// Проверяем соответствие Python структурам данных
/// </summary>
public class RouteEntityTests
{
    [Fact]
    public void Route_GenerateRouteKey_ShouldCreateCorrectKey()
    {
        // Arrange - тестируем аналог extract_route_key из Python utils.py
        var route = new Route
        {
            RouteNumber = "12345",
            TripDate = "20241201", 
            DriverTab = "67890"
        };

        // Act
        route.GenerateRouteKey();

        // Assert
        route.RouteKey.Should().Be("12345_20241201_67890");
    }

    [Fact]
    public void Route_GenerateRouteKey_WithMissingData_ShouldNotSetKey()
    {
        // Arrange
        var route = new Route
        {
            RouteNumber = "12345",
            TripDate = null, // отсутствует дата поездки
            DriverTab = "67890"
        };

        // Act
        route.GenerateRouteKey();

        // Assert
        route.RouteKey.Should().BeNull();
    }

    [Theory]
    [InlineData("ВЛ80С", "ВЛ80С")]  // уже нормализовано
    [InlineData("вл-80с", "ВЛ80С")]  // приведение к верхнему регистру
    [InlineData("ВЛ-80-С", "ВЛ80С")]  // удаление дефисов
    [InlineData("ВЛ 80 С", "ВЛ80С")]  // удаление пробелов
    [InlineData("2ЭС5К", "2ЭС5К")]   // цифры в начале
    public void LocomotiveCoefficient_NormalizeSeries_ShouldMatchPythonBehavior(
        string input, string expected)
    {
        // Act - тестируем аналог normalize_series из Python coefficients.py
        var result = LocomotiveCoefficient.NormalizeSeries(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void LocomotiveCoefficient_UpdateCalculatedFields_ShouldCalculateDeviationCorrectly()
    {
        // Arrange
        var coefficient = new LocomotiveCoefficient
        {
            Series = "вл-80с",
            Coefficient = 1.15m  // 15% больше нормы
        };

        // Act
        coefficient.UpdateCalculatedFields();

        // Assert
        coefficient.SeriesNormalized.Should().Be("ВЛ80С");
        coefficient.DeviationPercent.Should().Be(15.0m);
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
    public void DeviationStatus_GetStatus_ShouldMatchPythonStatusClassifier(
        decimal deviation, string expectedStatus)
    {
        // Act - тестируем аналог get_status из Python StatusClassifier
        var result = DeviationStatus.GetStatus(deviation);

        // Assert
        result.Should().Be(expectedStatus);
    }

    [Fact]
    public void NormPoint_ShouldHaveCorrectConstraints()
    {
        // Arrange
        var normPoint = new NormPoint
        {
            NormId = 1,
            Load = 22.5m,       // нагрузка на ось в т/ось
            Consumption = 45.2m, // расход в кВт·ч/10⁴ ткм
            PointType = "base",
            Order = 1
        };

        // Assert - проверяем что точка содержит правильные данные
        normPoint.Load.Should().BePositive();
        normPoint.Consumption.Should().BePositive();
        normPoint.PointType.Should().BeOneOf("base", "additional");
    }

    [Fact]  
    public void AnalysisResult_GenerateAnalysisHash_ShouldCreateUniqueHash()
    {
        // Arrange
        var analysisResult = new AnalysisResult
        {
            SectionName = "Москва - Санкт-Петербург",
            NormId = "123",
            SingleSectionOnly = true,
            UseCoefficients = false
        };

        // Act
        analysisResult.GenerateAnalysisHash();

        // Assert
        analysisResult.AnalysisHash.Should().NotBeNullOrEmpty();
        analysisResult.AnalysisHash.Length.Should().Be(64); // SHA256 hex string
    }

    [Fact]
    public void AnalysisResult_GenerateAnalysisHash_SameParameters_ShouldGenerateSameHash()
    {
        // Arrange
        var result1 = new AnalysisResult
        {
            SectionName = "Тест участок",
            NormId = "456", 
            SingleSectionOnly = false,
            UseCoefficients = true
        };
        
        var result2 = new AnalysisResult
        {
            SectionName = "Тест участок",
            NormId = "456",
            SingleSectionOnly = false, 
            UseCoefficients = true
        };

        // Act
        result1.GenerateAnalysisHash();
        result2.GenerateAnalysisHash();

        // Assert
        result1.AnalysisHash.Should().Be(result2.AnalysisHash);
    }
}

/// <summary>
/// Интеграционные тесты для DbContext
/// Проверяем что entity models корректно работают с Entity Framework
/// </summary>
public class DbContextIntegrationTests : IDisposable
{
    private readonly AnalysisNormDbContext _context;

    public DbContextIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AnalysisNormDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new AnalysisNormDbContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task DbContext_CanSaveAndRetrieveRoute()
    {
        // Arrange
        var route = new Route
        {
            RouteNumber = "TEST001",
            RouteDate = new DateTime(2024, 12, 1),
            SectionName = "Тестовый участок",
            NormNumber = "123",
            RashodFact = 100.5m,
            RashodNorm = 95.0m
        };

        // Act
        _context.Routes.Add(route);
        await _context.SaveChangesAsync();

        var savedRoute = await _context.Routes
            .FirstOrDefaultAsync(r => r.RouteNumber == "TEST001");

        // Assert
        savedRoute.Should().NotBeNull();
        savedRoute!.SectionName.Should().Be("Тестовый участок");
        savedRoute.RashodFact.Should().Be(100.5m);
    }

    [Fact] 
    public async Task DbContext_CanSaveNormWithPoints()
    {
        // Arrange
        var norm = new Norm
        {
            NormId = "TEST123",
            NormType = "Нажатие",
            Description = "Тестовая норма",
            Points = new List<NormPoint>
            {
                new() { Load = 20.0m, Consumption = 40.0m, PointType = "base", Order = 1 },
                new() { Load = 25.0m, Consumption = 45.0m, PointType = "base", Order = 2 }
            }
        };

        // Act
        _context.Norms.Add(norm);
        await _context.SaveChangesAsync();

        var savedNorm = await _context.Norms
            .Include(n => n.Points)
            .FirstOrDefaultAsync(n => n.NormId == "TEST123");

        // Assert
        savedNorm.Should().NotBeNull();
        savedNorm!.Points.Should().HaveCount(2);
        savedNorm.Points.First().Load.Should().Be(20.0m);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}