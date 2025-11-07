using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class InvoicePreviewControl : UserControl
    {
        public InvoicePreviewControl()
        {
            InitializeComponent();
        }

        public void DisplayInvoice(DataRowView invoiceData, DataView detailsView)
        {
            if (invoiceData == null || detailsView == null)
            {
                Clear();
                return;
            }

            this.Visibility = Visibility.Visible;

            tbInvoiceId.Text = ((int)invoiceData["ID"]).ToString("D6");
            tbTableName.Text = invoiceData["TableName"].ToString();
            tbCustomerName.Text = invoiceData["CustomerName"].ToString();
            tbCustomerCode.Text = invoiceData["CustomerCode"]?.ToString() ?? "N/A"; // Sử dụng ?? để xử lý DBNull
            tbDateTime.Text = ((DateTime)invoiceData["DateCheckOut"]).ToString("dd/MM/yyyy HH:mm");

            // Hiển thị danh sách món
            dgBillItems.ItemsSource = detailsView;

            // Xử lý hiển thị giảm giá
            decimal totalAmount = (decimal)invoiceData["TotalAmount"];
            // Lấy SubTotal, nếu là null (hóa đơn cũ không có) thì mặc định bằng TotalAmount
            decimal subTotal = (invoiceData["SubTotal"] != DBNull.Value) ? (decimal)invoiceData["SubTotal"] : totalAmount;

            tbSubTotal.Text = $"{subTotal:N0}";
            tbTotalAmount.Text = $"{totalAmount:N0} VNĐ";

            if (subTotal > totalAmount && subTotal > 0) // Thêm điều kiện subTotal > 0 để tránh lỗi chia cho 0
            {
                decimal discountAmount = subTotal - totalAmount;
                decimal discountPercent = (discountAmount / subTotal) * 100;
                tbDiscountAmount.Text = $"-{discountAmount:N0} ({discountPercent:G29}%)"; // G29 để loại bỏ các số 0 không cần thiết
                gridDiscount.Visibility = Visibility.Visible;
            }
            else
            {
                // Ẩn phần giảm giá nếu không có
                gridDiscount.Visibility = Visibility.Collapsed;
            }
        }

        public void Clear()
        {
            this.Visibility = Visibility.Collapsed;
            dgBillItems.ItemsSource = null;
        }
    }
}