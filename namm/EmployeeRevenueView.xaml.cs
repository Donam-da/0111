using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class EmployeeRevenueView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable revenueDataTable = new DataTable();

        public EmployeeRevenueView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Tạm gỡ event handler để tránh gọi nhiều lần
            dpStartDate.SelectedDateChanged -= Filters_Changed;
            dpEndDate.SelectedDateChanged -= Filters_Changed;
            cbEmployees.SelectionChanged -= Filters_Changed;

            await LoadEmployeeFilterAsync();

            // Mặc định là tháng này
            var today = DateTime.Today;
            dpStartDate.SelectedDate = new DateTime(today.Year, today.Month, 1);
            dpEndDate.SelectedDate = today;

            // Gắn lại event handler
            dpStartDate.SelectedDateChanged += Filters_Changed;
            dpEndDate.SelectedDateChanged += Filters_Changed;
            cbEmployees.SelectionChanged += Filters_Changed;

            await FilterData();
        }

        private async Task LoadEmployeeFilterAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                // Lấy danh sách nhân viên (Type = 0) và admin (Type = 1), thêm mục "Tất cả"
                const string query = @"
                    SELECT 'ALL_USERS' AS UserName, N'Tất cả' AS DisplayName, -1 AS SortOrder
                    UNION ALL
                    SELECT UserName, DisplayName, 0 AS SortOrder FROM Account WHERE Type IN (0, 1)
                    ORDER BY SortOrder, DisplayName";
                var adapter = new SqlDataAdapter(query, connection);
                var employeeTable = new DataTable();
                await Task.Run(() => adapter.Fill(employeeTable));
                cbEmployees.ItemsSource = employeeTable.DefaultView;
                cbEmployees.SelectedIndex = 0;
            }
        }

        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            await FilterData();
        }

        private async void Filters_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!this.IsLoaded) return;
            await FilterData();
        }

        private async Task FilterData()
        {
            DateTime? startDate = dpStartDate.SelectedDate?.Date;
            DateTime? endDate = dpEndDate.SelectedDate?.Date.AddDays(1).AddTicks(-1); // Lấy đến cuối ngày
            string? employeeUserName = (cbEmployees.SelectedValue != null && cbEmployees.SelectedValue.ToString() != "ALL_USERS")
                                       ? cbEmployees.SelectedValue.ToString()
                                       : null;

            await LoadRevenueDataAsync(startDate, endDate, employeeUserName);
        }

        private async Task LoadRevenueDataAsync(DateTime? startDate, DateTime? endDate, string? userName)
        {
            var parameters = new List<SqlParameter>();
            var queryBuilder = new StringBuilder(@"
                SELECT 
                    a.UserName,
                    a.DisplayName,
                    a.Type,
                    COUNT(b.ID) AS InvoiceCount,
                    ISNULL(SUM(b.TotalAmount), 0) AS TotalRevenue
                FROM Account a ");

            var joinConditions = new List<string> { "a.UserName = b.AccountUserName", "b.Status = 1" };

            if (startDate.HasValue)
            {
                joinConditions.Add("b.DateCheckOut >= @StartDate");
                parameters.Add(new SqlParameter("@StartDate", startDate.Value));
            }
            if (endDate.HasValue)
            {
                joinConditions.Add("b.DateCheckOut <= @EndDate");
                parameters.Add(new SqlParameter("@EndDate", endDate.Value));
            }

            queryBuilder.Append($" LEFT JOIN Bill b ON {string.Join(" AND ", joinConditions)} WHERE a.Type IN (0, 1) ");

            if (!string.IsNullOrEmpty(userName))
            {
                queryBuilder.Append(" AND a.UserName = @UserName");
                parameters.Add(new SqlParameter("@UserName", userName));
            }

            queryBuilder.Append(@"
                GROUP BY a.UserName, a.DisplayName, a.Type
                ORDER BY TotalRevenue DESC");

            using (var connection = new SqlConnection(connectionString))
            {
                var adapter = new SqlDataAdapter(queryBuilder.ToString(), connection);
                if (parameters.Any())
                {
                    adapter.SelectCommand.Parameters.AddRange(parameters.ToArray());
                }

                revenueDataTable = new DataTable();
                revenueDataTable.Columns.Add("STT", typeof(int));
                revenueDataTable.Columns.Add("Role", typeof(string));

                await Task.Run(() => adapter.Fill(revenueDataTable));

                for (int i = 0; i < revenueDataTable.Rows.Count; i++)
                {
                    var row = revenueDataTable.Rows[i];
                    row["STT"] = i + 1;
                    row["Role"] = Convert.ToInt32(row["Type"]) == 1 ? "Admin" : "Nhân viên";
                }

                dgEmployeeRevenue.ItemsSource = revenueDataTable.DefaultView;
                CalculateTotals();
            }
        }

        private void CalculateTotals()
        {
            long totalInvoices = 0;
            decimal totalRevenue = 0;

            foreach (DataRow row in revenueDataTable.Rows)
            {
                if (row["InvoiceCount"] != DBNull.Value)
                {
                    totalInvoices += Convert.ToInt64(row["InvoiceCount"]);
                }
                if (row["TotalRevenue"] != DBNull.Value)
                {
                    totalRevenue += Convert.ToDecimal(row["TotalRevenue"]);
                }
            }

            tbTotalInvoices.Text = $"{totalInvoices:N0}";
            tbTotalRevenue.Text = $"{totalRevenue:N0} VNĐ";
        }
    }
}