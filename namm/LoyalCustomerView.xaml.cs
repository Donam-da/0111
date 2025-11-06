using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class LoyalCustomerView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public LoyalCustomerView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadLoyalCustomersAsync();
        }

        private async Task LoadLoyalCustomersAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT
                        c.ID,
                        c.Name AS CustomerName,
                        c.CustomerCode,
                        c.PhoneNumber,
                        c.Address,
                        COUNT(b.ID) AS PurchaseCount,
                        ISNULL(SUM(b.TotalAmount), 0) AS TotalSpent
                    FROM Customer c
                    LEFT JOIN Bill b ON c.ID = b.IdCustomer AND b.Status = 1 -- Chỉ tính các hóa đơn đã thanh toán
                    GROUP BY c.ID, c.Name, c.CustomerCode, c.PhoneNumber, c.Address
                    ORDER BY TotalSpent DESC; -- Sắp xếp theo tổng chi tiêu giảm dần
                ";

                var adapter = new SqlDataAdapter(query, connection);
                var customerTable = new DataTable();
                customerTable.Columns.Add("STT", typeof(int));

                await Task.Run(() => adapter.Fill(customerTable));

                // Thêm số thứ tự
                for (int i = 0; i < customerTable.Rows.Count; i++)
                {
                    customerTable.Rows[i]["STT"] = i + 1;
                }

                dgLoyalCustomers.ItemsSource = customerTable.DefaultView;
            }
        }
    }
}