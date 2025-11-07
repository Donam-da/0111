using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    /// <summary>
    /// Interaction logic for InvoicePreviewControl.xaml
    /// </summary>
    public partial class InvoicePreviewControl : UserControl
    {
        public string? WatermarkIconSource { get; private set; }

        public InvoicePreviewControl()
        {
            InitializeComponent();
            this.DataContext = this;
            LoadWatermarkIcon();
        }

        private void LoadWatermarkIcon()
        {
            // Đọc đường dẫn ảnh đã được lưu trong phần cài đặt giao diện
            string? iconPath = Properties.Settings.Default.LoginIconPath;

            // Chỉ hiển thị nếu đường dẫn hợp lệ và file tồn tại
            if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
            {
                WatermarkIconSource = iconPath;
            }
        }

        public void DisplayInvoice(DataRowView invoiceData, DataView detailsData)
        {
            this.Visibility = Visibility.Visible;

            tbInvoiceId.Text = ((int)invoiceData["ID"]).ToString("D6");
            tbTableName.Text = invoiceData["TableName"].ToString();
            tbCustomerName.Text = invoiceData["CustomerName"].ToString();
            tbCustomerCode.Text = invoiceData["CustomerCode"].ToString();
            tbDateTime.Text = ((DateTime)invoiceData["DateCheckOut"]).ToString("dd/MM/yyyy HH:mm");

            dgBillItems.ItemsSource = detailsData;

            tbSubTotal.Text = $"{Convert.ToDecimal(invoiceData["SubTotal"]):N0}";
            tbTotalAmount.Text = $"{Convert.ToDecimal(invoiceData["TotalAmount"]):N0} VNĐ";
        }

        public void Clear()
        {
            this.Visibility = Visibility.Collapsed;
            dgBillItems.ItemsSource = null;
        }
    }
}