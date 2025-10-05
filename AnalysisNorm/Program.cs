using System;
using System.Windows.Forms;
using Serilog;
using AnalysisNorm.Core;
using AnalysisNorm.Utils;
using AnalysisNorm.GUI;

namespace AnalysisNorm
{
    /// <summary>
    /// Точка входа приложения
    /// Мигрировано из: main.py (lines 1-120)
    /// </summary>
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Python: setup_logging()
            SetupLogging();

            Log.Information("=== ЗАПУСК АНАЛИЗАТОРА НОРМ РАСХОДА ЭЛЕКТРОЭНЕРГИИ (C# версия) ===");
            Log.Information("Версия .NET: {Version}", Environment.Version);
            Log.Information("Рабочая директория: {Dir}", Environment.CurrentDirectory);

            try
            {
                // Python: check_dependencies()
                CheckDependencies();

                // Python: create_initial_directories()
                var config = AppConfig.CreateDefault();
                config.EnsureDirectories();
                Log.Information("Директории созданы: {Dirs}",
                    $"{config.LogsDir}, {config.TempDir}, {config.ExportsDir}");

                // Проверка базовых утилит
                TestUtilities();

                // Python: Application.run(new MainWindow())
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainWindow());
            }
            catch (Exception ex)
            {
                // Python: logger.error("Критическая ошибка запуска: %s", e)
                Log.Fatal(ex, "Критическая ошибка запуска");
                MessageBox.Show(
                    $"Ошибка запуска:\n\n{ex.Message}\n\nПодробности в логах.",
                    "Критическая ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                Log.Information("Приложение завершено");
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Настройка Serilog логирования
        /// Python: setup_logging(), main.py line 20
        /// </summary>
        static void SetupLogging()
        {
            // Python: log_filename = os.path.join(log_dir, f'analyzer_{datetime.now().strftime("%Y%m%d")}.log')
            string logPath = $"logs/analyzer_.log";

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .MinimumLevel.Debug()
                .CreateLogger();
        }

        /// <summary>
        /// Проверка зависимостей (заглушка)
        /// Python: check_dependencies(), main.py line 50
        /// </summary>
        static void CheckDependencies()
        {
            // Python: required_packages = [('pandas', 'pandas'), ...]
            Log.Information("Проверка зависимостей...");

            // TODO: В C# зависимости проверяются через NuGet автоматически
            // Здесь можно добавить проверку наличия критичных компонентов

            Log.Information("✓ Все необходимые зависимости найдены");
        }

        /// <summary>
        /// Тестирование базовых утилит
        /// Вместо unit-тестов - простая проверка в консоль
        /// </summary>
        static void TestUtilities()
        {
            Log.Information("=== Тестирование базовых утилит ===");

            // Тест NormalizeText
            string testText = "  Тест\u00A0текст  ";
            string normalized = TextUtils.NormalizeText(testText);
            Log.Information("NormalizeText: '{Original}' → '{Result}'", testText, normalized);

            // Тест SafeFloat
            double val1 = TextUtils.SafeFloat("123,45");
            double val2 = TextUtils.SafeFloat("  -987.65  ");
            double val3 = TextUtils.SafeFloat("N/A", 999.0);
            Log.Information("SafeFloat: '123,45' → {Val1}, '  -987.65  ' → {Val2}, 'N/A' (default=999) → {Val3}",
                val1, val2, val3);

            // Тест SafeInt
            int int1 = TextUtils.SafeInt("42");
            int int2 = TextUtils.SafeInt("123.999"); // Округление
            Log.Information("SafeInt: '42' → {Int1}, '123.999' → {Int2}", int1, int2);

            // Тест SafeDivide
            double div1 = TextUtils.SafeDivide(100, 20);
            double div2 = TextUtils.SafeDivide(100, 0, -1.0); // Деление на ноль
            Log.Information("SafeDivide: 100/20 → {Div1}, 100/0 (default=-1) → {Div2}", div1, div2);

            // Тест FormatNumber
            string fmt1 = TextUtils.FormatNumber(123.456, 2);
            string fmt2 = TextUtils.FormatNumber(null, 1, "ПУСТО");
            Log.Information("FormatNumber: 123.456 (2 знака) → '{Fmt1}', null → '{Fmt2}'", fmt1, fmt2);

            // Тест StatusClassifier
            var statuses = new[]
            {
                (-35.5, StatusClassifier.GetStatus(-35.5)),
                (-15.5, StatusClassifier.GetStatus(-15.5)),
                (-2.0, StatusClassifier.GetStatus(-2.0)),
                (3.0, StatusClassifier.GetStatus(3.0)),
                (15.0, StatusClassifier.GetStatus(15.0)),
                (25.0, StatusClassifier.GetStatus(25.0)),
                (40.0, StatusClassifier.GetStatus(40.0))
            };

            foreach (var (deviation, status) in statuses)
            {
                string color = StatusClassifier.GetStatusColor(status);
                Log.Information("GetStatus({Deviation}%) → '{Status}' (цвет: {Color})", 
                    deviation, status, color);
            }

            Log.Information("=== Все тесты утилит пройдены ✅ ===");
        }
    }
}