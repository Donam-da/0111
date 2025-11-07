using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace namm
{
    public partial class ProfitStatisticsView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable profitDataTable = new DataTable();

        public ProfitStatisticsView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Tải các bộ lọc
            // Tạm thời gỡ bỏ các event handler để tránh gọi FilterData() nhiều lần khi khởi tạo
            dpStartDate.SelectedDateChanged -= Filters_Changed;
            dpEndDate.SelectedDateChanged -= Filters_Changed;
            cbFilterDrinkType.SelectionChanged -= Filters_Changed;
            cbFilterCategory.SelectionChanged -= Filters_Changed;
            // Lưu ý: txtFilterDrinkName.TextChanged (nếu có) thường có handler riêng và không gây ra lỗi này.

            await LoadFilterComboBoxes();

            // Mặc định lọc theo ngày hôm nay
            dpStartDate.SelectedDate = DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today;
            
            // Gắn lại các event handler sau khi đã thiết lập giá trị ban đầu
            dpStartDate.SelectedDateChanged += Filters_Changed;
            dpEndDate.SelectedDateChanged += Filters_Changed;
            cbFilterDrinkType.SelectionChanged += Filters_Changed;
            cbFilterCategory.SelectionChanged += Filters_Changed;

            await FilterData();
        }

        private async Task LoadFilterComboBoxes()
        {
            // Load DrinkType filter
            cbFilterDrinkType.Items.Add("Tất cả");
            cbFilterDrinkType.Items.Add("Pha chế");
            cbFilterDrinkType.Items.Add("Nguyên bản");
            cbFilterDrinkType.SelectedIndex = 0;

            // Load Category filter
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT 0 AS ID, N'Tất cả' AS Name UNION ALL SELECT ID, Name FROM Category WHERE IsActive = 1 ORDER BY Name";
                var adapter = new SqlDataAdapter(query, connection);
                var categoryTable = new DataTable();
                await Task.Run(() => adapter.Fill(categoryTable));
                cbFilterCategory.ItemsSource = categoryTable.DefaultView;
                cbFilterCategory.SelectedIndex = 0;
            }
        }

        private async void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            await FilterData();
        }

        private async void Filters_Changed(object sender, RoutedEventArgs e)
        {
            // Nếu control chưa load xong hoặc đang trong quá trình khởi tạo, không làm gì cả.
            // Việc này giúp tránh gọi FilterData() nhiều lần khi các giá trị mặc định được thiết lập
            // trong UserControl_Loaded.
            // Kiểm tra IsLoaded để đảm bảo các phần tử UI đã sẵn sàng.
            if (!this.IsLoaded) return;
            await FilterData();
        }

        private async Task FilterData()
        {
            // Hàm này sẽ thay thế cho BtnFilter_Click cũ

            if (dpStartDate.SelectedDate == null || dpEndDate.SelectedDate == null)
            {
                MessageBox.Show("Vui lòng chọn ngày bắt đầu và ngày kết thúc.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime startDate = dpStartDate.SelectedDate.Value.Date;
            DateTime endDate = dpEndDate.SelectedDate.Value.Date.AddDays(1).AddTicks(-1); // Lấy đến cuối ngày

            // Lấy giá trị từ các bộ lọc mới
            string drinkNameFilter = txtFilterDrinkName.Text;
            string drinkTypeFilter = cbFilterDrinkType.SelectedIndex > 0 ? cbFilterDrinkType.SelectedItem.ToString() : null;
            int? categoryFilter = (cbFilterCategory.SelectedValue != null && (int)cbFilterCategory.SelectedValue > 0) ? (int)cbFilterCategory.SelectedValue : (int?)null;

            await LoadProfitDataAsync(startDate, endDate, drinkNameFilter, drinkTypeFilter, categoryFilter);
        }

        private async Task LoadProfitDataAsync(DateTime startDate, DateTime endDate, string drinkName, string drinkType, int? categoryId)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@StartDate", startDate),
                new SqlParameter("@EndDate", endDate)
            };

            using (var connection = new SqlConnection(connectionString))
            {
                var queryBuilder = new System.Text.StringBuilder(@"
                    WITH BillItemDetails AS (
                        SELECT
                            bi.BillID, bi.DrinkID, bi.DrinkType, bi.Quantity, bi.Price,
                            (bi.Quantity * bi.Price) AS ItemRevenue,
                            b.SubTotal, b.TotalAmount
                        FROM BillInfo bi
                        JOIN Bill b ON bi.BillID = b.ID
                        WHERE b.Status = 1 AND b.DateCheckOut BETWEEN @StartDate AND @EndDate
                    )
                    SELECT 
                        d.Name AS DrinkName, d.CategoryID,
                        bid.DrinkType,
                        SUM(bid.Quantity) AS TotalQuantitySold,
                        CASE WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice ELSE 0 END AS UnitCost,
                        SUM(bid.Quantity * (CASE WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice ELSE 0 END)) AS TotalCost,
                        SUM(CASE WHEN bid.SubTotal > 0 THEN (bid.ItemRevenue / bid.SubTotal) * bid.TotalAmount ELSE bid.ItemRevenue END) AS TotalRevenue
                    FROM BillItemDetails bid
                    JOIN Drink d ON bid.DrinkID = d.ID
                    WHERE 1=1 ");

                if (!string.IsNullOrWhiteSpace(drinkName))
                {
                    queryBuilder.Append(" AND d.Name LIKE @DrinkName");
                    parameters.Add(new SqlParameter("@DrinkName", $"%{drinkName}%"));
                }
                if (!string.IsNullOrWhiteSpace(drinkType))
                {
                    queryBuilder.Append(" AND bid.DrinkType = @DrinkType");
                    parameters.Add(new SqlParameter("@DrinkType", drinkType));
                }
                if (categoryId.HasValue)
                {
                    queryBuilder.Append(" AND d.CategoryID = @CategoryID");
                    parameters.Add(new SqlParameter("@CategoryID", categoryId.Value));
                }

                queryBuilder.Append(@"
                    GROUP BY d.Name, d.CategoryID, bid.DrinkType, d.RecipeCost, d.OriginalPrice
                    ORDER BY Profit DESC;
                ");

                // Thay thế ORDER BY Profit bằng ORDER BY (Doanh thu - Vốn) vì Profit không có trong SELECT
                string finalQuery = queryBuilder.ToString().Replace("ORDER BY Profit DESC", 
                    "ORDER BY (SUM(CASE WHEN bid.SubTotal > 0 THEN (bid.ItemRevenue / bid.SubTotal) * bid.TotalAmount ELSE bid.ItemRevenue END)) - (SUM(bid.Quantity * (CASE WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice ELSE 0 END))) DESC");


                var adapter = new SqlDataAdapter(finalQuery, connection);
                adapter.SelectCommand.Parameters.AddRange(parameters.ToArray());

                profitDataTable = new DataTable();
                profitDataTable.Columns.Add("STT", typeof(int));
                profitDataTable.Columns.Add("Profit", typeof(decimal));
                profitDataTable.Columns.Add("ProfitMargin", typeof(decimal));

                await Task.Run(() => adapter.Fill(profitDataTable));

                for (int i = 0; i < profitDataTable.Rows.Count; i++)
                {
                    var row = profitDataTable.Rows[i];
                    row["STT"] = i + 1;

                    decimal totalRevenue = Convert.ToDecimal(row["TotalRevenue"]);
                    decimal totalCost = Convert.ToDecimal(row["TotalCost"]);
                    decimal profit = totalRevenue - totalCost;
                    row["Profit"] = profit;

                    // Tính tỷ suất lợi nhuận, tránh chia cho 0
                    row["ProfitMargin"] = (totalRevenue > 0) ? (profit / totalRevenue) * 100 : 0;
                }

                dgProfitStats.ItemsSource = profitDataTable.DefaultView;
                CalculateTotals();
            }
        }
        private void CalculateTotals()
        {
            // Tính toán các giá trị tổng hợp dựa trên dữ liệu đã được lọc trong profitDataTable.
            // Điều này đảm bảo các con số tổng hợp luôn phản ánh chính xác nội dung đang hiển thị trong bảng.
            decimal totalRevenue = 0;
            decimal totalCost = 0;

            foreach (DataRow row in profitDataTable.Rows)
            {
                if (row["TotalRevenue"] != DBNull.Value)
                {
                    totalRevenue += Convert.ToDecimal(row["TotalRevenue"]);
                }
                if (row["TotalCost"] != DBNull.Value)
                {
                    totalCost += Convert.ToDecimal(row["TotalCost"]);
                }
            }
            tbTotalRevenue.Text = $"{totalRevenue:N0} VNĐ";
            tbTotalCost.Text = $"{totalCost:N0} VNĐ";
            tbTotalProfit.Text = $"{totalRevenue - totalCost:N0} VNĐ";
        }

    }
}