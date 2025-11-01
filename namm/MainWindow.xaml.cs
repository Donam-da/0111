﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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
        public MainWindow()
        {
            InitializeComponent();
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

            if (CheckLogin(username, password))
            {
                // Mở cửa sổ chính của ứng dụng
                MainAppWindow mainApp = new MainAppWindow();
                mainApp.Show();
                this.Close(); // Đóng cửa sổ đăng nhập
            }
            else
            {
                MessageBox.Show("Tên đăng nhập hoặc mật khẩu không chính xác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CheckLogin(string username, string password)
        {
            bool isValid = false;
            string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT COUNT(1) FROM Account WHERE UserName=@UserName AND Password=@Password";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", username);
                command.Parameters.AddWithValue("@Password", password); // Nhắc lại: nên hash mật khẩu

                connection.Open();
                int count = Convert.ToInt32(command.ExecuteScalar());
                if (count == 1)
                {
                    isValid = true;
                }
            }
            return isValid;
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
