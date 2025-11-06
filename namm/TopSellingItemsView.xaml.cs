using System;
using System.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class TopSellingItemsView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable topItemsTable = new DataTable();

        public TopSellingItemsView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Mặc định lọc theo ngày hôm nay
            dpStartDate.SelectedDate = DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today;

            // Thêm các mục cho ComboBox lọc kiểu
            cbFilterDrinkType.Items.Add("Tất cả");
            cbFilterDrinkType.Items.Add("Pha chế");
            cbFilterDrinkType.Items.Add("Nguyên bản");
            cbFilterDrinkType.SelectedIndex = 0;

            BtnFilter_Click(sender, e);
        }

        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (dpStartDate.SelectedDate == null || dpEndDate.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày bắt đầu và ngày kết thúc.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime startDate = dpStartDate.SelectedDate.Value.Date;
            DateTime endDate = dpEndDate.SelectedDate.Value.Date.AddDays(1).AddTicks(-1); // Lấy đến cuối ngày

            await LoadTopSellingItemsAsync(startDate, endDate);
        }

        private async Task LoadTopSellingItemsAsync(DateTime startDate, DateTime endDate)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT
                        d.Name AS DrinkName,
                        d.DrinkCode,
                        bi.DrinkType,
                        SUM(bi.Quantity) AS TotalQuantitySold,
                        SUM(bi.Quantity * bi.Price) AS TotalRevenue
                    FROM BillInfo bi
                    JOIN Bill b ON bi.BillID = b.ID
                    JOIN Drink d ON bi.DrinkID = d.ID
                    WHERE b.Status = 1 AND b.DateCheckOut BETWEEN @StartDate AND @EndDate
                    GROUP BY d.Name, d.DrinkCode, bi.DrinkType
                    ORDER BY TotalQuantitySold DESC;
                ";

                var adapter = new SqlDataAdapter(query, connection);
                adapter.SelectCommand.Parameters.AddWithValue("@StartDate", startDate);
                adapter.SelectCommand.Parameters.AddWithValue("@EndDate", endDate);

                topItemsTable = new DataTable();
                topItemsTable.Columns.Add("STT", typeof(int));

                await Task.Run(() => adapter.Fill(topItemsTable));

                for (int i = 0; i < topItemsTable.Rows.Count; i++)
                {
                    topItemsTable.Rows[i]["STT"] = i + 1;
                }

                dgTopSelling.ItemsSource = topItemsTable.DefaultView;
                ApplyColumnFilters(); // Áp dụng bộ lọc cột sau khi tải dữ liệu
            }
        }

        private void DpStartDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpStartDate.SelectedDate.HasValue)
            {
                dpEndDate.DisplayDateStart = dpStartDate.SelectedDate;
                if (dpEndDate.SelectedDate < dpStartDate.SelectedDate)
                {
                    dpEndDate.SelectedDate = dpStartDate.SelectedDate;
                }
            }
        }

        private void Filter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyColumnFilters();
        }

        private void Filter_TextChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyColumnFilters();
        }

        private void ApplyColumnFilters()
        {
            if (topItemsTable.DefaultView == null) return;

            var filters = new List<string>();

            if (!string.IsNullOrWhiteSpace(txtFilterDrinkName.Text))
            {
                filters.Add($"DrinkName LIKE '%{txtFilterDrinkName.Text.Replace("'", "''")}%'");
            }
            if (!string.IsNullOrWhiteSpace(txtFilterDrinkCode.Text))
            {
                filters.Add($"DrinkCode LIKE '%{txtFilterDrinkCode.Text.Replace("'", "''")}%'");
            }
            if (cbFilterDrinkType.SelectedIndex > 0) // Bỏ qua nếu chọn "Tất cả"
            {
                filters.Add($"DrinkType = '{cbFilterDrinkType.SelectedItem}'");
            }
            topItemsTable.DefaultView.RowFilter = string.Join(" AND ", filters);
        }
    }
}