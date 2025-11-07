﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.CompilerServices;

namespace namm
{
    public class DiscountRule : INotifyPropertyChanged
    {
        public string CriteriaType { get; set; } = string.Empty; // "Số lần mua" hoặc "Tổng chi tiêu"
        public int ID { get; set; } // Thêm ID để dễ dàng xóa
        public decimal Threshold { get; set; }
        public decimal DiscountPercent { get; set; }

        private bool _isApplied;
        public bool IsAppliedToSelectedCustomer
        {
            get => _isApplied;
            set
            {
                if (_isApplied != value)
                {
                    _isApplied = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public partial class LoyalCustomerView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable customerTable = new DataTable();
        private ObservableCollection<DiscountRule> discountRules = new ObservableCollection<DiscountRule>();
        private int? _activeCustomerId = null; // Lưu ID của khách hàng đang được chọn để sửa

        public LoyalCustomerView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                lvDiscountRules.ItemsSource = discountRules;
                await LoadDiscountRulesAsync(); // Tải các quy tắc đã lưu
                await LoadLoyalCustomersAsync(); // Tải danh sách khách hàng
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Đã xảy ra lỗi khi tải dữ liệu khách hàng: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Đảm bảo khi tải xong, không có quy tắc nào được đánh dấu là áp dụng
            foreach (var rule in discountRules)
            {
                rule.IsAppliedToSelectedCustomer = false;
            }
        }

        private async Task LoadLoyalCustomersAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT
                        c.ID,
                        c.Name AS CustomerName,
                        c.CustomerCode,
                        c.PhoneNumber,
                        c.Address,
                        COUNT(DISTINCT b.ID) AS PurchaseCount,
                        ISNULL(SUM(b.TotalAmount), 0) AS TotalSpent,
                        ISNULL(SUM(b.SubTotal - b.TotalAmount), 0) AS TotalDiscountGiven,
                        -- Lấy thông tin về TẤT CẢ các quy tắc được áp dụng, nối chuỗi lại
                        ISNULL(STRING_AGG(dr.CriteriaType + ' (' + FORMAT(dr.DiscountPercent, 'G29') + '%)', ', '), N'Tự động') AS AppliedRuleDescription
                    FROM Customer c
                    LEFT JOIN Bill b ON c.ID = b.IdCustomer AND b.Status = 1
                    LEFT JOIN CustomerAppliedRule car ON c.ID = car.CustomerID
                    LEFT JOIN DiscountRule dr ON car.DiscountRuleID = dr.ID
                    GROUP BY c.ID, c.Name, c.CustomerCode, c.PhoneNumber, c.Address
                    ORDER BY TotalSpent DESC;
                ";

                var adapter = new SqlDataAdapter(query, connection);
                customerTable = new DataTable();
                customerTable.Columns.Add("STT", typeof(int));
                customerTable.Columns.Add("Discount", typeof(decimal)); // Thêm cột giảm giá

                await Task.Run(() => adapter.Fill(customerTable));

                // Gán giá trị cho cột Discount, STT sẽ được xử lý trong sự kiện LoadingRow
                for (int i = 0; i < customerTable.Rows.Count; i++)
                {
                    customerTable.Rows[i]["Discount"] = customerTable.Rows[i]["TotalDiscountGiven"];
                }

                dgLoyalCustomers.ItemsSource = customerTable.DefaultView;
            }
        }

        private async Task LoadDiscountRulesAsync()
        {
            discountRules.Clear();
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, CriteriaType, Threshold, DiscountPercent FROM DiscountRule ORDER BY CriteriaType, Threshold";
                var command = new SqlCommand(query, connection);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        discountRules.Add(new DiscountRule
                        {
                            ID = reader.GetInt32(0),
                            CriteriaType = reader.GetString(1),
                            Threshold = reader.GetDecimal(2),
                            DiscountPercent = reader.GetDecimal(3)
                        });
                    }
                }
            }
        }

        private async void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            if (cbCriteriaType.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một tiêu chí.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!decimal.TryParse(txtThreshold.Text, out decimal threshold) || threshold <= 0)
            {
                MessageBox.Show("Ngưỡng phải là một số dương hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!decimal.TryParse(txtDiscountPercent.Text, out decimal discountPercent) || discountPercent < 0)
            {
                MessageBox.Show("Mức giảm giá phải là một số không âm hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string criteriaType = ((ComboBoxItem)cbCriteriaType.SelectedItem).Content.ToString();

            // Lưu vào DB
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "INSERT INTO DiscountRule (CriteriaType, Threshold, DiscountPercent) OUTPUT INSERTED.ID VALUES (@CriteriaType, @Threshold, @DiscountPercent)";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CriteriaType", criteriaType);
                command.Parameters.AddWithValue("@Threshold", threshold);
                command.Parameters.AddWithValue("@DiscountPercent", discountPercent);

                try
                {
                    await connection.OpenAsync();
                    int newId = (int)await command.ExecuteScalarAsync();

                    // Thêm vào danh sách trên UI
                    discountRules.Add(new DiscountRule
                    {
                        ID = newId,
                        CriteriaType = criteriaType,
                        Threshold = threshold,
                        DiscountPercent = discountPercent
                    });

                    // Reset input fields
                    cbCriteriaType.SelectedIndex = -1;
                    txtThreshold.Clear();
                    txtDiscountPercent.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi thêm mức giảm giá: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            // Lấy tất cả các quy tắc được chọn
            var selectedRules = lvDiscountRules.SelectedItems.Cast<DiscountRule>().ToList();

            if (selectedRules.Any())
            {
                if (MessageBox.Show($"Bạn có chắc chắn muốn xóa {selectedRules.Count} mức giảm giá đã chọn không?", 
                    "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }

                var idsToDelete = selectedRules.Select(r => r.ID).ToList();
                string idList = string.Join(",", idsToDelete);

                // Xóa khỏi DB
                using (var connection = new SqlConnection(connectionString))
                {
                    // Xóa nhiều mục cùng lúc bằng IN clause
                    var command = new SqlCommand($"DELETE FROM DiscountRule WHERE ID IN ({idList})", connection);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }

                // Xóa khỏi UI (cần duyệt ngược để tránh lỗi khi xóa item khỏi collection)
                foreach (var rule in selectedRules)
                {
                    discountRules.Remove(rule);
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một mức giảm giá để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DgLoyalCustomers_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            // Gán lại số thứ tự dựa trên vị trí hiển thị của hàng trong DataGrid.
            // Điều này đảm bảo STT luôn đúng thứ tự 1, 2, 3,... ngay cả khi sắp xếp.
            if (e.Row.Item is DataRowView rowView)
            {
                rowView.Row["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private async void BtnApplyRuleToSelected_Click(object sender, RoutedEventArgs e)
        {
            // Logic kiểm tra đã được chuyển vào sự kiện SelectionChanged, nên ở đây chỉ cần lấy mục đã chọn
            var selectedRules = lvDiscountRules.SelectedItems.Cast<DiscountRule>().ToList();

            if (dgLoyalCustomers.SelectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một khách hàng để áp dụng mức giảm giá.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCustomerIds = dgLoyalCustomers.SelectedItems.Cast<DataRowView>().Select(row => (int)row["ID"]).ToList();
            string ruleDescription = $"{selectedRules.Count} mức";

            if (MessageBox.Show($"Bạn có chắc chắn muốn áp dụng mức '{ruleDescription}' cho {selectedCustomerIds.Count} khách hàng đã chọn không?",
                "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            await ApplyRulesToCustomers(selectedCustomerIds, selectedRules.Select(r => r.ID).ToList());
        }

        private async void BtnApplyRuleToAll_Click(object sender, RoutedEventArgs e)
        {
            var selectedRules = lvDiscountRules.SelectedItems.Cast<DiscountRule>().ToList();

            string ruleDescription = $"{selectedRules.Count} mức";
            if (MessageBox.Show($"Hành động này sẽ áp dụng mức '{ruleDescription}' cho TẤT CẢ khách hàng trong hệ thống. Bạn có chắc chắn muốn tiếp tục?",
                "CẢNH BÁO", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            var allCustomerIds = customerTable.AsEnumerable().Select(row => row.Field<int>("ID")).ToList();
            await ApplyRulesToCustomers(allCustomerIds, selectedRules.Select(r => r.ID).ToList());
        }

        private void LvDiscountRules_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Bật các nút áp dụng khi có ít nhất 1 mức giảm giá được chọn
            bool anyRuleSelected = lvDiscountRules.SelectedItems.Count > 0;
            btnApplyRuleToSelected.IsEnabled = anyRuleSelected;
            btnApplyRuleToAll.IsEnabled = anyRuleSelected;
            btnRemoveRule.IsEnabled = anyRuleSelected;
        }

        private async Task ApplyRulesToCustomers(List<int> customerIds, List<int> ruleIds)
        {
            if (!customerIds.Any() || !ruleIds.Any()) return;

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    foreach (var customerId in customerIds)
                    {
                        foreach (var ruleId in ruleIds)
                        {
                            // Sử dụng MERGE để thực hiện UPSERT (chỉ INSERT nếu chưa có, không làm gì nếu đã có)
                            // Điều này tránh lỗi khóa chính khi cố gắng thêm một cặp (CustomerID, RuleID) đã tồn tại.
                            const string upsertQuery = @"
                                MERGE CustomerAppliedRule AS target
                                USING (SELECT @CustomerID AS CustomerID, @RuleID AS DiscountRuleID) AS source
                                ON (target.CustomerID = source.CustomerID AND target.DiscountRuleID = source.DiscountRuleID)
                                WHEN NOT MATCHED BY TARGET THEN
                                    INSERT (CustomerID, DiscountRuleID) VALUES (source.CustomerID, source.DiscountRuleID);";

                            var command = new SqlCommand(upsertQuery, connection);
                            command.Parameters.AddWithValue("@CustomerID", customerId);
                            command.Parameters.AddWithValue("@RuleID", ruleId);
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
                MessageBox.Show("Áp dụng mức giảm giá thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadLoyalCustomersAsync(); // Tải lại danh sách để hiển thị thay đổi
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi áp dụng mức giảm giá: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox headerCheckBox)
            {
                bool isChecked = headerCheckBox.IsChecked ?? false;
                foreach (var item in dgLoyalCustomers.Items)
                {
                    if (dgLoyalCustomers.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                        row.IsSelected = isChecked;
                }
            }
        }

        private void RuleHeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox headerCheckBox)
            {
                bool isChecked = headerCheckBox.IsChecked ?? false;
                foreach (var item in lvDiscountRules.Items)
                {
                    if (lvDiscountRules.ItemContainerGenerator.ContainerFromItem(item) is ListViewItem lvi)
                        lvi.IsSelected = isChecked;
                }
            }
        }

        private async void AppliedRule_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBlock textBlock && textBlock.DataContext is DataRowView selectedCustomer)
            {
                _activeCustomerId = (int)textBlock.Tag;
                tbEditingCustomer.Text = selectedCustomer["CustomerName"].ToString();
                btnUpdateForCustomer.IsEnabled = true;
                await HighlightAppliedRulesForCustomer(_activeCustomerId.Value);
            }
        }

        private async void BtnUpdateForCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (_activeCustomerId == null)
            {
                MessageBox.Show("Không có khách hàng nào được chọn để cập nhật.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Lấy danh sách các quy tắc mới được chọn từ ListView
            var newSelectedRuleIds = lvDiscountRules.SelectedItems.Cast<DiscountRule>().Select(r => r.ID).ToList();

            if (MessageBox.Show($"Bạn có chắc chắn muốn cập nhật lại các mức giảm giá cho khách hàng '{tbEditingCustomer.Text}' không?", 
                "Xác nhận cập nhật", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var transaction = connection.BeginTransaction())
                    {
                        // 1. Xóa tất cả các quy tắc cũ của khách hàng này
                        var deleteCmd = new SqlCommand("DELETE FROM CustomerAppliedRule WHERE CustomerID = @CustomerID", connection, transaction);
                        deleteCmd.Parameters.AddWithValue("@CustomerID", _activeCustomerId.Value);
                        await deleteCmd.ExecuteNonQueryAsync();

                        // 2. Thêm lại các quy tắc mới được chọn
                        foreach (var ruleId in newSelectedRuleIds)
                        {
                            var insertCmd = new SqlCommand("INSERT INTO CustomerAppliedRule (CustomerID, DiscountRuleID) VALUES (@CustomerID, @RuleID)", connection, transaction);
                            insertCmd.Parameters.AddWithValue("@CustomerID", _activeCustomerId.Value);
                            insertCmd.Parameters.AddWithValue("@RuleID", ruleId);
                            await insertCmd.ExecuteNonQueryAsync();
                        }

                        transaction.Commit();
                    }
                }

                MessageBox.Show("Cập nhật thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                await LoadLoyalCustomersAsync(); // Tải lại bảng khách hàng để cập nhật cột "Mức áp dụng"

                // Tải lại phần hiển thị "Đã áp dụng" cho khách hàng vừa cập nhật
                await HighlightAppliedRulesForCustomer(_activeCustomerId.Value);

                // Reset trạng thái sau khi cập nhật thành công
                _activeCustomerId = null;
                tbEditingCustomer.Text = "(Chưa chọn)";
                btnUpdateForCustomer.IsEnabled = false;
                lvDiscountRules.SelectedItems.Clear(); // Bỏ chọn tất cả các ô tích

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi cập nhật: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task HighlightAppliedRulesForCustomer(int customerId)
        {
            // Lấy danh sách ID các quy tắc đã được gán cho khách hàng này
            var appliedRuleIds = new HashSet<int>();
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT DiscountRuleID
                    FROM CustomerAppliedRule car
                    WHERE car.CustomerID = @CustomerId";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CustomerId", customerId);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while(await reader.ReadAsync())
                    {
                        appliedRuleIds.Add(reader.GetInt32(0));
                    }
                }
            }

            // Cập nhật trạng thái 'IsAppliedToSelectedCustomer' cho từng quy tắc trong danh sách
            foreach (var rule in discountRules)
            {
                rule.IsAppliedToSelectedCustomer = appliedRuleIds.Contains(rule.ID);
            }
        }

        private void DataGridRow_PreviewMouseLeftButtonDown_Selection(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is DataGridRow row)
            {
                // Tìm xem người dùng có nhấp vào TextBlock của cột "Mức áp dụng" không
                var textBlock = e.OriginalSource as TextBlock;
                bool isAppliedRuleColumn = textBlock != null && textBlock.Cursor == Cursors.Help;

                // Nếu người dùng nhấp vào một nơi khác ngoài CheckBox và ngoài cột "Mức áp dụng"
                if (!(e.OriginalSource is CheckBox) && !isAppliedRuleColumn)
                {
                    // Đảo ngược trạng thái lựa chọn hiện tại của hàng
                    row.IsSelected = !row.IsSelected;

                    // Đánh dấu sự kiện đã được xử lý để ngăn DataGrid
                    // thực hiện hành vi lựa chọn mặc định của nó (ví dụ: bỏ chọn các hàng khác).
                    e.Handled = true;
                }
                // Nếu người dùng nhấp vào CheckBox hoặc cột "Mức áp dụng", chúng ta không làm gì cả
                // và để các sự kiện mặc định hoặc sự kiện riêng của chúng tự xử lý.
            }
        }
    }
}