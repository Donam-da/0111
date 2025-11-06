using System;
using System.Data;
using System.Windows.Controls;

namespace namm
{
    public partial class InvoicePreviewControl : UserControl
    {
        public InvoicePreviewControl()
        {
            InitializeComponent();
        }

        public void DisplayInvoice(DataRowView invoiceData, DataView invoiceDetails)
        {
            if (invoiceData == null || invoiceDetails == null)
            {
                Clear();
                return;
            }

            this.Visibility = System.Windows.Visibility.Visible;

            tbInvoiceId.Text = ((int)invoiceData["ID"]).ToString("D6");
            tbTableName.Text = invoiceData["TableName"].ToString();
            tbCustomerName.Text = invoiceData["CustomerName"].ToString();
            tbCustomerCode.Text = invoiceData["CustomerCode"]?.ToString() ?? "N/A";
            tbDateTime.Text = ((DateTime)invoiceData["DateCheckOut"]).ToString("dd/MM/yyyy HH:mm");
            tbTotalAmount.Text = $"{((decimal)invoiceData["TotalAmount"]):N0} VNĐ";

            dgBillItems.ItemsSource = invoiceDetails;
        }

        public void Clear()
        {
            this.Visibility = System.Windows.Visibility.Collapsed;
            dgBillItems.ItemsSource = null;
        }
    }
}