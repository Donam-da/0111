using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class EmployeeView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public EmployeeView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAccounts();
        }

        private void LoadAccounts()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT UserName, DisplayName, Type, PhoneNumber, Address FROM Account";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("STT", typeof(int)); // Thêm cột STT vào DataTable
                adapter.Fill(dataTable);

                dgAccounts.ItemsSource = dataTable.DefaultView;
            }
        }

        private void DgAccounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgAccounts.SelectedItem is DataRowView row)
            {
                txtUserName.Text = row["UserName"].ToString();
                txtDisplayName.Text = row["DisplayName"].ToString();
                txtPhoneNumber.Text = row["PhoneNumber"].ToString();
                txtAddress.Text = row["Address"].ToString();
                cbAccountType.SelectedIndex = Convert.ToInt32(row["Type"]);

                // Không hiển thị mật khẩu khi chọn
                txtPassword.Text = "";
                txtUserName.IsEnabled = false; // Không cho sửa tên đăng nhập (khóa chính)
            }
        }

        private void DgAccounts_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                // Gán giá trị cho cột STT
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            string userName = txtUserName.Text;
            string displayName = txtDisplayName.Text;
            string password = txtPassword.Text;
            string phone = txtPhoneNumber.Text;
            string address = txtAddress.Text;
            int type = Convert.ToInt32(((ComboBoxItem)cbAccountType.SelectedItem).Tag);

            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Tên đăng nhập, tên hiển thị và mật khẩu không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "INSERT INTO Account (UserName, DisplayName, Password, Type, PhoneNumber, Address) VALUES (@UserName, @DisplayName, @Password, @Type, @PhoneNumber, @Address)";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@DisplayName", displayName);
                command.Parameters.AddWithValue("@Password", password); // Cảnh báo: Không an toàn
                command.Parameters.AddWithValue("@Type", type);
                command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(phone) ? (object)DBNull.Value : phone);
                command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(address) ? (object)DBNull.Value : address);

                try
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Thêm nhân viên thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAccounts();
                    ResetFields();
                }
                catch (SqlException ex)
                {
                    MessageBox.Show("Lỗi khi thêm nhân viên: " + ex.Message, "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgAccounts.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một nhân viên để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string userName = txtUserName.Text;
            string displayName = txtDisplayName.Text;
            string password = txtPassword.Text; // Nếu mật khẩu trống, ta không cập nhật
            string phone = txtPhoneNumber.Text;
            string address = txtAddress.Text;
            int type = Convert.ToInt32(((ComboBoxItem)cbAccountType.SelectedItem).Tag);

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Nếu người dùng không nhập mật khẩu mới, giữ nguyên mật khẩu cũ
                string query = string.IsNullOrWhiteSpace(password)
                    ? "UPDATE Account SET DisplayName=@DisplayName, Type=@Type, PhoneNumber=@PhoneNumber, Address=@Address WHERE UserName=@UserName"
                    : "UPDATE Account SET DisplayName=@DisplayName, Password=@Password, Type=@Type, PhoneNumber=@PhoneNumber, Address=@Address WHERE UserName=@UserName";

                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@UserName", userName);
                command.Parameters.AddWithValue("@DisplayName", displayName);
                if (!string.IsNullOrWhiteSpace(password))
                {
                    command.Parameters.AddWithValue("@Password", password);
                }
                command.Parameters.AddWithValue("@Type", type);
                command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(phone) ? (object)DBNull.Value : phone);
                command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(address) ? (object)DBNull.Value : address);

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Cập nhật thông tin thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAccounts();
                ResetFields();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgAccounts.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một nhân viên để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa nhân viên này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                string userName = ((DataRowView)dgAccounts.SelectedItem)["UserName"].ToString();
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "DELETE FROM Account WHERE UserName=@UserName";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@UserName", userName);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Xóa nhân viên thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadAccounts();
                    ResetFields();
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        private void ResetFields()
        {
            txtUserName.Clear();
            txtDisplayName.Clear();
            txtPassword.Clear();
            txtPhoneNumber.Clear();
            txtAddress.Clear();
            cbAccountType.SelectedIndex = 0;
            dgAccounts.SelectedItem = null;
            txtUserName.IsEnabled = true;
        }
    }
}