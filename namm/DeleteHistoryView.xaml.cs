using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Mặc định chọn ngày hôm nay để tránh người dùng vô tình xóa ngay lập tức
            dpDate.SelectedDate = DateTime.Today;
            lvSelectedDates.ItemsSource = _selectedDatesDetails;
            tbResult.Text = "";
            UpdateDateRangeInfo();
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            // Kiểm tra chung cho các chế độ dùng DatePicker
            if (rbOnDate.IsChecked != true && !dpDate.SelectedDate.HasValue)
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
            DateTime selectedDate = dpDate.SelectedDate.Value.Date;
            DateTime startDate;
            DateTime? endDate = null; // Nullable để xử lý trường hợp "Trước ngày"
            string confirmationMessage;

            // Xác định khoảng thời gian và thông báo xác nhận dựa trên chế độ được chọn
            if (rbBeforeDate.IsChecked == true)
            {
                startDate = selectedDate;
                confirmationMessage = $"Bạn có thực sự chắc chắn muốn xóa TẤT CẢ hóa đơn trước ngày {startDate:dd/MM/yyyy} không?";
            }
            else if (rbOnDate.IsChecked == true)
            {
                var selectedDates = calendarMultiSelect.SelectedDates;
                string datesString = string.Join(", ", selectedDates.Select(d => d.ToString("dd/MM/yyyy")));
                confirmationMessage = $"Bạn có thực sự chắc chắn muốn xóa TẤT CẢ hóa đơn trong {selectedDates.Count} ngày đã chọn:\n{datesString} không?";
                startDate = DateTime.MinValue; // Không dùng trong trường hợp này
            }
            else if (rbInWeek.IsChecked == true)
            {
                DayOfWeek firstDayOfWeek = DayOfWeek.Monday;
                startDate = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + (int)firstDayOfWeek);
                if (selectedDate.DayOfWeek < firstDayOfWeek) startDate = startDate.AddDays(-7);
                endDate = startDate.AddDays(7);
                confirmationMessage = $"Bạn có thực sự chắc chắn muốn xóa TẤT CẢ hóa đơn trong tuần từ {startDate:dd/MM/yyyy} đến {endDate.Value.AddDays(-1):dd/MM/yyyy} không?";
            }
            else // rbInMonth
            {
                startDate = new DateTime(selectedDate.Year, selectedDate.Month, 1);
                endDate = startDate.AddMonths(1);
                confirmationMessage = $"Bạn có thực sự chắc chắn muốn xóa TẤT CẢ hóa đơn trong tháng {startDate:MM/yyyy} không?";
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
                    string queryCondition;
                    var parameters = new List<SqlParameter>();

                    if (rbOnDate.IsChecked == true) // Chế độ chọn nhiều ngày
                    {
                        var dateParams = calendarMultiSelect.SelectedDates
                            .Select((d, i) => new SqlParameter($"@p{i}", d.Date))
                            .ToList();
                        var paramNames = string.Join(", ", dateParams.Select(p => p.ParameterName));

                        queryCondition = $"Status = 1 AND CAST(DateCheckOut AS DATE) IN ({paramNames})";
                        parameters.AddRange(dateParams);
                    }
                    else if (endDate.HasValue) // Các trường hợp Tuần, Tháng
                    {
                        queryCondition = "Status = 1 AND DateCheckOut >= @StartDate AND DateCheckOut < @EndDate";
                        parameters.Add(new SqlParameter("@StartDate", startDate));
                        parameters.Add(new SqlParameter("@EndDate", endDate.Value));
                    }
                    else // Trường hợp "Trước ngày"
                    {
                        queryCondition = "Status = 1 AND DateCheckOut < @StartDate";
                        parameters.Add(new SqlParameter("@StartDate", startDate));
                    }

                    if (string.IsNullOrEmpty(queryCondition)) return; // An toàn

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        using (var transaction = connection.BeginTransaction())
                        {
                            // Xóa BillInfo trước
                            string deleteBillInfoQuery = $"DELETE FROM BillInfo WHERE BillID IN (SELECT ID FROM Bill WHERE {queryCondition})";
                            var cmdBillInfo = new SqlCommand(deleteBillInfoQuery, connection, transaction);
                            cmdBillInfo.Parameters.AddRange(parameters.ToArray());
                            billInfosDeleted = await cmdBillInfo.ExecuteNonQueryAsync();

                            // Xóa Bill sau
                            string deleteBillQuery = $"DELETE FROM Bill WHERE {queryCondition}";
                            using (var cmdBill = new SqlCommand(deleteBillQuery, connection, transaction))
                            {
                                // Cần tạo lại parameters vì chúng đã được sử dụng ở command trước
                                var parametersForBill = new List<SqlParameter>();
                                if (rbOnDate.IsChecked == true)
                                {
                                    parametersForBill.AddRange(calendarMultiSelect.SelectedDates.Select((d, i) => new SqlParameter($"@p{i}", d.Date)));
                                }
                                else if (endDate.HasValue)
                                {
                                    parametersForBill.Add(new SqlParameter("@StartDate", startDate));
                                    parametersForBill.Add(new SqlParameter("@EndDate", endDate.Value));
                                }
                                else
                                {
                                    parametersForBill.Add(new SqlParameter("@StartDate", startDate));
                                }
                                cmdBill.Parameters.AddRange(parametersForBill.ToArray());
                                billsDeleted = await cmdBill.ExecuteNonQueryAsync();
                            }

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
                }
            }
        }

        private void DeleteMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!this.IsLoaded) return;

            // Ẩn/hiện các control chọn ngày
            dpDate.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Collapsed : Visibility.Visible;
            calendarMultiSelect.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            borderTotal.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;
            lvSelectedDates.Visibility = (rbOnDate.IsChecked == true) ? Visibility.Visible : Visibility.Collapsed;

            // Cập nhật nhãn
            if (rbBeforeDate.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn TRƯỚC ngày:";
            }
            else if (rbOnDate.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn trong CÁC NGÀY đã chọn:";
                UpdateSelectedDatesDetailsAsync();
            }
            else if (rbInWeek.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn trong TUẦN chứa ngày:";
            }
            else // rbInMonth
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn trong THÁNG chứa ngày:";
            }
            UpdateDateRangeInfo();
        }

        private async void DpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Khi thay đổi ngày, cũng cần cập nhật thông tin hóa đơn nếu đang ở chế độ tuần/tháng
            await UpdateDateRangeInfo();
        }

        // Đổi tên và sửa lại để trả về Task, vì có thể gọi DB
        private async Task UpdateDateRangeInfo()
        {
            if (!this.IsLoaded || !dpDate.SelectedDate.HasValue || rbOnDate.IsChecked == true) return;

            DateTime selectedDate = dpDate.SelectedDate.Value.Date;
            tbDateRangeInfo.Visibility = Visibility.Visible;

            if (rbInWeek.IsChecked == true)
            {
                DayOfWeek firstDayOfWeek = DayOfWeek.Monday;
                DateTime startDate = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + (int)firstDayOfWeek);
                if (selectedDate.DayOfWeek < firstDayOfWeek) startDate = startDate.AddDays(-7);
                DateTime endDate = startDate.AddDays(6);
                int invoiceCount = await GetInvoiceCountForDateRangeAsync(startDate, endDate.AddDays(1));
                tbDateRangeInfo.Text = $"(Từ {startDate:dd/MM/yyyy} đến {endDate:dd/MM/yyyy} - có {invoiceCount} hóa đơn)";
            }
            else { tbDateRangeInfo.Visibility = Visibility.Collapsed; }
        }

        private async void CalendarMultiSelect_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            // Khi danh sách ngày chọn thay đổi, cập nhật lại bảng chi tiết
            await UpdateSelectedDatesDetailsAsync();
        }

        private async Task UpdateSelectedDatesDetailsAsync()
        {
            if (!this.IsLoaded) return;

            _selectedDatesDetails.Clear();
            var selectedDates = calendarMultiSelect.SelectedDates.OrderBy(d => d.Date).ToList();

            foreach (var date in selectedDates)
            {
                int count = await GetInvoiceCountForSingleDateAsync(date);
                _selectedDatesDetails.Add(new DateInvoiceCount { Date = date, InvoiceCount = count });
            }

            // Tính và hiển thị tổng số hóa đơn
            int totalInvoices = _selectedDatesDetails.Sum(d => d.InvoiceCount);
            tbTotalInvoiceCount.Text = $"{totalInvoices} hóa đơn";
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
    }
}