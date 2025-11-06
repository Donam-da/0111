using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace namm
{
    /// <summary>
    /// Interaction logic for SelectCustomerView.xaml
    /// </summary>
    public partial class SelectCustomerView : UserControl
    {
        private readonly int _tableId;
        private readonly string _tableName;
        private readonly ObservableCollection<BillItem> _currentBill;
        private readonly decimal _totalAmount;

        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable customerDataTable = new DataTable();
        private DataRowView? _selectedCustomer;

        public SelectCustomerView(int tableId, string tableName, ObservableCollection<BillItem> currentBill)
        {
            InitializeComponent();

            _tableId = tableId;
            _tableName = tableName;
            _currentBill = currentBill;
            _totalAmount = _currentBill.Sum(item => item.TotalPrice);

            this.Loaded += SelectCustomerView_Loaded;
        }

        private async void SelectCustomerView_Loaded(object sender, RoutedEventArgs e)
        {
            tbBillInfo.Text = $"Hóa đơn cho {_tableName} - Tổng cộng: {_totalAmount:N0} VNĐ";
            await LoadCustomersAsync();
            ResetNewCustomerFields();
        }

        private async Task LoadCustomersAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name, PhoneNumber, Address, CustomerCode FROM Customer ORDER BY Name";
                var adapter = new SqlDataAdapter(query, connection);
                customerDataTable = new DataTable();
                customerDataTable.Columns.Add("STT", typeof(int));

                await Task.Run(() => adapter.Fill(customerDataTable));

                dgCustomers.ItemsSource = customerDataTable.DefaultView;
            }
        }

        private void DgCustomers_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgCustomers.SelectedItem is DataRowView selected)
            {
                _selectedCustomer = selected;
                tbSelectedCustomer.Text = $"Đã chọn: {selected["Name"]} (Mã: {selected["CustomerCode"]}) - SĐT: {selected["PhoneNumber"]}";
                btnPay.IsEnabled = true;

                // Điền thông tin vào form để sửa
                txtNewCustomerName.Text = selected["Name"].ToString();
                txtNewCustomerPhone.Text = selected["PhoneNumber"].ToString();
                txtNewCustomerCode.Text = selected["CustomerCode"].ToString();
                txtNewCustomerAddress.Text = selected["Address"].ToString();

                // Cập nhật trạng thái các nút
                btnAddNewCustomer.IsEnabled = false;
                btnEditCustomer.IsEnabled = true;
                btnDeleteCustomer.IsEnabled = true;
            }
            else
            {
                _selectedCustomer = null;
                tbSelectedCustomer.Text = "(Chưa chọn khách hàng)";
                // Nếu không có gì được chọn, các nút Sửa/Xóa sẽ bị vô hiệu hóa trong hàm Reset
                btnPay.IsEnabled = false;
            }
        }

        private void ApplyCustomerFilter()
        {
            string filter = txtSearchCustomer.Text.Replace("'", "''"); // tránh lỗi SQL injection trong filter
            if (customerDataTable.DefaultView != null)
            {
                customerDataTable.DefaultView.RowFilter =
                    $"Name LIKE '%{filter}%' OR PhoneNumber LIKE '%{filter}%' OR CustomerCode LIKE '%{filter}%'";
            }
        }

        // Phương thức này sẽ được gọi khi người dùng nhấn phím trong ô tìm kiếm
        // Chúng ta chỉ áp dụng bộ lọc khi phím Enter được nhấn
        private void TxtSearchCustomer_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {   
                ApplyCustomerFilter();
            }
        }

        private async void TxtNewCustomerName_LostFocus(object sender, RoutedEventArgs e)
        {
            // Chỉ tạo mã mới khi đang ở chế độ "Thêm mới"
            if (btnAddNewCustomer.IsEnabled)
            {
                string baseCode = GenerateCustomerCode(txtNewCustomerName.Text);
                if (string.IsNullOrEmpty(baseCode))
                {
                    txtNewCustomerCode.Text = string.Empty;
                    return;
                }

                string uniqueCode = await GetNextAvailableCustomerCodeAsync(baseCode);
                txtNewCustomerCode.Text = uniqueCode;
            }
        }

        private string GenerateCustomerCode(string customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName))
                return string.Empty;

            string temp = customerName.ToLower().Trim();

            // Bỏ dấu tiếng Việt
            temp = Regex.Replace(temp, "[áàảãạâấầẩẫậăắằẳẵặ]", "a");
            temp = Regex.Replace(temp, "[éèẻẽẹêếềểễệ]", "e");
            temp = Regex.Replace(temp, "[íìỉĩị]", "i");
            temp = Regex.Replace(temp, "[óòỏõọôốồổỗộơớờởỡợ]", "o");
            temp = Regex.Replace(temp, "[úùủũụưứừửữự]", "u");
            temp = Regex.Replace(temp, "[ýỳỷỹỵ]", "y");
            temp = Regex.Replace(temp, "[đ]", "d");

            // Thay thế nhiều khoảng trắng bằng một dấu gạch dưới
            temp = Regex.Replace(temp, @"\s+", "_");

            // Loại bỏ các ký tự không hợp lệ khác
            temp = Regex.Replace(temp, "[^a-z0-9_]", "");

            return temp;
        }

        private async Task<string> GetNextAvailableCustomerCodeAsync(string baseCode)
        {
            var existingCodes = new HashSet<string>();
            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand("SELECT CustomerCode FROM Customer WHERE CustomerCode LIKE @Pattern", connection);
                command.Parameters.AddWithValue("@Pattern", baseCode + "%");

                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        existingCodes.Add(reader.GetString(0));
                    }
                }
            }

            if (!existingCodes.Contains(baseCode)) return baseCode;

            int suffix = 1;
            while (existingCodes.Contains($"{baseCode}_{suffix}")) { suffix++; }
            return $"{baseCode}_{suffix}";
        }

        private async void BtnAddNewCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewCustomerName.Text))
            {
                MessageBox.Show("Tên khách hàng không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNewCustomerCode.Text))
            {
                MessageBox.Show("Mã khách hàng không được để trống. Vui lòng nhập tên và rời khỏi ô để tạo mã.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (var connection = new SqlConnection(connectionString)) // Đổi tên nút từ "Thêm & Chọn" thành "Thêm mới"
            {
                const string query = "INSERT INTO Customer (Name, CustomerCode, PhoneNumber, Address) OUTPUT INSERTED.ID VALUES (@Name, @CustomerCode, @PhoneNumber, @Address)";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Name", txtNewCustomerName.Text);
                command.Parameters.AddWithValue("@CustomerCode", txtNewCustomerCode.Text);
                command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(txtNewCustomerPhone.Text) ? DBNull.Value : (object)txtNewCustomerPhone.Text);
                command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(txtNewCustomerAddress.Text) ? DBNull.Value : (object)txtNewCustomerAddress.Text);

                try
                {
                    await connection.OpenAsync();
                    var newCustomerId = (int)await command.ExecuteScalarAsync();

                    MessageBox.Show("Thêm khách hàng mới thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadCustomersAsync();
                    ResetNewCustomerFields();

                    // Tự động chọn khách hàng vừa thêm
                    dgCustomers.SelectedValue = newCustomerId; 
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi thêm khách hàng: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnEditCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn một khách hàng để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNewCustomerName.Text))
            {
                MessageBox.Show("Tên khách hàng không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Khi sửa, mã khách hàng không thay đổi, chỉ cập nhật các thông tin khác
            // Nếu muốn cho phép sửa cả tên và tạo lại mã, logic sẽ phức tạp hơn
            if (txtNewCustomerName.Text != _selectedCustomer["Name"].ToString())
            {
                if (MessageBox.Show("Bạn đã thay đổi tên khách hàng. Điều này sẽ không thay đổi mã khách hàng hiện tại. Bạn có muốn tiếp tục?", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            int customerId = (int)_selectedCustomer["ID"];

            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "UPDATE Customer SET Name = @Name, PhoneNumber = @PhoneNumber, Address = @Address WHERE ID = @ID"; // Không cập nhật CustomerCode
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", customerId);
                command.Parameters.AddWithValue("@Name", txtNewCustomerName.Text);
                command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(txtNewCustomerPhone.Text) ? DBNull.Value : (object)txtNewCustomerPhone.Text);
                command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(txtNewCustomerAddress.Text) ? DBNull.Value : (object)txtNewCustomerAddress.Text);

                try
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Cập nhật thông tin khách hàng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadCustomersAsync();
                    dgCustomers.SelectedValue = customerId; // Chọn lại khách hàng vừa sửa
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi cập nhật khách hàng: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            int? customerId = null;
            string customerName = "Khách vãng lai";

            // Xác định ID khách hàng dựa trên nút nào được nhấn
            if (sender == btnPay && _selectedCustomer != null)
            {
                customerId = (int)_selectedCustomer["ID"];
                customerName = _selectedCustomer["Name"].ToString();
            }
            else if (sender == btnPayAsGuest)
            {
                // customerId vẫn là null cho khách vãng lai
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một khách hàng để thanh toán.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Xác nhận thanh toán cho '{customerName}'?\nTổng tiền: {_totalAmount:N0} VNĐ", "Xác nhận thanh toán", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            await ProcessPaymentAsync(customerId);
        }

        private async Task ProcessPaymentAsync(int? customerId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                // Tìm hóa đơn chưa thanh toán của bàn (status = 0) và cập nhật nó
                const string updateBillQuery = @"
                    UPDATE Bill 
                    SET Status = 1, -- Đã thanh toán
                        DateCheckOut = GETDATE(),
                        IdCustomer = @CustomerID,
                        TotalAmount = @TotalAmount
                    WHERE TableID = @TableID AND Status = 0";

                var updateBillCmd = new SqlCommand(updateBillQuery, connection);
                updateBillCmd.Parameters.AddWithValue("@CustomerID", customerId ?? (object)DBNull.Value);
                updateBillCmd.Parameters.AddWithValue("@TotalAmount", _totalAmount);
                updateBillCmd.Parameters.AddWithValue("@TableID", _tableId);

                // Cập nhật trạng thái bàn về 'Trống'
                const string updateTableQuery = "UPDATE TableFood SET Status = N'Trống' WHERE ID = @TableID";
                var updateTableCmd = new SqlCommand(updateTableQuery, connection);
                updateTableCmd.Parameters.AddWithValue("@TableID", _tableId);

                await updateBillCmd.ExecuteNonQueryAsync();
                await updateTableCmd.ExecuteNonQueryAsync();
            }

            MessageBox.Show("Thanh toán thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            NavigateBackToDashboard();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            NavigateBackToDashboard();
        }

        private void ResetNewCustomerFields()
        {
            txtNewCustomerName.Clear();
            txtNewCustomerPhone.Clear();
            txtNewCustomerCode.Clear();
            txtNewCustomerAddress.Clear();
            dgCustomers.SelectedItem = null;

            // Đặt lại trạng thái các nút
            btnAddNewCustomer.IsEnabled = true;
            btnEditCustomer.IsEnabled = false;
            btnDeleteCustomer.IsEnabled = false;
        }

        private void NavigateBackToDashboard()
        {
            var mainAppWindow = Window.GetWindow(this) as MainAppWindow;
            if (mainAppWindow != null)
            {
                mainAppWindow.MainContent.Children.Clear();
                mainAppWindow.MainContent.Children.Add(new DashboardView());
            }
        }

        private void BtnResetNewCustomer_Click(object sender, RoutedEventArgs e)
        {
            ResetNewCustomerFields();
        }

        private async void BtnDeleteCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCustomer == null)
            {
                MessageBox.Show("Vui lòng chọn một khách hàng để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string customerName = _selectedCustomer["Name"].ToString();
            if (MessageBox.Show($"Bạn có chắc chắn muốn xóa khách hàng '{customerName}' không? Hành động này không thể hoàn tác.",
                "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }

            int customerId = (int)_selectedCustomer["ID"];

            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();

                var checkBillCmd = new SqlCommand("SELECT COUNT(1) FROM Bill WHERE IdCustomer = @CustomerId", connection);
                checkBillCmd.Parameters.AddWithValue("@CustomerId", customerId);
                int billCount = (int)await checkBillCmd.ExecuteScalarAsync();

                if (billCount > 0)
                {
                    MessageBox.Show($"Không thể xóa khách hàng '{customerName}' vì họ đã có lịch sử giao dịch.",
                        "Không thể xóa", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var deleteCmd = new SqlCommand("DELETE FROM Customer WHERE ID = @ID", connection);
                deleteCmd.Parameters.AddWithValue("@ID", customerId);

                try
                {
                    int rowsAffected = await deleteCmd.ExecuteNonQueryAsync();
                    if (rowsAffected > 0)
                    {
                        MessageBox.Show("Xóa khách hàng thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadCustomersAsync();
                    }
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi xóa khách hàng: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
