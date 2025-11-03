﻿using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class DashboardView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable menuDataTable;
        private DataTable tableDataTable;

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
    }
}