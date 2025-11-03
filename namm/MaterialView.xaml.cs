﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class MaterialView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable materialDataTable = new DataTable();

        public MaterialView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUnitsToComboBox();
            LoadMaterials();
        }

        private void LoadUnitsToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT ID, Name FROM Unit WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable unitTable = new DataTable();
                adapter.Fill(unitTable);
                cbUnit.ItemsSource = unitTable.DefaultView;
            }
        }

        private void LoadMaterials()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Dùng JOIN để lấy tên đơn vị tính từ bảng Unit
                string query = @"
                    SELECT 
                        m.ID, m.Name, m.Quantity, m.Price, m.Description, m.IsActive, m.UnitID,
                        u.Name AS UnitName 
                    FROM Material m
                    LEFT JOIN Unit u ON m.UnitID = u.ID";

                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                materialDataTable = new DataTable();
                materialDataTable.Columns.Add("STT", typeof(int));
                materialDataTable.Columns.Add("StatusText", typeof(string));
                adapter.Fill(materialDataTable);

                UpdateStatusText();
                dgMaterials.ItemsSource = materialDataTable.DefaultView;
            }
        }

        private void UpdateStatusText()
        {
            foreach (DataRow row in materialDataTable.Rows)
            {
                row["StatusText"] = (bool)row["IsActive"] ? "Sử dụng" : "Ngưng";
            }
        }

        private void DgMaterials_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgMaterials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgMaterials.SelectedItem is DataRowView row)
            {
                txtName.Text = row["Name"].ToString();
                txtQuantity.Text = row["Quantity"].ToString();
                txtPrice.Text = Convert.ToDecimal(row["Price"]).ToString("G0"); // Bỏ phần thập phân .00
                txtDescription.Text = row["Description"].ToString();
                chkIsActive.IsChecked = (bool)row["IsActive"];

                // Tìm và chọn Unit trong ComboBox
                cbUnit.SelectedValue = row["UnitID"];
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "INSERT INTO Material (Name, UnitID, Quantity, Price, Description, IsActive) VALUES (@Name, @UnitID, @Quantity, @Price, @Description, @IsActive)";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command);

                try
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Thêm nguyên liệu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadMaterials();
                    ResetFields();
                }
                catch (SqlException ex)
                {
                    // Bắt lỗi nếu tên nguyên liệu đã tồn tại (lỗi khóa UNIQUE)
                    MessageBox.Show($"Lỗi khi thêm nguyên liệu: {ex.Message}\n\nCó thể tên nguyên liệu này đã tồn tại.", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgMaterials.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một nguyên liệu để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ValidateInput()) return;

            DataRowView row = (DataRowView)dgMaterials.SelectedItem;
            int materialId = (int)row["ID"];

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE Material SET Name = @Name, UnitID = @UnitID, Quantity = @Quantity, Price = @Price, Description = @Description, IsActive = @IsActive WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", materialId);
                AddParameters(command);

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Cập nhật nguyên liệu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadMaterials();
                ResetFields();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgMaterials.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một nguyên liệu để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa nguyên liệu này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgMaterials.SelectedItem;
                int materialId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "DELETE FROM Material WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", materialId);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Xóa nguyên liệu thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadMaterials();
                    ResetFields();
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            materialDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
        }

        private void ResetFields()
        {
            txtName.Clear();
            txtQuantity.Clear();
            txtPrice.Clear();
            txtDescription.Clear();
            cbUnit.SelectedIndex = -1;
            chkIsActive.IsChecked = true;
            dgMaterials.SelectedItem = null;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || cbUnit.SelectedItem == null || string.IsNullOrWhiteSpace(txtQuantity.Text) || string.IsNullOrWhiteSpace(txtPrice.Text))
            {
                MessageBox.Show("Tên, đơn vị, số lượng và đơn giá không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!decimal.TryParse(txtQuantity.Text, out _) || !decimal.TryParse(txtPrice.Text, out _))
            {
                MessageBox.Show("Số lượng và đơn giá phải là số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void AddParameters(SqlCommand command)
        {
            command.Parameters.AddWithValue("@Name", txtName.Text);
            command.Parameters.AddWithValue("@UnitID", cbUnit.SelectedValue);
            command.Parameters.AddWithValue("@Quantity", Convert.ToDecimal(txtQuantity.Text));
            command.Parameters.AddWithValue("@Price", Convert.ToDecimal(txtPrice.Text));
            command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text);
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }
    }
}