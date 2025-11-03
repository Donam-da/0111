﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class DashboardView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable? menuDataTable;
        private DataTable? tableDataTable;

        public DashboardView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadTables();
                await LoadCategories();
                await LoadMenu();
                // Gắn sự kiện sau khi đã tải xong dữ liệu ban đầu
                cbCategory.SelectionChanged += CbCategory_SelectionChanged;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi nghiêm trọng khi tải màn hình chính: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadCategories()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Thêm mục "Tất cả" vào danh sách
                const string query = "SELECT 0 AS ID, N'Tất cả' AS Name UNION ALL SELECT ID, Name FROM Category WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable categoryTable = new DataTable();
                await Task.Run(() => adapter.Fill(categoryTable));
                cbCategory.ItemsSource = categoryTable.DefaultView;
                cbCategory.SelectedValuePath = "ID";
                cbCategory.DisplayMemberPath = "Name";
                cbCategory.SelectedIndex = 0; // Mặc định chọn "Tất cả"
            }
        }

        private async Task LoadMenu()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Chỉ lấy những đồ uống đang được kích hoạt để hiển thị trên menu
                const string query = "SELECT ID, Name, DrinkCode, ActualPrice, CategoryID FROM Drink WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                menuDataTable = new DataTable();
                menuDataTable.Columns.Add("STT", typeof(int));
                await Task.Run(() => adapter.Fill(menuDataTable));
                dgMenu.ItemsSource = menuDataTable.DefaultView;
            }
        }

        private async Task LoadTables()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name, Status FROM TableFood ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                tableDataTable = new DataTable();
                tableDataTable.Columns.Add("STT", typeof(int));
                await Task.Run(() => adapter.Fill(tableDataTable));
                dgTables.ItemsSource = tableDataTable.DefaultView;
            }
        }

        private void CbCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterMenu();
        }

        private void FilterMenu()
        {
            if (menuDataTable == null || cbCategory.SelectedValue == null)
            {
                return;
            }

            int categoryId = (int)cbCategory.SelectedValue;

            if (categoryId == 0) // 0 là ID của "Tất cả"
            {
                menuDataTable.DefaultView.RowFilter = string.Empty; // Xóa bộ lọc
            }
            else
            {
                menuDataTable.DefaultView.RowFilter = $"CategoryID = {categoryId}"; // Lọc theo CategoryID
            }
        }

        private void DgTables_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Logic này có thể đã có sẵn, nếu chưa có thì thêm vào
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgMenu_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private async void DgMenu_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 1. Kiểm tra đã chọn bàn chưa
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn trước khi thêm món.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Lấy thông tin đồ uống được chọn
            if (dgMenu.SelectedItem is DataRowView selectedDrinkRow)
            {
                int drinkId = (int)selectedDrinkRow["ID"];
                string drinkName = selectedDrinkRow["Name"].ToString();

                // 3. Kiểm tra các kiểu có sẵn của đồ uống từ DB
                var availableStock = await GetDrinkStockAsync(drinkId);

                if (!availableStock.Any())
                {
                    MessageBox.Show("Đồ uống này chưa được cấu hình để bán (chưa có giá hoặc công thức).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 4. Mở dialog để nhập số lượng cho các kiểu có sẵn
                var dialog = new SelectDrinkTypeDialog(drinkName, availableStock);
                dialog.Owner = Window.GetWindow(this); // Đặt cửa sổ chính làm chủ sở hữu

                if (dialog.ShowDialog() == true)
                {
                    foreach (var item in dialog.SelectedQuantities)
                    {
                        // TODO: Thêm logic để thêm đồ uống vào hóa đơn và trừ tồn kho
                        MessageBox.Show($"Đã thêm: {item.Value} x {drinkName} (Kiểu: {item.Key})", "Thông báo");
                    }
                }
            }
        }

        private async Task<Dictionary<string, int>> GetDrinkStockAsync(int drinkId)
        {
            var stock = new Dictionary<string, int>();
            using (var connection = new SqlConnection(connectionString))
            {
                // 1. Lấy số lượng tồn kho của đồ uống nguyên bản
                var cmdOriginal = new SqlCommand("SELECT StockQuantity FROM Drink WHERE ID = @ID AND OriginalPrice > 0", connection);
                cmdOriginal.Parameters.AddWithValue("@ID", drinkId);

                // 2. Tính số lượng có thể làm của đồ uống pha chế
                var cmdRecipe = new SqlCommand(@"
                    SELECT MIN(ISNULL(FLOOR(m.Quantity / r.Quantity), 0))
                    FROM Recipe r
                    JOIN Material m ON r.MaterialID = m.ID
                    WHERE r.DrinkID = @ID", connection);
                cmdRecipe.Parameters.AddWithValue("@ID", drinkId);

                await connection.OpenAsync();

                var originalStockResult = await cmdOriginal.ExecuteScalarAsync();
                if (originalStockResult != null && originalStockResult != DBNull.Value)
                {
                    stock["Nguyên bản"] = Convert.ToInt32(originalStockResult);
                }

                var recipeStockResult = await cmdRecipe.ExecuteScalarAsync();
                if (recipeStockResult != null && recipeStockResult != DBNull.Value)
                {
                    stock["Pha chế"] = Convert.ToInt32(recipeStockResult);
                }
            }
            return stock;
        }
    }
}