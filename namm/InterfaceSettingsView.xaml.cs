using Microsoft.Win32;
using System;
using System.Configuration;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace namm
{
    public partial class InterfaceSettingsView : UserControl
    {
        public InterfaceSettingsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Đọc các cài đặt đã lưu
            txtImagePath.Text = ConfigurationManager.AppSettings["LoginIconPath"] ?? "Resources/login_icon.png";
            
            if (double.TryParse(ConfigurationManager.AppSettings["LoginIconSize"], out double size))
                sliderSize.Value = size;
            else
                sliderSize.Value = 240; // Kích thước mặc định

            if (double.TryParse(ConfigurationManager.AppSettings["LoginIconOpacity"], out double opacity))
                sliderOpacity.Value = opacity;
            else
                sliderOpacity.Value = 1.0; // Mặc định

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

        private void UpdatePreview()
        {
            try
            {
                // Cập nhật ảnh
                imgPreview.Source = new BitmapImage(new Uri(txtImagePath.Text, UriKind.RelativeOrAbsolute));

                // Cập nhật kích thước
                double size = sliderSize.Value;
                // Kích thước của Border chứa ảnh sẽ là `size`, ảnh bên trong sẽ có margin 30
                imgPreview.Width = size - 60;
                imgPreview.Height = size - 60;

                // Cập nhật độ mờ
                imgPreview.Opacity = sliderOpacity.Value;
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
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

                // Lưu các giá trị mới
                config.AppSettings.Settings.Remove("LoginIconPath");
                config.AppSettings.Settings.Add("LoginIconPath", txtImagePath.Text);

                config.AppSettings.Settings.Remove("LoginIconSize");
                config.AppSettings.Settings.Add("LoginIconSize", sliderSize.Value.ToString());

                config.AppSettings.Settings.Remove("LoginIconOpacity");
                config.AppSettings.Settings.Add("LoginIconOpacity", sliderOpacity.Value.ToString());

                // Lưu file config và làm mới section
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

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
                // Xóa các key cài đặt
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings.Remove("LoginIconPath");
                config.AppSettings.Settings.Remove("LoginIconSize");
                config.AppSettings.Settings.Remove("LoginIconOpacity");
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");

                // Tải lại cài đặt mặc định
                LoadSettings();
                MessageBox.Show("Đã đặt lại về mặc định.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}