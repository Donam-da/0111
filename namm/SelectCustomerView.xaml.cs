using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    /// <summary>
    /// Interaction logic for SelectCustomerView.xaml
    /// </summary>
    public partial class SelectCustomerView : UserControl
    {
        // Dữ liệu được truyền từ DashboardView
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
            // Hiển thị thông tin hóa đơn
            tbBillInfo.Text = $"Hóa đơn cho {_tableName} - Tổng cộng: {_totalAmount:N0} VNĐ";
            await LoadCustomersAsync();
            ResetNewCustomerFields();
        }

        private async Task LoadCustomersAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name, PhoneNumber, Address FROM Customer ORDER BY Name";
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
                tbSelectedCustomer.Text = $"Đã chọn: {selected["Name"]} - SĐT: {selected["PhoneNumber"]}";
                btnPay.IsEnabled = true;
            }
            else
            {
                _selectedCustomer = null;
                tbSelectedCustomer.Text = "(Chưa chọn khách hàng)";
                btnPay.IsEnabled = false;
            }
        }

        private void TxtSearchCustomer_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = txtSearchCustomer.Text;
            if (customerDataTable.DefaultView != null)
            {
                // Tìm kiếm theo tên hoặc số điện thoại
                customerDataTable.DefaultView.RowFilter = $"Name LIKE '%{filter}%' OR PhoneNumber LIKE '%{filter}%'";
            }
        }

        private async void BtnAddNewCustomer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewCustomerName.Text))
            {
                MessageBox.Show("Tên khách hàng không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "INSERT INTO Customer (Name, PhoneNumber, Address) OUTPUT INSERTED.ID VALUES (@Name, @PhoneNumber, @Address)";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Name", txtNewCustomerName.Text);
                command.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(txtNewCustomerPhone.Text) ? (object)DBNull.Value : txtNewCustomerPhone.Text);
                command.Parameters.AddWithValue("@Address", string.IsNullOrWhiteSpace(txtNewCustomerAddress.Text) ? (object)DBNull.Value : txtNewCustomerAddress.Text);

                try
                {
                    await connection.OpenAsync();
                    // Lấy ID của khách hàng vừa thêm
                    var newCustomerId = (int)await command.ExecuteScalarAsync();

                    MessageBox.Show("Thêm khách hàng mới thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadCustomersAsync(); // Tải lại danh sách

                    // Tự động chọn khách hàng vừa thêm
                    dgCustomers.SelectedValue = newCustomerId;

                    ResetNewCustomerFields();
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi thêm khách hàng: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            int? customerId = _selectedCustomer != null ? (int)_selectedCustomer["ID"] : (int?)null;

            // TODO: Logic xử lý thanh toán và in hóa đơn sẽ được thêm ở đây.
            // Ví dụ:
            // 1. Cập nhật trạng thái hóa đơn trong DB (Bill.Status = 1, Bill.CustomerID = customerId)
            // 2. Cập nhật trạng thái bàn về "Trống"
            // 3. Hiển thị cửa sổ in hóa đơn hoặc thông báo thanh toán thành công.

            MessageBox.Show($"Chuẩn bị thanh toán cho khách hàng ID: {customerId?.ToString() ?? "Khách vãng lai"}\nBàn: {_tableName}\nTổng tiền: {_totalAmount:N0} VNĐ", "Thanh toán (Chức năng đang phát triển)", MessageBoxButton.OK, MessageBoxImage.Information);

            // Sau khi thanh toán thành công, quay về màn hình chính
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
            txtNewCustomerAddress.Clear();
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
    }
}