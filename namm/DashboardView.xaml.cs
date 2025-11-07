﻿﻿﻿﻿﻿﻿﻿using System;
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
        // Lưu trữ hóa đơn cho mỗi bàn, với Key là TableID
        private Dictionary<int, ObservableCollection<BillItem>> billsByTable = new Dictionary<int, ObservableCollection<BillItem>>();

        public DashboardView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
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

        private async void DgTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgTables.SelectedItem is DataRowView selectedTable)
            {
                string tableName = selectedTable["Name"].ToString() ?? "Không xác định";
                tbSelectedTable.Text = $"Chọn: {tableName}";
                tbSelectedTable.FontStyle = FontStyles.Normal;
                tbSelectedTable.Foreground = System.Windows.Media.Brushes.Black;

                // Tải hóa đơn chưa thanh toán từ DB cho bàn này
                int tableId = (int)selectedTable["ID"];
                var billItems = await LoadUnpaidBillForTableAsync(tableId);
                billsByTable[tableId] = billItems;

                // Hiển thị hóa đơn của bàn đã chọn
                dgBill.ItemsSource = billItems;
                UpdateTotalAmount();

                // Sau khi tải hóa đơn, đồng bộ lại trạng thái bàn cho chính xác
                SyncTableStatusBasedOnBill();
            }
            else
            {
                tbSelectedTable.Text = "(Chưa chọn bàn)";
                tbSelectedTable.FontStyle = FontStyles.Italic;
                tbSelectedTable.Foreground = System.Windows.Media.Brushes.Gray;

                // Khi không có bàn nào được chọn, xóa hiển thị hóa đơn
                dgBill.ItemsSource = null;
                UpdateTotalAmount();
            }
        }

        private async void DgMenu_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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

                    // Lấy dữ liệu tồn kho một cách bất đồng bộ để không làm treo UI
                    var availableStock = await GetDrinkStockAsync(drinkId);

                    if (!availableStock.Any())
                    {
                        MessageBox.Show("Đồ uống này chưa được cấu hình để bán (chưa có giá hoặc công thức).", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var dialog = new SelectDrinkTypeDialog(drinkName, availableStock);
                    dialog.Owner = Window.GetWindow(this);

                    if (dialog.ShowDialog() == true)
                    {
                        // Lấy hóa đơn của bàn hiện tại
                        int currentTableId = (int)((DataRowView)dgTables.SelectedItem)["ID"];
                        var currentBillItems = billsByTable[currentTableId];

                        foreach (var selectedItem in dialog.SelectedQuantities)
                        {
                            string drinkType = selectedItem.Key;
                            int quantity = selectedItem.Value;
                            decimal price = Convert.ToDecimal(selectedDrinkRow["ActualPrice"]);
                            string baseDrinkCode = selectedDrinkRow["DrinkCode"].ToString() ?? "";

                            var existingItem = currentBillItems.FirstOrDefault(item => item.DrinkId == drinkId && item.DrinkType == drinkType);

                            if (existingItem != null)
                            {
                                existingItem.Quantity += quantity;
                            }
                            else
                            {
                                currentBillItems.Add(new BillItem { 
                                    DrinkId = drinkId, 
                                    DrinkName = drinkName, 
                                    DrinkTypeCode = $"{baseDrinkCode}_{(drinkType == "Nguyên bản" ? "NB" : "PC")}",
                                    DrinkType = drinkType, 
                                    Quantity = quantity, 
                                    Price = price 
                                });
                            }
                            // Cập nhật kho cho món vừa được thêm/cập nhật
                            await UpdateStockForDrinkAsync(drinkId, drinkType, quantity);
                        }
                        // Sau khi xử lý tất cả các món, lưu toàn bộ hóa đơn vào DB
                        await SaveBillToDbAsync(currentTableId, currentBillItems);

                        UpdateTotalAmount();
                        SyncTableStatusBasedOnBill();
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
            // Tính tổng tiền dựa trên hóa đơn đang được hiển thị
            if (dgBill.ItemsSource is ObservableCollection<BillItem> currentBillItems)
            {
                decimal total = currentBillItems.Sum(item => item.TotalPrice);
                tbTotalAmount.Text = $"{total:N0} VNĐ";
            }
            else
            {
                tbTotalAmount.Text = "0 VNĐ";
            }
        }

        private async void DeleteBillItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is BillItem itemToRemove && dgBill.ItemsSource is ObservableCollection<BillItem> currentBillItems)
            {
                // Trước khi xóa, hoàn trả lại số lượng vào kho
                // Chạy tác vụ này ở chế độ nền
                _ = UpdateStockForDrinkAsync(itemToRemove.DrinkId, itemToRemove.DrinkType, -itemToRemove.Quantity);

                currentBillItems.Remove(itemToRemove);
                UpdateTotalAmount();

                // Nếu hóa đơn trống sau khi xóa, chuyển trạng thái bàn về "Trống"
                if (currentBillItems.Any())
                {
                    // Nếu hóa đơn vẫn còn món, chỉ cập nhật lại DB
                    int currentTableId = (int)((DataRowView)dgTables.SelectedItem)["ID"];
                    await SaveBillToDbAsync(currentTableId, currentBillItems);
                }
                // Sau khi xóa món, đồng bộ lại trạng thái bàn
                SyncTableStatusBasedOnBill();
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
                    SELECT MIN(FLOOR(m.Quantity / r.Quantity))
                    FROM Recipe r
                    JOIN Material m ON r.MaterialID = m.ID
                    WHERE r.DrinkID = @ID 
                    -- Chỉ tính khi có công thức tồn tại
                    HAVING COUNT(r.DrinkID) > 0", connection);
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

        private async Task UpdateTableStatusInDbAsync(int tableId, string newStatus)
        {
            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    var command = new SqlCommand("UPDATE TableFood SET Status = @Status WHERE ID = @ID", connection);
                    command.Parameters.AddWithValue("@Status", newStatus);
                    command.Parameters.AddWithValue("@ID", tableId);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                // Ghi lại lỗi hoặc hiển thị một thông báo không làm phiền người dùng
                MessageBox.Show($"Không thể cập nhật trạng thái bàn vào cơ sở dữ liệu: {ex.Message}", "Lỗi nền", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task UpdateStockForDrinkAsync(int drinkId, string drinkType, int quantityChange)
        {
            // quantityChange > 0: Trừ kho (thêm món)
            // quantityChange < 0: Hoàn kho (xóa món)
            if (quantityChange == 0) return;

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                if (drinkType == "Nguyên bản")
                {
                    var cmd = new SqlCommand("UPDATE Drink SET StockQuantity = StockQuantity - @QuantityChange WHERE ID = @DrinkID", connection);
                    cmd.Parameters.AddWithValue("@DrinkID", drinkId);
                    cmd.Parameters.AddWithValue("@QuantityChange", quantityChange);
                    await cmd.ExecuteNonQueryAsync();
                }
                else if (drinkType == "Pha chế")
                {
                    // Lấy công thức của đồ uống
                    var recipeCmd = new SqlCommand("SELECT MaterialID, Quantity FROM Recipe WHERE DrinkID = @DrinkID", connection);
                    recipeCmd.Parameters.AddWithValue("@DrinkID", drinkId);

                    var materialsToUpdate = new List<(int MaterialID, decimal RecipeQuantity)>();
                    using (var reader = await recipeCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            materialsToUpdate.Add((reader.GetInt32(0), reader.GetDecimal(1)));
                        }
                    }

                    // Cập nhật số lượng cho từng nguyên liệu
                    foreach (var material in materialsToUpdate)
                    {
                        var updateMaterialCmd = new SqlCommand("UPDATE Material SET Quantity = Quantity - @QuantityChange WHERE ID = @MaterialID", connection);
                        decimal totalMaterialChange = (decimal)material.RecipeQuantity * quantityChange;
                        updateMaterialCmd.Parameters.AddWithValue("@MaterialID", material.MaterialID);
                        updateMaterialCmd.Parameters.AddWithValue("@QuantityChange", totalMaterialChange);
                        await updateMaterialCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        private async Task<ObservableCollection<BillItem>> LoadUnpaidBillForTableAsync(int tableId)
        {
            var billItems = new ObservableCollection<BillItem>();
            using (var connection = new SqlConnection(connectionString))
            {
                // Tìm hóa đơn chưa thanh toán (Status = 0) của bàn
                const string query = @"                    SELECT bi.DrinkID, d.Name, bi.DrinkType, bi.Quantity, bi.Price, d.DrinkCode
                    FROM BillInfo bi
                    JOIN Bill b ON bi.BillID = b.ID
                    JOIN Drink d ON bi.DrinkID = d.ID
                    WHERE b.TableID = @TableID AND b.Status = 0";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TableID", tableId);

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string drinkType = reader.GetString(2);
                        string baseDrinkCode = reader.GetString(5);

                        billItems.Add(new BillItem
                        {
                            // Xây dựng lại DrinkTypeCode khi tải từ DB
                            DrinkTypeCode = $"{baseDrinkCode}_{(drinkType == "Nguyên bản" ? "NB" : "PC")}",
                            DrinkId = reader.GetInt32(0),
                            DrinkName = reader.GetString(1),
                            DrinkType = reader.GetString(2),
                            Quantity = reader.GetInt32(3),
                            Price = reader.GetDecimal(4)
                        });
                    }
                }
            }
            return billItems;
        }

        private async Task SaveBillToDbAsync(int tableId, ObservableCollection<BillItem> billItems)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Tìm hoặc tạo hóa đơn chưa thanh toán cho bàn
                        var cmdFindBill = new SqlCommand("SELECT ID FROM Bill WHERE TableID = @TableID AND Status = 0", connection, transaction);
                        cmdFindBill.Parameters.AddWithValue("@TableID", tableId);
                        var billIdResult = await cmdFindBill.ExecuteScalarAsync();
                        int billId;

                        if (billIdResult != null)
                        {
                            billId = (int)billIdResult;
                            // Xóa các chi tiết hóa đơn cũ để ghi đè
                            var cmdDeleteInfo = new SqlCommand("DELETE FROM BillInfo WHERE BillID = @BillID", connection, transaction);
                            cmdDeleteInfo.Parameters.AddWithValue("@BillID", billId);
                            await cmdDeleteInfo.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            // Tạo hóa đơn mới
                            var cmdCreateBill = new SqlCommand("INSERT INTO Bill (TableID, Status) OUTPUT INSERTED.ID VALUES (@TableID, 0)", connection, transaction);
                            cmdCreateBill.Parameters.AddWithValue("@TableID", tableId);
                            billId = (int)await cmdCreateBill.ExecuteScalarAsync();
                        }

                        // 2. Thêm các chi tiết hóa đơn mới
                        foreach (var item in billItems)
                        {
                            var cmdInsertInfo = new SqlCommand("INSERT INTO BillInfo (BillID, DrinkID, DrinkType, Quantity, Price) VALUES (@BillID, @DrinkID, @DrinkType, @Quantity, @Price)", connection, transaction);
                            cmdInsertInfo.Parameters.AddWithValue("@BillID", billId);
                            // Lưu ý: DrinkTypeCode không được lưu vào DB, nó chỉ dùng để hiển thị
                            cmdInsertInfo.Parameters.AddWithValue("@DrinkID", item.DrinkId);
                            cmdInsertInfo.Parameters.AddWithValue("@DrinkType", item.DrinkType);
                            cmdInsertInfo.Parameters.AddWithValue("@Quantity", item.Quantity);
                            cmdInsertInfo.Parameters.AddWithValue("@Price", item.Price);
                            await cmdInsertInfo.ExecuteNonQueryAsync();
                        }

                        // 3. Cập nhật tổng tiền cho hóa đơn
                        decimal totalAmount = billItems.Sum(i => i.TotalPrice);
                        var cmdUpdateTotal = new SqlCommand("UPDATE Bill SET TotalAmount = @TotalAmount WHERE ID = @BillID", connection, transaction);
                        cmdUpdateTotal.Parameters.AddWithValue("@TotalAmount", totalAmount);
                        cmdUpdateTotal.Parameters.AddWithValue("@BillID", billId);
                        await cmdUpdateTotal.ExecuteNonQueryAsync();

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw; // Ném lại lỗi để được xử lý ở lớp ngoài
                    }
                }
            }
        }

        private async Task ClearBillFromDbAsync(int tableId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                // Để đảm bảo an toàn, chúng ta sẽ xóa các BillInfo trước, sau đó mới xóa Bill
                const string query = @"
                    DELETE FROM BillInfo WHERE BillID IN (SELECT ID FROM Bill WHERE TableID = @TableID AND Status = 0);
                    DELETE FROM Bill WHERE TableID = @TableID AND Status = 0;";

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@TableID", tableId);
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
        }

        private void SyncTableStatusBasedOnBill()
        {
            if (dgTables.SelectedItem is DataRowView selectedTable && dgBill.ItemsSource is ObservableCollection<BillItem> currentBill)
            {
                int tableId = (int)selectedTable.Row["ID"];
                string currentStatus = selectedTable.Row["Status"].ToString();
                string newStatus;

                if (currentBill.Any())
                {
                    // Nếu hóa đơn có món, trạng thái phải là "Có người"
                    newStatus = "Có người";
                }
                else
                {
                    // Nếu hóa đơn trống, trạng thái phải là "Trống"
                    newStatus = "Trống";
                    // Đồng thời xóa hóa đơn rỗng khỏi DB
                    _ = ClearBillFromDbAsync(tableId);
                }

                if (currentStatus != newStatus)
                {
                    selectedTable.Row["Status"] = newStatus;
                    _ = UpdateTableStatusInDbAsync(tableId, newStatus);
                }
            }
        }

        private void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            // 1. Kiểm tra đã chọn bàn và có hóa đơn chưa
            if (dgTables.SelectedItem == null || !(dgBill.ItemsSource is ObservableCollection<BillItem> currentBill) || !currentBill.Any())
            {
                MessageBox.Show("Vui lòng chọn bàn có hóa đơn để thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2. Lấy thông tin cần thiết để truyền đi
            var selectedTable = (DataRowView)dgTables.SelectedItem;
            int tableId = (int)selectedTable["ID"];
            string tableName = selectedTable["Name"].ToString();

            // 3. Chuyển sang màn hình chọn khách hàng
            var mainAppWindow = Window.GetWindow(this) as MainAppWindow;
            if (mainAppWindow != null)
            {
                // Xóa nội dung cũ và thêm view mới vào
                mainAppWindow.MainContent.Children.Clear();
                // Lấy thông tin tài khoản đang đăng nhập từ MainAppWindow và truyền vào
                mainAppWindow.MainContent.Children.Add(new SelectCustomerView(tableId, tableName, currentBill, mainAppWindow.LoggedInAccount));
            }
        }
    }
}