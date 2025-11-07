﻿using System;
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
            dpStartDate.SelectedDateChanged -= DpStartDate_SelectedDateChanged; // Gỡ cả event mới
            dpStartDate.SelectedDateChanged -= Filters_Changed;
            dpEndDate.SelectedDateChanged -= Filters_Changed;
            cbFilterDrinkName.SelectionChanged -= Filters_Changed;
            cbFilterDrinkType.SelectionChanged -= Filters_Changed;
            cbFilterCategory.SelectionChanged -= Filters_Changed;
            // Lưu ý: txtFilterDrinkName.TextChanged (nếu có) thường có handler riêng và không gây ra lỗi này.

            await LoadFilterComboBoxes();

            // Lấy ngày có hóa đơn đầu tiên từ DB
            DateTime? firstInvoiceDate = await GetFirstInvoiceDateAsync();

            // Nếu không có hóa đơn nào, mặc định là ngày hôm nay. Ngược lại, lấy ngày đầu tiên.
            dpStartDate.SelectedDate = firstInvoiceDate?.Date ?? DateTime.Today;
            dpEndDate.SelectedDate = DateTime.Today;
            
            // Gắn lại các event handler sau khi đã thiết lập giá trị ban đầu
            dpStartDate.SelectedDateChanged += DpStartDate_SelectedDateChanged; // Gắn lại event mới
            dpStartDate.SelectedDateChanged += Filters_Changed;
            dpEndDate.SelectedDateChanged += Filters_Changed;
            cbFilterDrinkName.SelectionChanged += Filters_Changed;
            cbFilterDrinkType.SelectionChanged += Filters_Changed;
            cbFilterCategory.SelectionChanged += Filters_Changed;

            await FilterData();
        }

        private async Task<DateTime?> GetFirstInvoiceDateAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                // Lấy ngày thanh toán sớm nhất từ các hóa đơn đã hoàn thành
                const string query = "SELECT MIN(DateCheckOut) FROM Bill WHERE Status = 1";
                var command = new SqlCommand(query, connection);
                await connection.OpenAsync();
                var result = await command.ExecuteScalarAsync();
                if (result != null && result != DBNull.Value)
                {
                    return (DateTime)result;
                }
            }
            return null; // Trả về null nếu không có hóa đơn nào
        }

        private async Task LoadFilterComboBoxes()
        {
            // Load DrinkName filter
            using (var connection = new SqlConnection(connectionString))
            {
                // Thêm cột SortOrder để đảm bảo 'Tất cả' luôn ở đầu tiên
                const string query = @"
                    SELECT 0 AS ID, N'Tất cả' AS Name, 0 AS SortOrder
                    UNION ALL
                    SELECT DISTINCT ID, Name, 1 AS SortOrder FROM Drink ORDER BY SortOrder, Name";
                var adapter = new SqlDataAdapter(query, connection);
                var drinkTable = new DataTable();
                await Task.Run(() => adapter.Fill(drinkTable));
                cbFilterDrinkName.ItemsSource = drinkTable.DefaultView;
                cbFilterDrinkName.SelectedIndex = 0;
            }

            // Load DrinkType filter
            cbFilterDrinkType.Items.Add("Tất cả");
            cbFilterDrinkType.Items.Add("Pha chế");
            cbFilterDrinkType.Items.Add("Nguyên bản");
            cbFilterDrinkType.SelectedIndex = 0;

            // Load Category filter
            using (var connection = new SqlConnection(connectionString))
            {
                // Thêm cột SortOrder để đảm bảo 'Tất cả' luôn ở đầu tiên
                const string query = @"
                    SELECT 0 AS ID, N'Tất cả' AS Name, 0 AS SortOrder 
                    UNION ALL 
                    SELECT ID, Name, 1 AS SortOrder FROM Category WHERE IsActive = 1 ORDER BY SortOrder, Name";
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

            DateTime? startDate = dpStartDate.SelectedDate?.Date;
            DateTime? endDate = dpEndDate.SelectedDate?.Date.AddDays(1).AddTicks(-1); // Lấy đến cuối ngày
            int? drinkIdFilter = (cbFilterDrinkName.SelectedValue != null && (int)cbFilterDrinkName.SelectedValue > 0) ? (int)cbFilterDrinkName.SelectedValue : (int?)null;
            string? drinkTypeFilter = cbFilterDrinkType.SelectedIndex > 0 ? cbFilterDrinkType.SelectedItem.ToString() : null;
            int? categoryFilter = (cbFilterCategory.SelectedValue != null && (int)cbFilterCategory.SelectedValue > 0) ? (int)cbFilterCategory.SelectedValue : (int?)null;

            await LoadProfitDataAsync(startDate, endDate, drinkIdFilter, drinkTypeFilter, categoryFilter);
        }

        private async Task LoadProfitDataAsync(DateTime? startDate, DateTime? endDate, int? drinkId, string? drinkType, int? categoryId)
        {
            var parameters = new List<SqlParameter>();

            using (var connection = new SqlConnection(connectionString))
            {
                var queryBuilder = new System.Text.StringBuilder(@"
                    WITH BillItemDetails AS (
                        SELECT
                            bi.BillID, bi.DrinkID, bi.DrinkType, bi.Quantity, bi.Price,
                            (bi.Quantity * bi.Price) AS ItemRevenue,
                            b.SubTotal, b.TotalAmount
                        FROM BillInfo bi
                        JOIN Bill b ON bi.BillID = b.ID WHERE b.Status = 1
                    )
                    SELECT 
                    d.Name AS DrinkName, 
                    d.CategoryID,
                        bid.DrinkType,
                        SUM(bid.Quantity) AS TotalQuantitySold,
                    -- Đảm bảo giá vốn không bao giờ là NULL
                    ISNULL(CASE 
                        WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost 
                        WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice 
                        ELSE 0 
                    END, 0) AS UnitCost,
                    -- Tính tổng vốn dựa trên giá vốn và số lượng bán
                    SUM(bid.Quantity * ISNULL(CASE 
                        WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost 
                        WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice 
                        ELSE 0 
                    END, 0)) AS TotalCost,
                    -- Sửa lại cách tính doanh thu để tránh sai số làm tròn.
                    -- Doanh thu của một món = (Doanh thu trước giảm giá của món đó / Tổng doanh thu trước giảm giá của hóa đơn) * Tổng tiền thanh toán cuối cùng của hóa đơn đó.
                    -- Sau đó mới SUM lại. Cách này đảm bảo tổng doanh thu của các món sẽ bằng chính xác tổng doanh thu của các hóa đơn.
                    SUM(
                        CASE 
                            -- Ép kiểu sang DECIMAL để đảm bảo phép chia là phép chia số thực, tránh sai số do làm tròn của phép chia số nguyên.
                            -- (DECIMAL * DECIMAL) / DECIMAL = DECIMAL
                            WHEN bid.SubTotal > 0 THEN 
                                (CAST(bid.ItemRevenue AS DECIMAL(18, 2)) * CAST(bid.TotalAmount AS DECIMAL(18, 2))) / CAST(bid.SubTotal AS DECIMAL(18, 2))
                            ELSE bid.ItemRevenue 
                        END
                    ) AS TotalRevenue
                    FROM BillItemDetails bid
                    JOIN Drink d ON bid.DrinkID = d.ID
                    WHERE 1=1 ");

                if (startDate.HasValue)
                {
                    queryBuilder.Append(" AND bid.BillID IN (SELECT ID FROM Bill WHERE DateCheckOut >= @StartDate)");
                    parameters.Add(new SqlParameter("@StartDate", startDate.Value));
                }

                if (endDate.HasValue)
                {
                    queryBuilder.Append(" AND bid.BillID IN (SELECT ID FROM Bill WHERE DateCheckOut <= @EndDate)");
                    parameters.Add(new SqlParameter("@EndDate", endDate.Value));
                }

                if (drinkId.HasValue)
                {
                    queryBuilder.Append(" AND d.ID = @DrinkID");
                    parameters.Add(new SqlParameter("@DrinkID", drinkId.Value));
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
                    @"ORDER BY 
                        (SUM(
                            CASE 
                                WHEN bid.SubTotal > 0 THEN 
                                    (CAST(bid.ItemRevenue AS DECIMAL(18, 2)) * CAST(bid.TotalAmount AS DECIMAL(18, 2))) / CAST(bid.SubTotal AS DECIMAL(18, 2))
                                ELSE bid.ItemRevenue 
                            END
                        )) 
                        - 
                        (SUM(bid.Quantity * ISNULL(CASE WHEN bid.DrinkType = N'Pha chế' THEN d.RecipeCost WHEN bid.DrinkType = N'Nguyên bản' THEN d.OriginalPrice ELSE 0 END, 0))) 
                    DESC");


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

        private void DpStartDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Logic này đảm bảo người dùng không thể chọn ngày kết thúc trước ngày bắt đầu.
            if (dpStartDate.SelectedDate.HasValue)
            {
                // Đặt ngày bắt đầu có thể chọn cho DatePicker 'Đến ngày'
                dpEndDate.DisplayDateStart = dpStartDate.SelectedDate;

                // Nếu ngày kết thúc hiện tại nhỏ hơn ngày bắt đầu mới, cập nhật nó
                if (dpEndDate.SelectedDate < dpStartDate.SelectedDate)
                {
                    dpEndDate.SelectedDate = dpStartDate.SelectedDate;
                }
            }
        }
    }
}