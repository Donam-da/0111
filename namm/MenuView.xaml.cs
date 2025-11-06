﻿﻿﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class MenuView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable? menuDataTable;

        public MenuView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCategoriesToComboBox();
            await LoadMenuItems();
        }

        private async Task LoadCategoriesToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name FROM Category WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable categoryTable = new DataTable();
                await Task.Run(() => adapter.Fill(categoryTable));
                cbCategory.ItemsSource = categoryTable.DefaultView;
            }
        }

        private async Task LoadMenuItems()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT 
                        d.ID, 
                        d.DrinkCode, 
                        d.Name, 
                        d.IsActive, 
                        d.CategoryID,
                        c.Name AS CategoryName,
                        CASE 
                            WHEN d.OriginalPrice > 0 AND EXISTS (SELECT 1 FROM Recipe r WHERE r.DrinkID = d.ID) AND d.IsActive = 1 THEN N'Nguyên bản/Pha chế'
                            WHEN d.OriginalPrice > 0 THEN N'Nguyên bản'
                            WHEN EXISTS (SELECT 1 FROM Recipe r WHERE r.DrinkID = d.ID) AND d.IsActive = 1 THEN N'Pha chế'
                            ELSE N'Chưa gán' 
                        END AS DrinkType
                    FROM Drink d 
                    LEFT JOIN Category c ON d.CategoryID = c.ID";

                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                menuDataTable = new DataTable();
                menuDataTable.Columns.Add("STT", typeof(int));
                menuDataTable.Columns.Add("StatusText", typeof(string));
                await Task.Run(() => adapter.Fill(menuDataTable));

                UpdateStatusText();
                dgMenuItems.ItemsSource = menuDataTable.DefaultView;
            }
        }

        private void UpdateStatusText()
        {
            if (menuDataTable != null) foreach (DataRow row in menuDataTable.Rows)
            {
                row["StatusText"] = (bool)row["IsActive"] ? "Hiển thị" : "Ẩn";
            }
        }

        private void DgMenuItems_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgMenuItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgMenuItems.SelectedItem is DataRowView row)
            {
                // Tạm ngắt sự kiện để tránh tạo mã mới khi đang chọn
                txtName.LostFocus -= TxtName_LostFocus;

                txtName.Text = row["Name"] as string ?? string.Empty;
                txtDrinkCode.Text = row["DrinkCode"] as string ?? string.Empty;
                cbCategory.SelectedValue = row["CategoryID"]; // This should work if CategoryID is not null
                chkIsActive.IsChecked = Convert.ToBoolean(row["IsActive"]);

                // Bật lại sự kiện
                txtName.LostFocus += TxtName_LostFocus;
            }
            else
            {
                ResetFields();
            }
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "INSERT INTO Drink (DrinkCode, Name, CategoryID, IsActive, OriginalPrice, RecipeCost, ActualPrice) VALUES (@DrinkCode, @Name, @CategoryID, @IsActive, 0, 0, 0)";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command);

                try
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Thêm đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadMenuItems();
                    ResetFields();
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi thêm đồ uống: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dgMenuItems.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để cập nhật.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ValidateInput()) return;

            if (dgMenuItems.SelectedItem is DataRowView row)
            {
                int drinkId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    const string query = "UPDATE Drink SET Name = @Name, CategoryID = @CategoryID, IsActive = @IsActive WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    AddParameters(command, drinkId);

                    try
                    {
                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                        MessageBox.Show("Cập nhật đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadMenuItems();
                        ResetFields();
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show($"Lỗi khi cập nhật đồ uống: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgMenuItems.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa vĩnh viễn đồ uống này? Hành động này không thể hoàn tác.", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (dgMenuItems.SelectedItem is DataRowView row)
                {
                    int drinkId = (int)row["ID"];

                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        // This query will permanently delete the drink.
                        // Ensure cascading deletes are set up in the DB for related tables (like Recipe) or handle them here.
                        const string query = "DELETE FROM Drink WHERE ID = @ID";
                        SqlCommand command = new SqlCommand(query, connection);
                        command.Parameters.AddWithValue("@ID", drinkId);

                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                        MessageBox.Show("Xóa đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadMenuItems();
                        ResetFields();
                    }
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (menuDataTable != null)
            {
                menuDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
            }
        }

        private void TxtName_LostFocus(object sender, RoutedEventArgs e)
        {
            // Chỉ tạo mã khi người dùng đang thêm mới (chưa chọn item nào từ grid)
            if (dgMenuItems.SelectedItem == null)
            {
                txtDrinkCode.Text = GenerateMenuCode(txtName.Text); // Logic tạo mã vẫn giữ nguyên
            }
        }

        private string GenerateMenuCode(string drinkName)
        {
            // Chuyển tên đồ uống thành mã không dấu, không khoảng trắng, không ký tự đặc biệt
            string temp = drinkName.ToLower();
            temp = Regex.Replace(temp, "[áàảãạâấầẩẫậăắằẳẵặ]", "a");
            temp = Regex.Replace(temp, "[éèẻẽẹêếềểễệ]", "e");
            temp = Regex.Replace(temp, "[íìỉĩị]", "i");
            temp = Regex.Replace(temp, "[óòỏõọôốồổỗộơớờởỡợ]", "o");
            temp = Regex.Replace(temp, "[úùủũụưứừửữự]", "u");
            temp = Regex.Replace(temp, "[ýỳỷỹỵ]", "y");
            temp = Regex.Replace(temp, "[đ]", "d");
            // Bỏ các ký tự đặc biệt và khoảng trắng
            temp = Regex.Replace(temp.Replace(" ", ""), "[^a-z0-9]", "");
            return temp;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || cbCategory.SelectedItem == null || string.IsNullOrWhiteSpace(txtDrinkCode.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin: Tên và loại đồ uống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void AddParameters(SqlCommand command, int? id = null)
        {
            if (id.HasValue)
            {
                command.Parameters.AddWithValue("@ID", id.Value);
            }
            command.Parameters.AddWithValue("@DrinkCode", txtDrinkCode.Text);
            command.Parameters.AddWithValue("@Name", txtName.Text);
            command.Parameters.AddWithValue("@CategoryID", cbCategory.SelectedValue);
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }

        private void ResetFields()
        {
            txtName.Clear();
            txtDrinkCode.Clear();
            cbCategory.SelectedIndex = -1;
            chkIsActive.IsChecked = true;
            dgMenuItems.SelectedItem = null;
        }
    }
}