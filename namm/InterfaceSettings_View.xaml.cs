using Microsoft.Win32;
using System;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace namm
{
    public partial class InterfaceSettingsView : UserControl
    {
        private Color _appBackgroundColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.AppBackgroundColor);
        private Color _loginPanelBackgroundColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.LoginIconBgColor);

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Đọc các cài đặt đã lưu
            txtImagePath.Text = Properties.Settings.Default.LoginIconPath; // Đường dẫn ảnh vẫn là string

            // Cập nhật các biến Color và hiển thị mã Hex
            _loginPanelBackgroundColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.LoginIconBgColor);
            txtLoginPanelBackgroundColorHex.Text = Properties.Settings.Default.LoginIconBgColor;
            _appBackgroundColor = (Color)ColorConverter.ConvertFromString(Properties.Settings.Default.AppBackgroundColor);
            txtAppBackgroundColorHex.Text = Properties.Settings.Default.AppBackgroundColor;
            
            // Tải các giá trị lề riêng biệt
            sliderMarginLeft.Value = Properties.Settings.Default.LoginIconMarginLeft;
            sliderMarginRight.Value = Properties.Settings.Default.LoginIconMarginRight;
            sliderMarginTop.Value = Properties.Settings.Default.LoginIconMarginTop;
            sliderMarginBottom.Value = Properties.Settings.Default.LoginIconMarginBottom;
            sliderOpacity.Value = Properties.Settings.Default.LoginIconOpacity;

            UpdatePreview();
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
                UpdatePreview();
            }
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded) // Chỉ cập nhật khi view đã được tải xong
            {
                UpdatePreview();
            }
        }

        private void BtnSelectAppBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            var colorPicker = new ColorPickerDialog(_appBackgroundColor);
            if (colorPicker.ShowDialog() == true)
            {
                _appBackgroundColor = colorPicker.SelectedColor;
                txtAppBackgroundColorHex.Text = _appBackgroundColor.ToString(); // Cập nhật TextBox hiển thị mã Hex
                UpdatePreview();
            }
        }

        private void BtnSelectLoginPanelBackgroundColor_Click(object sender, RoutedEventArgs e)
        {
            var colorPicker = new ColorPickerDialog(_loginPanelBackgroundColor);
            if (colorPicker.ShowDialog() == true)
            {
                _loginPanelBackgroundColor = colorPicker.SelectedColor;
                txtLoginPanelBackgroundColorHex.Text = _loginPanelBackgroundColor.ToString(); // Cập nhật TextBox hiển thị mã Hex
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            try
            {
                // Cập nhật màu nền của phần xem trước để minh họa
                previewGroupBox.Background = new SolidColorBrush(_appBackgroundColor);

                // Cập nhật ảnh
                imgPreview.Source = new BitmapImage(new Uri(txtImagePath.Text, UriKind.RelativeOrAbsolute));

                // Cập nhật độ mờ
                imgPreview.Opacity = sliderOpacity.Value;

                // Cập nhật màu nền của panel icon
                previewIconBorder.Background = new SolidColorBrush(_loginPanelBackgroundColor);

                // Cập nhật lề của icon
                imgPreview.Margin = UIHelper.GetConstrainedMargin(
                    sliderMarginLeft.Value,
                    sliderMarginTop.Value,
                    sliderMarginRight.Value,
                    sliderMarginBottom.Value
                );

            }
            catch (Exception ex)
            {
                // Nếu đường dẫn ảnh không hợp lệ, hiển thị ảnh mặc định
                imgPreview.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
                MessageBox.Show($"Không thể tải ảnh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Mở file config để ghi
                // Lưu các giá trị mới
                Properties.Settings.Default.LoginIconPath = txtImagePath.Text;
                Properties.Settings.Default.LoginIconBgColor = txtLoginPanelBackgroundColorHex.Text;
                Properties.Settings.Default.AppBackgroundColor = txtAppBackgroundColorHex.Text;
                Properties.Settings.Default.LoginIconMarginLeft = sliderMarginLeft.Value;
                Properties.Settings.Default.LoginIconMarginRight = sliderMarginRight.Value;
                Properties.Settings.Default.LoginIconMarginTop = sliderMarginTop.Value;
                Properties.Settings.Default.LoginIconMarginBottom = sliderMarginBottom.Value;
                Properties.Settings.Default.LoginIconOpacity = sliderOpacity.Value;

                // Lưu các cài đặt
                Properties.Settings.Default.Save();

                MessageBox.Show("Đã lưu cài đặt thành công! Thay đổi sẽ được áp dụng ở lần đăng nhập tiếp theo.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu cài đặt: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Bạn có chắc muốn đặt lại tất cả cài đặt giao diện về mặc định không?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Đặt lại các cài đặt về giá trị mặc định của chúng
                Properties.Settings.Default.Reset();
                Properties.Settings.Default.Save();

                // Tải lại cài đặt mặc định
                LoadSettings();
                MessageBox.Show("Đã đặt lại về mặc định.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}