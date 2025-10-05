using System;
using System.IO;

namespace AnalysisNorm.Core
{
    /// <summary>
    /// Конфигурация приложения
    /// Мигрировано из: core/config.py (AppConfig, line 10-60)
    /// </summary>
    public class AppConfig
    {
        #region Свойства путей

        /// <summary>Корневая директория приложения</summary>
        public string AppRoot { get; set; }

        /// <summary>Директория для логов</summary>
        public string LogsDir { get; set; }

        /// <summary>Директория для временных файлов</summary>
        public string TempDir { get; set; }

        /// <summary>Директория для экспортов</summary>
        public string ExportsDir { get; set; }

        /// <summary>Директория статических ресурсов</summary>
        public string StaticDir { get; set; }

        #endregion

        #region Константы

        /// <summary>
        /// Поддерживаемые кодировки для чтения HTML
        /// Python: supported_encodings = ('cp1251', 'utf-8', 'utf-8-sig')
        /// </summary>
        public static readonly string[] SupportedEncodings = { "windows-1251", "utf-8", "utf-8" };

        /// <summary>
        /// Максимальное количество временных файлов
        /// Python: max_temp_files = 10
        /// </summary>
        public const int MaxTempFiles = 10;

        /// <summary>
        /// Допустимое отклонение по умолчанию (%)
        /// Python: default_tolerance_percent = 5.0
        /// </summary>
        public const double DefaultTolerancePercent = 5.0;

        /// <summary>
        /// Минимальный порог работы для фильтрации локомотивов
        /// Python: min_work_threshold = 200.0
        /// </summary>
        public const double MinWorkThreshold = 200.0;

        /// <summary>
        /// Размер окна по умолчанию (ширина x высота)
        /// Python: default_window_size = (1400, 900)
        /// </summary>
        public static readonly (int Width, int Height) DefaultWindowSize = (1400, 900);

        #endregion

        #region Методы

        /// <summary>
        /// Создает конфигурацию по умолчанию
        /// Python: create_default(), line 42
        /// </summary>
        public static AppConfig CreateDefault()
        {
            // Python: app_root = Path(__file__).parent.parent
            string appRoot = AppDomain.CurrentDomain.BaseDirectory;

            var config = new AppConfig
            {
                AppRoot = appRoot,
                StaticDir = Path.Combine(appRoot, "static"),
                LogsDir = Path.Combine(appRoot, "logs"),
                TempDir = Path.Combine(appRoot, "temp"),
                ExportsDir = Path.Combine(appRoot, "exports")
            };

            return config;
        }

        /// <summary>
        /// Создает необходимые директории
        /// Python: ensure_directories(), line 51
        /// </summary>
        public void EnsureDirectories()
        {
            // Python: for directory in [self.static_dir, self.logs_dir, self.temp_dir, self.exports_dir]:
            //             directory.mkdir(exist_ok=True)
            
            foreach (var dir in new[] { StaticDir, LogsDir, TempDir, ExportsDir })
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
        }

        /// <summary>
        /// Версия приложения
        /// Python: VERSION = "1.0.0"
        /// </summary>
        public static string Version => "1.0.0-alpha";

        /// <summary>
        /// Имя приложения
        /// </summary>
        public static string ApplicationName => "Analysis Norm C#";

        /// <summary>
        /// Полное описание приложения
        /// </summary>
        public static string ApplicationFullName => 
            $"{ApplicationName} v{Version} (.NET 9.0)";

        #endregion
    }
}