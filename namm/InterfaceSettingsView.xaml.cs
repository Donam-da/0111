using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace namm
{
    public partial class InterfaceSettingsView : UserControl
    {
        private Color selectedAppColor;
        private Color selectedLoginPanelColor;

        public InterfaceSettingsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCurrentSettings();
            PopulateColorPalette(appColorPalette, AppColor_Click);
            PopulateColorPalette(loginPanelColorPalette, LoginPanelColor_Click);
        }

        private void PopulateColorPalette(Panel palette, RoutedEventHandler colorClickHandler)
        {
            List<Color> colors = new List<Color>
            {
                Colors.LightCoral, Colors.Khaki, Colors.LightGreen, Colors.PaleTurquoise, 
                Colors.LightSteelBlue, Colors.Plum, Colors.LightGray, Colors.MistyRose
            };

            foreach (var color in colors)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(color),
                    Width = 20,
                    Height = 20,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(10),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                border.MouseLeftButtonDown += (s, e) => colorClickHandler(s, e);
                border.Tag = color;
                palette.Children.Add(border);
            }
        }

        private void AppColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Color color)
            {
                selectedAppColor = color;
                UpdateAppColor();
            }
        }

        private void LoginPanelColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is Color color)
            {
                selectedLoginPanelColor = color;
                UpdateLoginPanelColor();
            }
        }

        private void AppColorAdjustment_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
            {
                UpdateAppColor();
            }
        }

        private void LoginPanelColorAdjustment_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
            {
                UpdateLoginPanelColor();
            }
        }

        private void UpdateAppColor()
        {
            Color adjustedColor = AdjustColor(selectedAppColor, sliderAppLightness.Value, sliderAppAlpha.Value);
            previewGroupBox.Background = new SolidColorBrush(adjustedColor);
            rectAppColorPreview.Fill = new SolidColorBrush(adjustedColor);
            txtAppBackgroundColorHex.Text = adjustedColor.ToString();
        }

        private void UpdateLoginPanelColor()
        {
            Color adjustedColor = AdjustColor(selectedLoginPanelColor, sliderLoginPanelLightness.Value, sliderLoginPanelAlpha.Value);
            previewIconBorder.Background = new SolidColorBrush(adjustedColor);
            rectLoginPanelColorPreview.Fill = new SolidColorBrush(adjustedColor);
            txtLoginPanelBackgroundColorHex.Text = adjustedColor.ToString();
        }

        private Color AdjustColor(Color baseColor, double lightness, double alpha)
        {
            // This is a simplified lightness adjustment.
            float factor = (float)(1 + lightness);
            byte r = (byte)Math.Max(0, Math.Min(255, baseColor.R * factor));
            byte g = (byte)Math.Max(0, Math.Min(255, baseColor.G * factor));
            byte b = (byte)Math.Max(0, Math.Min(255, baseColor.B * factor));
            byte a = (byte)Math.Max(0, Math.Min(255, 255 * alpha));

            return Color.FromArgb(a, r, g, b);
        }

        private void HexColor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (sender is TextBox textBox)
                {
                    UpdateColorFromHex(textBox);
                    // Đánh dấu đã xử lý để ngăn tiếng 'ding' khi nhấn Enter
                    e.Handled = true;
                    // Di chuyển focus ra khỏi TextBox để người dùng thấy kết quả ngay
                    textBox.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                }
            }
        }

        private void HexColor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                UpdateColorFromHex(textBox);
            }
        }

        private void UpdateColorFromHex(TextBox textBox)
        {
            try
            {
                // Sử dụng ColorConverter để phân tích chuỗi hex
                var newColor = (Color)ColorConverter.ConvertFromString(textBox.Text);

                if (textBox.Name == "txtAppBackgroundColorHex")
                {
                    selectedAppColor = newColor;
                    UpdateAppColor();
                }
                else if (textBox.Name == "txtLoginPanelBackgroundColorHex")
                {
                    selectedLoginPanelColor = newColor;
                    UpdateLoginPanelColor();
                }
            }
            catch (FormatException)
            {
                // Nếu định dạng không hợp lệ, hoàn nguyên textbox về màu hợp lệ cuối cùng
                if (textBox.Name == "txtAppBackgroundColorHex")
                {
                    textBox.Text = txtAppBackgroundColorHex.Text;
                }
                else if (textBox.Name == "txtLoginPanelBackgroundColorHex")
                {
                    textBox.Text = txtLoginPanelBackgroundColorHex.Text;
                }
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (imgPreview != null)
            {
                imgPreview.Margin = new Thickness(sliderMarginLeft.Value, sliderMarginTop.Value, sliderMarginRight.Value, sliderMarginBottom.Value);
                imgPreview.Opacity = sliderOpacity.Value;
            }
        }

        private void BtnSelectImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpeg;*.jpg)|*.png;*.jpeg;*.jpg|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                txtImagePath.Text = openFileDialog.FileName;
                try
                {
                    imgPreview.Source = new BitmapImage(new Uri(openFileDialog.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Save App Background Color
                Properties.Settings.Default.AppBackgroundColor = txtAppBackgroundColorHex.Text;

                // Save Login Panel Color
                Properties.Settings.Default.LoginIconBgColor = txtLoginPanelBackgroundColorHex.Text;

                // Save Image Path and settings
                Properties.Settings.Default.LoginIconPath = txtImagePath.Text;
                Properties.Settings.Default.LoginIconMarginLeft = sliderMarginLeft.Value;
                Properties.Settings.Default.LoginIconMarginRight = sliderMarginRight.Value;
                Properties.Settings.Default.LoginIconMarginTop = sliderMarginTop.Value;
                Properties.Settings.Default.LoginIconMarginBottom = sliderMarginBottom.Value;
                Properties.Settings.Default.LoginIconOpacity = sliderOpacity.Value;

                Properties.Settings.Default.Save();
                MessageBox.Show("Đã lưu cài đặt thành công! Vui lòng khởi động lại ứng dụng để các thay đổi có hiệu lực.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Are you sure you want to reset all interface settings to their default values?", "Confirm Reset", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                Properties.Settings.Default.Reset();
                Properties.Settings.Default.Save();
                LoadCurrentSettings();
                MessageBox.Show("Settings have been reset to default.", "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadCurrentSettings()
        {
            try
            {
                // Load colors
                var appBgColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.AppBackgroundColor);
                var loginPanelColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.LoginIconBgColor);

                selectedAppColor = appBgColor;
                selectedLoginPanelColor = loginPanelColor;

                // For simplicity, we don't try to reverse-engineer the slider values from the saved color.
                // We just apply the final color.
                UpdateAppColor();
                UpdateLoginPanelColor();

                // Load image path and settings
                txtImagePath.Text = Properties.Settings.Default.LoginIconPath;
                if (!string.IsNullOrEmpty(txtImagePath.Text) && File.Exists(txtImagePath.Text))
                {
                    imgPreview.Source = new BitmapImage(new Uri(txtImagePath.Text));
                }

                sliderMarginLeft.Value = Properties.Settings.Default.LoginIconMarginLeft;
                sliderMarginTop.Value = Properties.Settings.Default.LoginIconMarginTop;
                sliderMarginRight.Value = Properties.Settings.Default.LoginIconMarginRight;
                sliderMarginBottom.Value = Properties.Settings.Default.LoginIconMarginBottom;

                sliderOpacity.Value = Properties.Settings.Default.LoginIconOpacity;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load settings, using defaults. Error: {ex.Message}", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                // In case of error, use hardcoded defaults
                selectedAppColor = Colors.LightGray;
                selectedLoginPanelColor = (Color)ColorConverter.ConvertFromString("#D2B48C");
                UpdateAppColor();
                UpdateLoginPanelColor();
            }
        }
    }
}