using Microsoft.EntityFrameworkCore;
using AnalysisNorm.Core.Entities;

namespace AnalysisNorm.Data;

/// <summary>
/// Основной контекст базы данных для системы анализа норм
/// Использует SQLite для кэширования и хранения исторических данных
/// Соответствует архитектуре Python приложения с добавлением кэширования
/// </summary>
public class AnalysisNormDbContext : DbContext
{
    public AnalysisNormDbContext(DbContextOptions<AnalysisNormDbContext> options) : base(options)
    {
    }

    // === ОСНОВНЫЕ СУЩНОСТИ ===
    public DbSet<Route> Routes { get; set; } = null!;
    public DbSet<Norm> Norms { get; set; } = null!;
    public DbSet<NormPoint> NormPoints { get; set; } = null!;
    public DbSet<LocomotiveCoefficient> LocomotiveCoefficients { get; set; } = null!;
    public DbSet<AnalysisResult> AnalysisResults { get; set; } = null!;
    public DbSet<NormInterpolationCache> NormInterpolationCache { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === КОНФИГУРАЦИЯ ROUTE ===
        modelBuilder.Entity<Route>(entity =>
        {
            // Индексы для быстрого поиска (аналог группировки в Python pandas)
            entity.HasIndex(e => e.RouteNumber).HasDatabaseName("IX_Routes_RouteNumber");
            entity.HasIndex(e => e.RouteDate).HasDatabaseName("IX_Routes_RouteDate");
            entity.HasIndex(e => e.SectionName).HasDatabaseName("IX_Routes_SectionName");
            entity.HasIndex(e => e.NormNumber).HasDatabaseName("IX_Routes_NormNumber");
            entity.HasIndex(e => e.RouteKey).IsUnique().HasDatabaseName("IX_Routes_RouteKey");
            
            // Составной индекс для группировки дубликатов (как в Python)
            entity.HasIndex(e => new { e.RouteNumber, e.TripDate, e.DriverTab })
                .HasDatabaseName("IX_Routes_DuplicateDetection");

            // Обновление временных меток
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NULL");
        });

        // === КОНФИГУРАЦИЯ NORM ===
        modelBuilder.Entity<Norm>(entity =>
        {
            entity.HasIndex(e => e.NormId).IsUnique().HasDatabaseName("IX_Norms_NormId");
            entity.HasIndex(e => e.NormType).HasDatabaseName("IX_Norms_NormType");
            
            // Каскадное удаление точек при удалении нормы
            entity.HasMany(e => e.Points)
                .WithOne(e => e.Norm)
                .HasForeignKey(e => e.NormId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // === КОНФИГУРАЦИЯ NORM_POINT ===
        modelBuilder.Entity<NormPoint>(entity =>
        {
            // Индекс для быстрой сортировки точек (для интерполяции)
            entity.HasIndex(e => new { e.NormId, e.Order }).HasDatabaseName("IX_NormPoints_NormId_Order");
            entity.HasIndex(e => new { e.NormId, e.Load }).HasDatabaseName("IX_NormPoints_NormId_Load");
            
            // Проверка корректности значений (как в Python validation)
            entity.HasCheckConstraint("CK_NormPoints_Load_Positive", "Load > 0");
            entity.HasCheckConstraint("CK_NormPoints_Consumption_Positive", "Consumption > 0");
        });

        // === КОНФИГУРАЦИЯ LOCOMOTIVE_COEFFICIENT ===
        modelBuilder.Entity<LocomotiveCoefficient>(entity =>
        {
            // Уникальный индекс для комбинации серия+номер (как Dict key в Python)
            entity.HasIndex(e => new { e.SeriesNormalized, e.Number })
                .IsUnique()
                .HasDatabaseName("IX_LocomotiveCoefficients_Series_Number");
            
            entity.HasIndex(e => e.SeriesNormalized).HasDatabaseName("IX_LocomotiveCoefficients_SeriesNormalized");
            
            // Автоматическое обновление рассчитанных полей
            entity.Property(e => e.SeriesNormalized).HasComputedColumnSql(null);
            entity.Property(e => e.DeviationPercent).HasComputedColumnSql(null);
        });

        // === КОНФИГУРАЦИЯ ANALYSIS_RESULT ===
        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            // Уникальный индекс для предотвращения дублирования анализов
            entity.HasIndex(e => e.AnalysisHash).IsUnique().HasDatabaseName("IX_AnalysisResults_AnalysisHash");
            entity.HasIndex(e => e.SectionName).HasDatabaseName("IX_AnalysisResults_SectionName");
            entity.HasIndex(e => e.CreatedAt).HasDatabaseName("IX_AnalysisResults_CreatedAt");

            // Связь с нормой (optional)
            entity.HasOne(e => e.Norm)
                .WithMany(e => e.AnalysisResults)
                .HasForeignKey(e => e.NormId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // === КОНФИГУРАЦИЯ NORM_INTERPOLATION_CACHE ===
        modelBuilder.Entity<NormInterpolationCache>(entity =>
        {
            // Уникальный индекс для кэша интерполяции
            entity.HasIndex(e => new { e.NormId, e.ParameterValue })
                .IsUnique()
                .HasDatabaseName("IX_NormInterpolationCache_NormId_Parameter");
            
            entity.HasIndex(e => e.LastUsed).HasDatabaseName("IX_NormInterpolationCache_LastUsed");
            
            // Каскадное удаление при удалении нормы
            entity.HasOne(e => e.Norm)
                .WithMany()
                .HasForeignKey(e => e.NormId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // === ЗНАЧЕНИЯ ПО УМОЛЧАНИЮ ===
        modelBuilder.Entity<Route>()
            .Property(e => e.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<Norm>()
            .Property(e => e.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<LocomotiveCoefficient>()
            .Property(e => e.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<AnalysisResult>()
            .Property(e => e.CreatedAt)
            .HasDefaultValueSql("datetime('now')");

        modelBuilder.Entity<NormInterpolationCache>()
            .Property(e => e.LastUsed)
            .HasDefaultValueSql("datetime('now')");
    }

    /// <summary>
    /// Очищает устаревшие записи кэша интерполяции
    /// Аналог управления памятью в Python версии
    /// </summary>
    public async Task CleanupInterpolationCacheAsync(TimeSpan maxAge)
    {
        var cutoffDate = DateTime.UtcNow - maxAge;
        await NormInterpolationCache
            .Where(c => c.LastUsed < cutoffDate)
            .ExecuteDeleteAsync();
    }

    /// <summary>
    /// Получает статистику использования базы данных
    /// </summary>
    public async Task<DatabaseStatistics> GetDatabaseStatisticsAsync()
    {
        return new DatabaseStatistics
        {
            RoutesCount = await Routes.CountAsync(),
            NormsCount = await Norms.CountAsync(),
            NormPointsCount = await NormPoints.CountAsync(),
            CoefficientsCount = await LocomotiveCoefficients.CountAsync(),
            AnalysisResultsCount = await AnalysisResults.CountAsync(),
            CacheEntriesCount = await NormInterpolationCache.CountAsync()
        };
    }
}

/// <summary>
/// Статистика базы данных
/// </summary>
public record DatabaseStatistics
{
    public int RoutesCount { get; init; }
    public int NormsCount { get; init; }
    public int NormPointsCount { get; init; }
    public int CoefficientsCount { get; init; }
    public int AnalysisResultsCount { get; init; }
    public int CacheEntriesCount { get; init; }
}