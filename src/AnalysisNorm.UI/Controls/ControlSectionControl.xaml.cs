using System.Windows;
using System.Windows.Controls;

namespace AnalysisNorm.UI.Controls;

/// <summary>
/// UserControl для управления анализом - аналог Python ControlSection
/// Обеспечивает выбор участков, норм и настройки анализа
/// </summary>
public partial class ControlSectionControl : UserControl
{
    public ControlSectionControl()
    {
        InitializeComponent();
    }

    #region Обработчики событий UI - минимальные, основная логика в ViewModel

    /// <summary>
    /// Обработчик изменения выбранной нормы
    /// Включает/выключает кнопку "Инфо" - аналог Python _on_norm_change
    /// </summary>
    private void NormComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedNorm = NormComboBox.SelectedItem as string;
        
        // Включаем кнопку "Инфо" только для конкретных норм (не "Все нормы")
        NormInfoButton.IsEnabled = !string.IsNullOrEmpty(selectedNorm) && 
                                   selectedNorm != "Все нормы" &&
                                   selectedNorm.StartsWith("Норма ");
    }

    #endregion
}
}