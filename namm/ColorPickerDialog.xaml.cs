using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace namm
{
    public partial class ColorPickerDialog : Window
    {
        public Color SelectedColor { get; private set; }

        public ColorPickerDialog(Color initialColor)
        {
            InitializeComponent();
            SelectedColor = initialColor;
            sliderRed.Value = initialColor.R;
            sliderGreen.Value = initialColor.G;
            sliderBlue.Value = initialColor.B;
            UpdateColorPreview();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateColorPreview();
        }

        private void UpdateColorPreview()
        {
            SelectedColor = Color.FromRgb((byte)sliderRed.Value, (byte)sliderGreen.Value, (byte)sliderBlue.Value);
            colorPreview.Fill = new SolidColorBrush(SelectedColor);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Focus on the first slider for better usability
            sliderRed.Focus();
        }
    }
}