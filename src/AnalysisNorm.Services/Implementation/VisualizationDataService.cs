using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
// ИСПРАВЛЕНО: Используем SkiaSharp вместо Wpf для Services слоя
using OxyPlot.SkiaSharp;
using AnalysisNorm.Core.Entities;
using AnalysisNorm.Services.Interfaces;


/// <summary>
/// ЕДИНСТВЕННАЯ реализация сервиса подготовки данных для визуализации в OxyPlot
/// Соответствует PlotBuilder из Python analysis/visualization.py + экспорт изображений
/// ИСПРАВЛЕНО: Устранены все дубликаты, конфликты пространств имен и WPF зависимости
/// </summary>
