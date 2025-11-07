using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    /// <summary>
    /// Interaction logic for DeleteHistoryView.xaml
    /// </summary>
    public partial class DeleteHistoryView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public DeleteHistoryView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Mặc định chọn ngày hôm nay để tránh người dùng vô tình xóa ngay lập tức
            dpDate.SelectedDate = DateTime.Today;
            tbResult.Text = "";
            UpdateDateRangeInfo();
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!dpDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Vui lòng chọn một ngày.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
                startDate = selectedDate;
                endDate = startDate.AddDays(1);
                confirmationMessage = $"Bạn có thực sự chắc chắn muốn xóa TẤT CẢ hóa đơn trong ngày {startDate:dd/MM/yyyy} không?";
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

                    if (endDate.HasValue) // Các trường hợp Trong ngày, Tuần, Tháng
                    {
                        queryCondition = "Status = 1 AND DateCheckOut >= @StartDate AND DateCheckOut < @EndDate";
                        parameters.Add(new SqlParameter("@StartDate", startDate));
                        parameters.Add(new SqlParameter("@EndDate", endDate.Value));
                    }
                    else // Trường hợp Trước ngày
                    {
                        queryCondition = "Status = 1 AND DateCheckOut < @StartDate";
                        parameters.Add(new SqlParameter("@StartDate", startDate));
                    }

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
                            var cmdBill = new SqlCommand(deleteBillQuery, connection, transaction);
                            // Cần tạo lại parameters vì chúng đã được sử dụng ở command trước
                            var parametersForBill = new List<SqlParameter>();
                            if (endDate.HasValue)
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

            if (rbBeforeDate.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn TRƯỚC ngày:";
            }
            else if (rbOnDate.IsChecked == true)
            {
                tbDateSelectionLabel.Text = "Xóa tất cả hóa đơn TRONG ngày:";
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

        private void DpDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDateRangeInfo();
        }

        private void UpdateDateRangeInfo()
        {
            if (!this.IsLoaded || !dpDate.SelectedDate.HasValue) return;

            DateTime selectedDate = dpDate.SelectedDate.Value.Date;
            tbDateRangeInfo.Visibility = Visibility.Visible;

            if (rbInWeek.IsChecked == true)
            {
                DayOfWeek firstDayOfWeek = DayOfWeek.Monday;
                DateTime startDate = selectedDate.AddDays(-(int)selectedDate.DayOfWeek + (int)firstDayOfWeek);
                if (selectedDate.DayOfWeek < firstDayOfWeek) startDate = startDate.AddDays(-7);
                DateTime endDate = startDate.AddDays(6);
                tbDateRangeInfo.Text = $"(Tức là từ {startDate:dd/MM/yyyy} đến {endDate:dd/MM/yyyy})";
            }
            else { tbDateRangeInfo.Visibility = Visibility.Collapsed; }
        }
    }
}