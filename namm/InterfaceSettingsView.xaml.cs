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
            txtBackgroundColor.Text = ConfigurationManager.AppSettings["LoginIconBgColor"] ?? "#D2B48C";
            
            // Tải các giá trị lề riêng biệt
            if (double.TryParse(ConfigurationManager.AppSettings["LoginIconMarginLeft"], out double marginLeft)) sliderMarginLeft.Value = marginLeft;
            else sliderMarginLeft.Value = 30;

            if (double.TryParse(ConfigurationManager.AppSettings["LoginIconMarginRight"], out double marginRight)) sliderMarginRight.Value = marginRight;
            else sliderMarginRight.Value = 30;

            if (double.TryParse(ConfigurationManager.AppSettings["LoginIconMarginTop"], out double marginTop)) sliderMarginTop.Value = marginTop;
            else sliderMarginTop.Value = 30;

            if (double.TryParse(ConfigurationManager.AppSettings["LoginIconMarginBottom"], out double marginBottom)) sliderMarginBottom.Value = marginBottom;
            else sliderMarginBottom.Value = 30;

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

        private void TxtBackgroundColor_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Chỉ cập nhật khi view đã được tải xong để tránh lỗi không cần thiết
            if (IsLoaded)
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

                // Cập nhật độ mờ
                imgPreview.Opacity = sliderOpacity.Value;

                // Cập nhật lề của icon
                imgPreview.Margin = UIHelper.GetConstrainedMargin(
                    sliderMarginLeft.Value,
                    sliderMarginTop.Value,
                    sliderMarginRight.Value,
                    sliderMarginBottom.Value
                );

                // Cập nhật màu nền
                try
                {
                    previewIconBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(txtBackgroundColor.Text);
                }
                catch
                {
                    // Nếu mã màu không hợp lệ, không làm gì cả, giữ nguyên màu cũ
                }
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

                config.AppSettings.Settings.Remove("LoginIconBgColor");
                config.AppSettings.Settings.Add("LoginIconBgColor", txtBackgroundColor.Text);

                config.AppSettings.Settings.Remove("LoginIconMarginLeft");
                config.AppSettings.Settings.Add("LoginIconMarginLeft", sliderMarginLeft.Value.ToString());
                config.AppSettings.Settings.Remove("LoginIconMarginRight");
                config.AppSettings.Settings.Add("LoginIconMarginRight", sliderMarginRight.Value.ToString());
                config.AppSettings.Settings.Remove("LoginIconMarginTop");
                config.AppSettings.Settings.Add("LoginIconMarginTop", sliderMarginTop.Value.ToString());
                config.AppSettings.Settings.Remove("LoginIconMarginBottom");
                config.AppSettings.Settings.Add("LoginIconMarginBottom", sliderMarginBottom.Value.ToString());

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
                config.AppSettings.Settings.Remove("LoginIconBgColor");
                config.AppSettings.Settings.Remove("LoginIconMarginLeft");
                config.AppSettings.Settings.Remove("LoginIconMarginRight");
                config.AppSettings.Settings.Remove("LoginIconMarginTop");
                config.AppSettings.Settings.Remove("LoginIconMarginBottom");
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