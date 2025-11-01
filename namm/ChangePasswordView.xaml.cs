using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class ChangePasswordView : UserControl
    {
        // Sự kiện để yêu cầu đăng xuất (nullable)
        public event EventHandler? LogoutRequested;

        private AccountDTO loggedInAccount;
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public ChangePasswordView(AccountDTO account)
        {
            InitializeComponent();
            this.loggedInAccount = account;
            txtUserName.Text = loggedInAccount.UserName;
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            string oldPassword = pwbOldPassword.Password;
            string newPassword = pwbNewPassword.Password;
            string confirmPassword = pwbConfirmPassword.Password;

            if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (newPassword != confirmPassword)
            {
                MessageBox.Show("Mật khẩu mới và mật khẩu xác nhận không trùng khớp.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Kiểm tra mật khẩu cũ có đúng không
            if (!CheckOldPassword(loggedInAccount.UserName, oldPassword))
            {
                MessageBox.Show("Mật khẩu cũ không chính xác.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Cập nhật mật khẩu mới
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE Account SET Password = @NewPassword WHERE UserName = @UserName";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@NewPassword", newPassword);
                command.Parameters.AddWithValue("@UserName", loggedInAccount.UserName);

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Đổi mật khẩu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

                // Kích hoạt sự kiện yêu cầu đăng xuất
                LogoutRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private bool CheckOldPassword(string userName, string oldPassword)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT COUNT(1) FROM Account WHERE UserName = @UserName AND Password = @Password";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@Password", oldPassword);
                connection.Open();
                int count = Convert.ToInt32(command.ExecuteScalar());
                return count > 0;
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        private void ResetFields()
        {
            pwbOldPassword.Clear();
            pwbNewPassword.Clear();
            pwbConfirmPassword.Clear();
        }
    }
}