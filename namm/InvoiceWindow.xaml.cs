using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace namm
{
    public partial class InvoiceWindow : Window
    {
        public InvoiceWindow(int tableId, string tableName, string customerName, string customerCode, decimal subTotal, decimal discountPercent, decimal finalTotal, ObservableCollection<BillItem> billItems, int billId)
        {
            InitializeComponent();

            // Điền thông tin vào hóa đơn
            tbInvoiceId.Text = billId.ToString("D6"); // Định dạng số hóa đơn, ví dụ: 000123
            tbTableName.Text = tableName;
            tbCustomerCode.Text = customerCode;
            tbCustomerName.Text = customerName;
            tbDateTime.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            tbSubTotal.Text = $"{subTotal:N0}";
            tbTotalAmount.Text = $"{finalTotal:N0} VNĐ";

            if (discountPercent > 0)
            {
                decimal discountAmount = subTotal - finalTotal;
                tbDiscountAmount.Text = $"-{discountAmount:N0} ({discountPercent:G29}%)";
                gridDiscount.Visibility = Visibility.Visible;
            }

            // Hiển thị danh sách món
            dgBillItems.ItemsSource = billItems;
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            // Đóng cửa sổ và trả về kết quả là true để xác nhận thanh toán
            this.DialogResult = true;
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            // Chức năng in hóa đơn
            // Đây là một chức năng phức tạp, tạm thời chỉ hiển thị thông báo
            MessageBox.Show("Chức năng in hóa đơn đang được phát triển!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);

            // Nếu bạn muốn triển khai in thật, bạn có thể sử dụng PrintDialog
            // PrintDialog printDialog = new PrintDialog();
            // if (printDialog.ShowDialog() == true)
            // {
            //     printDialog.PrintVisual(this, "In hóa đơn");
            // }
        }
    }
}