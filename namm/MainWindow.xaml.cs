﻿﻿﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using namm.Properties;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace namm
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public MainWindow()
        {
            InitializeComponent();
            LoadCustomInterface();
            LoadRememberedUser();
            this.Loaded += (s, e) => txtUsername.Focus(); // Focus vào ô username khi cửa sổ được tải
        }

        private void LoadCustomInterface()
        {
            try
            {
                // Đọc các cài đặt đã lưu từ App.config
                string iconPath = Properties.Settings.Default.LoginIconPath;

                string bgColor = Properties.Settings.Default.LoginIconBgColor;
                try
                {
                    iconBorder.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(bgColor);
                }
                catch
                {
                    // Nếu mã màu lưu bị lỗi, dùng màu mặc định hoặc màu đã được định nghĩa trong XAML
                }

                imgLoginIcon.Opacity = Properties.Settings.Default.LoginIconOpacity;

                double marginLeft = Properties.Settings.Default.LoginIconMarginLeft;
                double marginRight = Properties.Settings.Default.LoginIconMarginRight;
                double marginTop = Properties.Settings.Default.LoginIconMarginTop;
                double marginBottom = Properties.Settings.Default.LoginIconMarginBottom;

                imgLoginIcon.Margin = UIHelper.GetConstrainedMargin(
                    marginLeft > 0 ? marginLeft : 30,
                    marginTop > 0 ? marginTop : 30,
                    marginRight > 0 ? marginRight : 30,
                    marginBottom > 0 ? marginBottom : 30
                );

                // Thiết lập nguồn ảnh
                imgLoginIcon.Source = new BitmapImage(new Uri(iconPath, UriKind.RelativeOrAbsolute));
            }
            catch (Exception)
            {
                // Nếu có lỗi (ví dụ: file ảnh bị xóa), sử dụng ảnh mặc định
                imgLoginIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/login_icon.png"));
            }
        }

        private void LoadRememberedUser()
        {
            if (Settings.Default.RememberMe)
            {
                txtUsername.Text = Settings.Default.Username;
                pwbPassword.Password = Settings.Default.Password;
                chkRememberMe.IsChecked = true;
            }
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text;
            string password = pwbPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Vui lòng nhập tên đăng nhập và mật khẩu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AccountDTO? loginAccount = CheckLogin(username, password);
            if (loginAccount != null)
            {
                // Lưu hoặc xóa cài đặt tùy thuộc vào checkbox
                if (chkRememberMe.IsChecked == true)
                {
                    Settings.Default.Username = username;
                    Settings.Default.Password = password; // Cảnh báo: Lưu mật khẩu dạng plain text không an toàn
                    Settings.Default.RememberMe = true;
                }
                else
                {
                    Settings.Default.Username = "";
                    Settings.Default.Password = "";
                    Settings.Default.RememberMe = false;
                }
                Settings.Default.Save();

                MainAppWindow mainApp = new MainAppWindow(loginAccount);
                mainApp.Show();
                this.Close(); // Đóng cửa sổ đăng nhập
            }
            else
            {
                MessageBox.Show("Tên đăng nhập hoặc mật khẩu không chính xác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private AccountDTO? CheckLogin(string username, string password)
        {
            AccountDTO? account = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT * FROM Account WHERE UserName=@UserName AND Password=@Password";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", username);
                command.Parameters.AddWithValue("@Password", password); // Nhắc lại: nên hash mật khẩu

                connection.Open();
                SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    account = new AccountDTO
                    {
                        UserName = reader["UserName"].ToString() ?? "",
                        DisplayName = reader["DisplayName"].ToString() ?? "",
                        Type = (int)reader["Type"],
                    };
                }
            }
            return account;
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
