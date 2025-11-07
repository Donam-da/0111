using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Mặc định lọc theo ngày hôm nay
            dpStartDate.SelectedDate = DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today;

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

            await LoadProfitDataAsync(startDate, endDate);
        }

        private async Task LoadProfitDataAsync(DateTime startDate, DateTime endDate)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT
                        d.Name AS DrinkName,
                        bi.DrinkType,
                        SUM(bi.Quantity) AS TotalQuantitySold,
                        -- Tính giá vốn trên mỗi đơn vị
                        CASE 
                            WHEN bi.DrinkType = N'Pha chế' THEN d.RecipeCost
                            WHEN bi.DrinkType = N'Nguyên bản' THEN d.OriginalPrice
                            ELSE 0 
                        END AS UnitCost,
                        -- Tính tổng vốn
                        SUM(bi.Quantity * 
                            CASE 
                                WHEN bi.DrinkType = N'Pha chế' THEN d.RecipeCost
                                WHEN bi.DrinkType = N'Nguyên bản' THEN d.OriginalPrice
                                ELSE 0 
                            END
                        ) AS TotalCost,
                        -- Tính tổng doanh thu
                        SUM(bi.Quantity * bi.Price) AS TotalRevenue,
                        -- Tính lợi nhuận
                        SUM(bi.Quantity * bi.Price) - SUM(bi.Quantity * 
                            CASE 
                                WHEN bi.DrinkType = N'Pha chế' THEN d.RecipeCost
                                WHEN bi.DrinkType = N'Nguyên bản' THEN d.OriginalPrice
                                ELSE 0 
                            END
                        ) AS Profit
                    FROM BillInfo bi
                    JOIN Bill b ON bi.BillID = b.ID
                    JOIN Drink d ON bi.DrinkID = d.ID
                    WHERE b.Status = 1 AND b.DateCheckOut BETWEEN @StartDate AND @EndDate
                    GROUP BY d.Name, bi.DrinkType, d.RecipeCost, d.OriginalPrice
                    ORDER BY Profit DESC;
                ";

                var adapter = new SqlDataAdapter(query, connection);
                adapter.SelectCommand.Parameters.AddWithValue("@StartDate", startDate);
                adapter.SelectCommand.Parameters.AddWithValue("@EndDate", endDate);

                profitDataTable = new DataTable();
                profitDataTable.Columns.Add("STT", typeof(int));

                await Task.Run(() => adapter.Fill(profitDataTable));

                for (int i = 0; i < profitDataTable.Rows.Count; i++)
                {
                    profitDataTable.Rows[i]["STT"] = i + 1;
                }

                dgProfitStats.ItemsSource = profitDataTable.DefaultView;
                CalculateTotals();
            }
        }

        private void CalculateTotals()
        {
            decimal totalRevenue = 0;
            decimal totalCost = 0;

            foreach (DataRow row in profitDataTable.Rows)
            {
                totalRevenue += (decimal)row["TotalRevenue"];
                totalCost += (decimal)row["TotalCost"];
            }

            tbTotalRevenue.Text = $"{totalRevenue:N0} VNĐ";
            tbTotalCost.Text = $"{totalCost:N0} VNĐ";
            tbTotalProfit.Text = $"{totalRevenue - totalCost:N0} VNĐ";
        }
    }
}