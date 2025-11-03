﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class TableView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public TableView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTables();
        }

        private void LoadTables()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT ID, Name, Capacity, Status FROM TableFood";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("STT", typeof(int));
                adapter.Fill(dataTable);
                dgTables.ItemsSource = dataTable.DefaultView;
            }
        }

        private void DgTables_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTables.SelectedItem is DataRowView row)
            {
                // Chỉ hiển thị số của bàn trong ô nhập liệu
                string fullName = row["Name"].ToString() ?? "";
                string tableNumber = fullName.Replace("Bàn ", "").Trim();
                txtName.Text = tableNumber;

                txtCapacity.Text = row["Capacity"].ToString();
                cbStatus.Text = row["Status"].ToString();

                // Khi chọn một bàn, bật chế độ Sửa/Xóa và tắt chế độ Thêm
                btnAdd.IsEnabled = false;
                btnEdit.IsEnabled = true;
                btnDelete.IsEnabled = true;
            }
            else
            {
                ResetFields();
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtName.Text, out _))
            {
                MessageBox.Show("Mã bàn phải là một số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "INSERT INTO TableFood (Name, Capacity, Status) VALUES (@Name, @Capacity, @Status)";
                SqlCommand command = new SqlCommand(query, connection);
                // Tự động thêm "Bàn " vào trước số người dùng nhập
                command.Parameters.AddWithValue("@Name", "Bàn " + txtName.Text);
                command.Parameters.AddWithValue("@Capacity", Convert.ToInt32(txtCapacity.Text));
                command.Parameters.AddWithValue("@Status", ((ComboBoxItem)cbStatus.SelectedItem).Content.ToString());

                try
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Thêm bàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadTables();
                    ResetFields();
                }
                catch (SqlException ex)
                {
                    // Bắt lỗi nếu tên bàn đã tồn tại (lỗi khóa UNIQUE)
                    MessageBox.Show($"Lỗi khi thêm bàn: {ex.Message}\n\nCó thể tên bàn này đã tồn tại.", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtName.Text, out _))
            {
                MessageBox.Show("Mã bàn phải là một số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DataRowView row = (DataRowView)dgTables.SelectedItem;
            int tableId = (int)row["ID"];

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE TableFood SET Name = @Name, Capacity = @Capacity, Status = @Status WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", tableId);
                // Tự động thêm "Bàn " vào trước số người dùng nhập
                command.Parameters.AddWithValue("@Name", "Bàn " + txtName.Text);
                command.Parameters.AddWithValue("@Capacity", Convert.ToInt32(txtCapacity.Text));
                command.Parameters.AddWithValue("@Status", ((ComboBoxItem)cbStatus.SelectedItem).Content.ToString());

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Cập nhật thông tin bàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadTables();
                ResetFields();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa bàn này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgTables.SelectedItem;
                int tableId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "DELETE FROM TableFood WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", tableId);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Xóa bàn thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadTables();
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
            txtName.Clear();
            txtCapacity.Clear();
            cbStatus.SelectedIndex = 0;
            dgTables.SelectedItem = null;

            // Khi làm mới, bật chế độ Thêm và tắt chế độ Sửa/Xóa
            btnAdd.IsEnabled = true;
            btnEdit.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }
    }
}