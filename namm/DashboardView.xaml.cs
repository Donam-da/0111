﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        // Sử dụng ObservableCollection để UI tự động cập nhật khi có thay đổi
        private ObservableCollection<BillItem> currentBillItems = new ObservableCollection<BillItem>();

        public DashboardView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            dgBill.ItemsSource = currentBillItems;
            try
            {
                // Tải dữ liệu song song để tăng tốc độ khởi động và giữ cho UI luôn phản hồi.
                var loadTablesTask = LoadTables();
                var loadCategoriesTask = LoadCategories();
                var loadMenuTask = LoadMenu();

                await Task.WhenAll(loadTablesTask, loadCategoriesTask, loadMenuTask);

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
                var categoryTable = new DataTable();
                // Sử dụng Task.Run để chạy tác vụ Fill trên một luồng nền.
                await Task.Run(() => adapter.Fill(categoryTable));

                // Sau khi await, chúng ta đã quay lại luồng UI, có thể cập nhật trực tiếp.
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

                // Sau khi await, chúng ta đã quay lại luồng UI, có thể cập nhật trực tiếp.
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

                // Sau khi await, chúng ta đã quay lại luồng UI, có thể cập nhật trực tiếp.
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

        private void DgMenu_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgTables.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một bàn trước khi thêm món.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgMenu.SelectedItem is DataRowView selectedDrinkRow)
            {
                try
                {
                    int drinkId = (int)selectedDrinkRow["ID"];
                    string drinkName = selectedDrinkRow["Name"].ToString() ?? "Không tên";

                    // Lấy dữ liệu tồn kho một cách đồng bộ
                    var availableStock = GetDrinkStock(drinkId);

                    if (!availableStock.Any())
                    {
                        MessageBox.Show("Đồ uống này chưa được cấu hình để bán (chưa có giá hoặc công thức).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var dialog = new SelectDrinkTypeDialog(drinkName, availableStock);
                    dialog.Owner = Window.GetWindow(this);

                    if (dialog.ShowDialog() == true)
                    {
                        foreach (var selectedItem in dialog.SelectedQuantities)
                        {
                            string drinkType = selectedItem.Key;
                            int quantity = selectedItem.Value;
                            decimal price = Convert.ToDecimal(selectedDrinkRow["ActualPrice"]);

                            var existingItem = currentBillItems.FirstOrDefault(item => item.DrinkId == drinkId && item.DrinkType == drinkType);

                            if (existingItem != null)
                            {
                                existingItem.Quantity += quantity;
                            }
                            else
                            {
                                currentBillItems.Add(new BillItem { DrinkId = drinkId, DrinkName = drinkName, DrinkType = drinkType, Quantity = quantity, Price = price });
                            }
                        }
                        UpdateTotalAmount();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Đã xảy ra lỗi không mong muốn: {ex.Message}", "Lỗi nghiêm trọng", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void UpdateTotalAmount()
        {
            decimal total = currentBillItems.Sum(item => item.TotalPrice);
            tbTotalAmount.Text = $"{total:N0} VNĐ";
        }

        private void DeleteBillItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is BillItem itemToRemove)
            {
                currentBillItems.Remove(itemToRemove);
                UpdateTotalAmount();
            }
        }

        private Dictionary<string, int> GetDrinkStock(int drinkId)
        {
            var stock = new Dictionary<string, int>();
            using (var connection = new SqlConnection(connectionString))
            {
                // 1. Lấy số lượng tồn kho của đồ uống nguyên bản
                var cmdOriginal = new SqlCommand("SELECT StockQuantity FROM Drink WHERE ID = @ID AND OriginalPrice > 0", connection);
                cmdOriginal.Parameters.AddWithValue("@ID", drinkId);

                // 2. Tính số lượng có thể làm của đồ uống pha chế
                var cmdRecipe = new SqlCommand(@"
                    SELECT MIN(FLOOR(m.Quantity / r.Quantity))
                    FROM Recipe r
                    JOIN Material m ON r.MaterialID = m.ID
                    WHERE r.DrinkID = @ID 
                    -- Chỉ tính khi có công thức tồn tại
                    HAVING COUNT(r.DrinkID) > 0", connection);
                cmdRecipe.Parameters.AddWithValue("@ID", drinkId);

                connection.Open();

                var originalStockResult = cmdOriginal.ExecuteScalar();
                if (originalStockResult != null && originalStockResult != DBNull.Value)
                {
                    stock["Nguyên bản"] = Convert.ToInt32(originalStockResult);
                }

                var recipeStockResult = cmdRecipe.ExecuteScalar();
                if (recipeStockResult != null && recipeStockResult != DBNull.Value)
                {
                    stock["Pha chế"] = Convert.ToInt32(recipeStockResult);
                }
            }
            return stock;
        }
    }
}