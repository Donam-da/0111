using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace namm
{
    /// <summary>
    /// Lớp để lưu trữ thông tin ngày và số hóa đơn tương ứng
    /// </summary>
    public class DateInvoiceCount
    {
        public DateTime Date { get; set; }
        public int InvoiceCount { get; set; }
    }
    /// <summary>
    /// Interaction logic for DeleteHistoryView.xaml
    /// </summary>
    public partial class DeleteHistoryView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private readonly ObservableCollection<DateInvoiceCount> _selectedDatesDetails = new ObservableCollection<DateInvoiceCount>();

        public DeleteHistoryView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Mặc định chọn ngày hôm nay để tránh người dùng vô tình xóa ngay lập tức
            dpDate.SelectedDate = DateTime.Today;
            // lvSelectedDates.ItemsSource = _selectedDatesDetails; // Không còn dùng
            tbResult.Text = "";
            await LoadCustomersAsync();
            // UpdateDateRangeInfo(); // Sẽ được gọi trong DeleteMode_Changed
            // Gọi lần đầu để tải dữ liệu cho chế độ mặc định
            DeleteMode_Changed(null, null);
        }
        
        private void HeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = (sender as CheckBox)?.IsChecked ?? false;
            if (dgInvoicesToDelete.ItemsSource is DataView dataView)
            {
                foreach (DataRowView rowView in dataView)
                    rowView["IsSelected"] = isChecked;
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra chung cho các chế độ dùng DatePicker
            if (rbOnDate.IsChecked != true && rbByCustomer.IsChecked != true && !dpDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Vui lòng chọn một ngày.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Kiểm tra riêng cho chế độ chọn nhiều ngày
            if (rbOnDate.IsChecked == true && calendarMultiSelect.SelectedDates.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một ngày trên lịch.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            // Kiểm tra riêng cho chế độ khách hàng
            if (rbByCustomer.IsChecked == true && cbCustomers.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một khách hàng.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Lấy danh sách ID các hóa đơn được chọn để xóa
            var selectedInvoiceIds = (dgInvoicesToDelete.ItemsSource as DataView)?
                .Cast<DataRowView>()
                .Where(row => (bool)row["IsSelected"])
                .Select(row => (int)row["ID"])
                .ToList() ?? new List<int>();

            if (!selectedInvoiceIds.Any())
            {
                MessageBox.Show("Vui lòng chọn ít nhất một hóa đơn để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DateTime selectedDate = dpDate.SelectedDate.Value.Date;
            DateTime startDate;
            DateTime? endDate = null; // Nullable để xử lý trường hợp "Trước ngày"
            string confirmationMessage;

            // Xác định khoảng thời gian và thông báo xác nhận dựa trên chế độ được chọn
            if (rbBeforeDate.IsChecked == true)
            {
                startDate = selectedDate;
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn trước ngày {startDate:dd/MM/yyyy} không?";
            }
            else if (rbOnDate.IsChecked == true)
            {
                var selectedDates = calendarMultiSelect.SelectedDates;
                string datesString = string.Join(", ", selectedDates.Select(d => d.ToString("dd/MM/yyyy")));
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn trong các ngày đã chọn không?";
                startDate = DateTime.MinValue; // Không dùng trong trường hợp này
            }
            else if (rbInWeek.IsChecked == true)
            {
                DayOfWeek firstDayOfWeek = DayOfWeek.Monday;
                startDate = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + (int)firstDayOfWeek);
                if (selectedDate.DayOfWeek < firstDayOfWeek) startDate = startDate.AddDays(-7);
                endDate = startDate.AddDays(7);
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn trong tuần từ {startDate:dd/MM/yyyy} đến {endDate.Value.AddDays(-1):dd/MM/yyyy} không?";
            }
            else if (rbInMonth.IsChecked == true)
            {
                startDate = new DateTime(selectedDate.Year, selectedDate.Month, 1);
                endDate = startDate.AddMonths(1);
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn trong tháng {startDate:MM/yyyy} không?";
            }
            else // rbByCustomer
            {
                var selectedCustomer = (DataRowView)cbCustomers.SelectedItem;
                string customerName = selectedCustomer["Name"].ToString();
                confirmationMessage = $"Bạn có chắc chắn muốn xóa {selectedInvoiceIds.Count} hóa đơn đã chọn của khách hàng '{customerName}' không?";
                startDate = DateTime.MinValue; // Không dùng
            }

            // Hiển thị hộp thoại xác nhận cuối cùng
            var result = MessageBox.Show(
                $"{confirmationMessage}\n\nHÀNH ĐỘNG NÀY KHÔNG THỂ HOÀN TÁC!",
                "XÁC NHẬN XÓA VĨNH VIỄN",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No); // Mặc định là No

            if (result == MessageBoxResult.Yes)
            {
                tbResult.Text = "Đang xử lý, vui lòng chờ..."; // Cập nhật trạng thái
                btnDelete.IsEnabled = false;

                try
                {
                    int billsDeleted = 0;
                    int billInfosDeleted = 0;
                    
                    // Tạo danh sách tham số cho câu lệnh IN
                    var idParameters = selectedInvoiceIds.Select((id, index) => new SqlParameter($"@id{index}", id)).ToList();
                    var idParamNames = string.Join(", ", idParameters.Select(p => p.ParameterName));
                    string queryCondition = $"ID IN ({idParamNames})";

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        using (var transaction = connection.BeginTransaction())
                        {
                            // Xóa BillInfo trước, sử dụng cùng điều kiện
                            string deleteBillInfoQuery = $"DELETE FROM BillInfo WHERE BillID IN ({idParamNames})";
                            var cmdBillInfo = new SqlCommand(deleteBillInfoQuery, connection, transaction);
                            cmdBillInfo.Parameters.AddRange(idParameters.ToArray());
                            billInfosDeleted = await cmdBillInfo.ExecuteNonQueryAsync();

                            // Xóa Bill sau
                            string deleteBillQuery = $"DELETE FROM Bill WHERE {queryCondition}";
                            var cmdBill = new SqlCommand(deleteBillQuery, connection, transaction);
                            // Tạo lại tham số vì chúng đã được dùng ở command trước
                            var idParametersForBill = selectedInvoiceIds.Select((id, index) => new SqlParameter($"@id{index}", id)).ToList();
                            cmdBill.Parameters.AddRange(idParametersForBill.ToArray());
                            billsDeleted = await cmdBill.ExecuteNonQueryAsync();

                            // Nếu mọi thứ thành công, commit transaction
                            transaction.Commit();
                        }
                    }

                    tbResult.Text = $"Hoàn tất! Đã xóa thành công {billsDeleted} hóa đơn và {billInfosDeleted} chi tiết món.";
                }
                catch (Exception ex)
                {
                    tbResult.Text = "Đã xảy ra lỗi trong quá trình xóa.";
                    MessageBox.Show($"Lỗi khi xóa dữ liệu: {ex.Message}", "Lỗi nghiêm trọng", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    btnDelete.IsEnabled = true;
                    // Tải lại danh sách hóa đơn sau khi xóa
                    if (rbOnDate.IsChecked == true) 
                        await UpdateSelectedDatesDetailsAsync();
                    else if (rbByCustomer.IsChecked == true) 
                        await UpdateCustomerInvoiceInfo();
                    else await UpdateDateRangeInfo();
                }
            }
        }

        private async void DeleteMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded) return;

            // Ẩn/hiện các control chọn ngày
            dpDate.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Collapsed : Visibility.Visible;
            calendarMultiSelect.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            cbCustomers.Visibility = (rbByCustomer.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            
            // Các control cũ không còn dùng
            // borderTotal.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            // lvSelectedDates.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            // Cập nhật nhãn
            if (rbBeforeDate.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn TRƯỚC ngày:";
            }
            else if (rbOnDate.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn trong CÁC NGÀY đã chọn:";
                await UpdateSelectedDatesDetailsAsync();
            }
            else if (rbInWeek.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn trong TUẦN chứa ngày:";
            }
            else // rbInMonth
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn trong THÁNG chứa ngày:";
            }

            if (rbByCustomer.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn của khách hàng:";
                dpDate.Visibility = Visibility.Collapsed;
                calendarMultiSelect.Visibility = Visibility.Collapsed;
                await UpdateCustomerInvoiceInfo();
                return; // Dừng ở đây cho chế độ khách hàng
            }
            // Tải lại thông tin và danh sách hóa đơn
            await UpdateDateRangeInfo();
        }

        private async void DpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {            // Khi thay đổi ngày, cũng cần cập nhật thông tin hóa đơn nếu đang ở chế độ tuần/tháng
            await UpdateDateRangeInfo();
        }

        // Đổi tên và sửa lại để trả về Task, vì có thể gọi DB
        private async Task UpdateDateRangeInfo()
        {
            if (!this.IsLoaded || !dpDate.SelectedDate.HasValue || rbOnDate.IsChecked == true || rbByCustomer.IsChecked == true)
            {
                tbDateRangeInfo.Visibility = Visibility.Collapsed;
                return;
            }

            // Dọn dẹp trước khi tải mới
            dgInvoicesToDelete.ItemsSource = null;
            invoicePreview.Clear();

            if (!dpDate.SelectedDate.HasValue)
            {
                tbDateRangeInfo.Visibility = Visibility.Collapsed;
                return;
            }

            DateTime selectedDate = dpDate.SelectedDate.Value.Date;
            tbDateRangeInfo.Visibility = Visibility.Visible;
            int invoiceCount;

            if (rbBeforeDate.IsChecked == true)
            {
                var invoices = await GetInvoicesBeforeDateAsync(selectedDate);
                invoiceCount = invoices.Rows.Count;
                dgInvoicesToDelete.ItemsSource = invoices.DefaultView;
                tbDateRangeInfo.Text = $"(Sẽ xóa {invoiceCount} hóa đơn)";
            }
            else if (rbInWeek.IsChecked == true)
            {
                DayOfWeek firstDayOfWeek = DayOfWeek.Monday;
                DateTime startDate = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + (int)firstDayOfWeek);
                if (selectedDate.DayOfWeek < firstDayOfWeek) startDate = startDate.AddDays(-7);
                DateTime endDate = startDate.AddDays(7);
                var invoices = await GetInvoicesForDateRangeAsync(startDate, endDate);
                invoiceCount = invoices.Rows.Count;
                dgInvoicesToDelete.ItemsSource = invoices.DefaultView;
                tbDateRangeInfo.Text = $"(Từ {startDate:dd/MM/yyyy} đến {endDate.AddDays(-1):dd/MM/yyyy} - có {invoiceCount} hóa đơn)";
            }
            else if (rbInMonth.IsChecked == true)
            {
                DateTime startDate = new DateTime(selectedDate.Year, selectedDate.Month, 1);
                DateTime endDate = startDate.AddMonths(1);
                var invoices = await GetInvoicesForDateRangeAsync(startDate, endDate);
                invoiceCount = invoices.Rows.Count;
                dgInvoicesToDelete.ItemsSource = invoices.DefaultView;
                tbDateRangeInfo.Text = $"(Trong tháng {startDate:MM/yyyy} - có {invoiceCount} hóa đơn)";
            }
            else 
            { 
                tbDateRangeInfo.Visibility = Visibility.Collapsed; 
            }
        }

        private async void CalendarMultiSelect_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            // Khi danh sách ngày chọn thay đổi, cập nhật lại bảng chi tiết
            await UpdateSelectedDatesDetailsAsync();
        }

        private async void CbCustomers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateCustomerInvoiceInfo();
        }

        private async Task UpdateCustomerInvoiceInfo()
        {
            if (!this.IsLoaded || cbCustomers.SelectedItem == null)
            {
                tbDateRangeInfo.Visibility = Visibility.Collapsed;
                dgInvoicesToDelete.ItemsSource = null;
                invoicePreview.Clear();
                return;
            }

            // Dọn dẹp trước khi tải mới
            dgInvoicesToDelete.ItemsSource = null;
            invoicePreview.Clear();

            var selectedCustomer = (DataRowView)cbCustomers.SelectedItem;
            int customerId = (int)selectedCustomer["ID"];
            string customerName = selectedCustomer["Name"].ToString();

            var invoices = await GetInvoicesForCustomerAsync(customerId);
            int invoiceCount = invoices.Rows.Count;

            dgInvoicesToDelete.ItemsSource = invoices.DefaultView;
            tbDateRangeInfo.Visibility = Visibility.Visible;
            tbDateRangeInfo.Text = $"(Khách hàng '{customerName}' có {invoiceCount} hóa đơn)";
        }


        private async Task UpdateSelectedDatesDetailsAsync()
        {
            if (!this.IsLoaded) return;

            // Dọn dẹp
            dgInvoicesToDelete.ItemsSource = null;
            invoicePreview.Clear();
            // _selectedDatesDetails.Clear(); // Không còn dùng

            var selectedDates = calendarMultiSelect.SelectedDates.OrderBy(d => d.Date).ToList();

            var invoices = await GetInvoicesForMultipleDatesAsync(selectedDates);
            dgInvoicesToDelete.ItemsSource = invoices.DefaultView;

            // Tính và hiển thị tổng số hóa đơn
            int totalInvoices = invoices.Rows.Count;
            tbDateRangeInfo.Visibility = Visibility.Visible;
            tbDateRangeInfo.Text = $"(Sẽ xóa {totalInvoices} hóa đơn từ {selectedDates.Count} ngày đã chọn)";

            // tbTotalInvoiceCount.Text = $"{totalInvoices} hóa đơn"; // Không còn dùng
        }

        private async Task<int> GetInvoiceCountForDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            string query = "SELECT COUNT(ID) FROM Bill WHERE Status = 1 AND DateCheckOut >= @StartDate AND DateCheckOut < @EndDate";
            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand(query, connection);
                command.Parameters.Add(new SqlParameter("@StartDate", startDate));
                command.Parameters.Add(new SqlParameter("@EndDate", endDate));
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            }
        }

        private async Task<int> GetInvoiceCountForSingleDateAsync(DateTime date)
        {
            return await GetInvoiceCountForDateRangeAsync(date.Date, date.Date.AddDays(1));
        }

        private async Task<int> GetInvoiceCountBeforeDateAsync(DateTime date)
        {
            string query = "SELECT COUNT(ID) FROM Bill WHERE Status = 1 AND DateCheckOut < @EndDate";
            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand(query, connection);
                command.Parameters.Add(new SqlParameter("@EndDate", date));
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            }
        }

        #region Data Access for Invoice Lists

        private async Task<DataTable> GetInvoicesForDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await GetInvoicesByConditionAsync("b.Status = 1 AND b.DateCheckOut >= @StartDate AND b.DateCheckOut < @EndDate",
                new SqlParameter("@StartDate", startDate), new SqlParameter("@EndDate", endDate));
        }

        private async Task<DataTable> GetInvoicesBeforeDateAsync(DateTime date)
        {
            return await GetInvoicesByConditionAsync("b.Status = 1 AND b.DateCheckOut < @EndDate",
                new SqlParameter("@EndDate", date));
        }

        private async Task<DataTable> GetInvoicesForMultipleDatesAsync(IEnumerable<DateTime> dates)
        {
            if (!dates.Any()) return new DataTable();

            var dateParams = dates.Select((d, i) => new SqlParameter($"@p{i}", d.Date)).ToList();
            var paramNames = string.Join(", ", dateParams.Select(p => p.ParameterName));
            string condition = $"b.Status = 1 AND CAST(b.DateCheckOut AS DATE) IN ({paramNames})";

            return await GetInvoicesByConditionAsync(condition, dateParams.ToArray());
        }

        private async Task<DataTable> GetInvoicesForCustomerAsync(int customerId)
        {
            return await GetInvoicesByConditionAsync("b.Status = 1 AND b.IdCustomer = @CustomerId",
                new SqlParameter("@CustomerId", customerId));
        }

        private async Task<DataTable> GetInvoicesByConditionAsync(string condition, params SqlParameter[] parameters)
        {
            var dt = new DataTable();
            string query = $@"
                SELECT 
                    b.ID, 
                    ISNULL(c.CustomerCode, b.GuestCustomerCode) as CustomerCode,
                    b.DateCheckOut, 
                    ISNULL(c.Name, N'Khách vãng lai') AS CustomerName, 
                    tf.Name AS TableName, 
                    b.TotalAmount,
                    b.SubTotal
                FROM Bill b
                LEFT JOIN Customer c ON b.IdCustomer = c.ID
                JOIN TableFood tf ON b.TableID = tf.ID
                WHERE {condition}
                ORDER BY b.DateCheckOut DESC";

            using (var connection = new SqlConnection(connectionString))
            {
                var adapter = new SqlDataAdapter(query, connection);
                if (parameters != null && parameters.Length > 0)
                {
                    adapter.SelectCommand.Parameters.AddRange(parameters);
                }

                // Thêm cột IsSelected vào DataTable và đặt giá trị mặc định là true
                if (!dt.Columns.Contains("IsSelected"))
                {
                    dt.Columns.Add("IsSelected", typeof(bool)).DefaultValue = true;
                }

                await Task.Run(() => adapter.Fill(dt));
            }
            return dt;
        }

        private async Task LoadCustomersAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name FROM Customer ORDER BY Name";
                var adapter = new SqlDataAdapter(query, connection);
                var customerTable = new DataTable();
                await Task.Run(() => adapter.Fill(customerTable));
                cbCustomers.ItemsSource = customerTable.DefaultView;
            }
        }

        private async Task<DataView> LoadInvoiceDetailsAsync(int billId)
        {
            const string query = @"
                SELECT d.Name + N' (' + bi.DrinkType + N')' AS DrinkName, bi.Quantity, bi.Price, (bi.Quantity * bi.Price) AS TotalPrice
                FROM BillInfo bi
                JOIN Drink d ON bi.DrinkID = d.ID
                WHERE bi.BillID = @BillID";

            var adapter = new SqlDataAdapter(query, connectionString);
            adapter.SelectCommand.Parameters.AddWithValue("@BillID", billId);
            var detailsTable = new DataTable();
            await Task.Run(() => adapter.Fill(detailsTable));
            return detailsTable.DefaultView;
        }
        #endregion

        private async Task<int> GetInvoiceCountForMultipleDatesAsync(IEnumerable<DateTime> dates)
        {
            if (!dates.Any()) return 0;

            var dateParams = dates.Select((d, i) => new SqlParameter($"@p{i}", d.Date)).ToList();
            var paramNames = string.Join(", ", dateParams.Select(p => p.ParameterName));
            string query = $"SELECT COUNT(ID) FROM Bill WHERE Status = 1 AND CAST(DateCheckOut AS DATE) IN ({paramNames})";

            using (var connection = new SqlConnection(connectionString))
            {
                var command = new SqlCommand(query, connection);
                command.Parameters.AddRange(dateParams.ToArray());
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                return (result != null && result != DBNull.Value) ? Convert.ToInt32(result) : 0;
            }
        }

        private void CalendarMultiSelect_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Tìm nút ngày được nhấp vào
            if (e.OriginalSource is FrameworkElement originalSource)
            {
                var dayButton = FindVisualParent<CalendarDayButton>(originalSource);
                if (dayButton != null && dayButton.DataContext is DateTime clickedDate)
                {
                    e.Handled = true; // Ngăn chặn hành vi chọn mặc định

                    // Tự quản lý việc chọn/bỏ chọn
                    if (calendarMultiSelect.SelectedDates.Contains(clickedDate))
                        calendarMultiSelect.SelectedDates.Remove(clickedDate);
                    else
                        calendarMultiSelect.SelectedDates.Add(clickedDate);
                    // Sự kiện SelectedDatesChanged sẽ được kích hoạt và tự động gọi UpdateSelectedDatesDetailsAsync()
                }
            }
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        private async void PreviewInvoice_Click(object sender, RoutedEventArgs e)
        {
            // Lấy dữ liệu của hàng từ CommandParameter của nút
            if ((sender as Button)?.CommandParameter is DataRowView selectedInvoice)
            {
                int billId = (int)selectedInvoice["ID"];
                var detailsView = await LoadInvoiceDetailsAsync(billId);
                invoicePreview.DisplayInvoice(selectedInvoice, detailsView);
            }
        }
    }
}