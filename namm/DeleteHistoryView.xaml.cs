using System;
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
            dpDeleteBeforeDate.SelectedDate = DateTime.Today;
            tbResult.Text = "";
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (!dpDeleteBeforeDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Vui lòng chọn một ngày.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            DateTime deleteBeforeDate = dpDeleteBeforeDate.SelectedDate.Value.Date;

            // Hiển thị hộp thoại xác nhận cuối cùng
            var result = MessageBox.Show(
                $"Bạn có thực sự chắc chắn muốn xóa TẤT CẢ hóa đơn trước ngày {deleteBeforeDate:dd/MM/yyyy} không?\n\nHÀNH ĐỘNG NÀY KHÔNG THỂ HOÀN TÁC!",
                "XÁC NHẬN XÓA VĨNH VIỄN",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No); // Mặc định là No

            if (result == MessageBoxResult.Yes)
            {
                tbResult.Text = "Đang xử lý, vui lòng chờ...";
                btnDelete.IsEnabled = false;

                try
                {
                    int billsDeleted = 0;
                    int billInfosDeleted = 0;

                    using (var connection = new SqlConnection(connectionString))
                    {
                        await connection.OpenAsync();
                        using (var transaction = connection.BeginTransaction())
                        {
                            // Câu lệnh để xóa các chi tiết hóa đơn (BillInfo) trước
                            string deleteBillInfoQuery = @"
                                DELETE FROM BillInfo 
                                WHERE BillID IN (SELECT ID FROM Bill WHERE Status = 1 AND DateCheckOut < @DeleteBeforeDate)";
                            var cmdBillInfo = new SqlCommand(deleteBillInfoQuery, connection, transaction);
                            cmdBillInfo.Parameters.AddWithValue("@DeleteBeforeDate", deleteBeforeDate);
                            billInfosDeleted = await cmdBillInfo.ExecuteNonQueryAsync();

                            // Câu lệnh để xóa các hóa đơn (Bill) sau
                            string deleteBillQuery = "DELETE FROM Bill WHERE Status = 1 AND DateCheckOut < @DeleteBeforeDate";
                            var cmdBill = new SqlCommand(deleteBillQuery, connection, transaction);
                            cmdBill.Parameters.AddWithValue("@DeleteBeforeDate", deleteBeforeDate);
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
    }
}