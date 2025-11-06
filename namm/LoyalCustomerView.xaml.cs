using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public class DiscountRule
    {
        public string CriteriaType { get; set; } = string.Empty; // "Số lần mua" hoặc "Tổng chi tiêu"
        public int ID { get; set; } // Thêm ID để dễ dàng xóa
        public decimal Threshold { get; set; }
        public decimal DiscountPercent { get; set; }
    }

    public partial class LoyalCustomerView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable customerTable = new DataTable();
        private ObservableCollection<DiscountRule> discountRules = new ObservableCollection<DiscountRule>();

        public LoyalCustomerView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            lvDiscountRules.ItemsSource = discountRules;
            await LoadDiscountRulesAsync(); // Tải các quy tắc đã lưu
            await LoadLoyalCustomersAsync(); // Tải danh sách khách hàng
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
                        ISNULL(SUM(b.TotalAmount), 0) AS TotalSpent,
                        ISNULL(SUM(b.SubTotal - b.TotalAmount), 0) AS TotalDiscountGiven
                    FROM Customer c
                    LEFT JOIN Bill b ON c.ID = b.IdCustomer AND b.Status = 1 -- Chỉ tính các hóa đơn đã thanh toán
                    GROUP BY c.ID, c.Name, c.CustomerCode, c.PhoneNumber, c.Address
                    ORDER BY TotalSpent DESC;
                ";

                var adapter = new SqlDataAdapter(query, connection);
                customerTable = new DataTable();
                customerTable.Columns.Add("STT", typeof(int));
                customerTable.Columns.Add("Discount", typeof(decimal)); // Thêm cột giảm giá

                await Task.Run(() => adapter.Fill(customerTable));

                // Thêm số thứ tự
                for (int i = 0; i < customerTable.Rows.Count; i++)
                {
                    customerTable.Rows[i]["STT"] = i + 1;
                    customerTable.Rows[i]["Discount"] = customerTable.Rows[i]["TotalDiscountGiven"];
                }

                dgLoyalCustomers.ItemsSource = customerTable.DefaultView;
            }
        }

        private async Task LoadDiscountRulesAsync()
        {
            discountRules.Clear();
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, CriteriaType, Threshold, DiscountPercent FROM DiscountRule ORDER BY CriteriaType, Threshold";
                var command = new SqlCommand(query, connection);
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        discountRules.Add(new DiscountRule
                        {
                            ID = reader.GetInt32(0),
                            CriteriaType = reader.GetString(1),
                            Threshold = reader.GetDecimal(2),
                            DiscountPercent = reader.GetDecimal(3)
                        });
                    }
                }
            }
        }

        private async void BtnAddRule_Click(object sender, RoutedEventArgs e)
        {
            if (cbCriteriaType.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một tiêu chí.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!decimal.TryParse(txtThreshold.Text, out decimal threshold) || threshold <= 0)
            {
                MessageBox.Show("Ngưỡng phải là một số dương hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (!decimal.TryParse(txtDiscountPercent.Text, out decimal discountPercent) || discountPercent < 0)
            {
                MessageBox.Show("Mức giảm giá phải là một số không âm hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string criteriaType = ((ComboBoxItem)cbCriteriaType.SelectedItem).Content.ToString();

            // Lưu vào DB
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "INSERT INTO DiscountRule (CriteriaType, Threshold, DiscountPercent) OUTPUT INSERTED.ID VALUES (@CriteriaType, @Threshold, @DiscountPercent)";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@CriteriaType", criteriaType);
                command.Parameters.AddWithValue("@Threshold", threshold);
                command.Parameters.AddWithValue("@DiscountPercent", discountPercent);

                try
                {
                    await connection.OpenAsync();
                    int newId = (int)await command.ExecuteScalarAsync();

                    // Thêm vào danh sách trên UI
                    discountRules.Add(new DiscountRule
                    {
                        ID = newId,
                        CriteriaType = criteriaType,
                        Threshold = threshold,
                        DiscountPercent = discountPercent
                    });

                    // Reset input fields
                    cbCriteriaType.SelectedIndex = -1;
                    txtThreshold.Clear();
                    txtDiscountPercent.Clear();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi thêm mức giảm giá: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
        {
            if (lvDiscountRules.SelectedItem is DiscountRule selectedRule)
            {
                // Xóa khỏi DB
                using (var connection = new SqlConnection(connectionString))
                {
                    var command = new SqlCommand("DELETE FROM DiscountRule WHERE ID = @ID", connection);
                    command.Parameters.AddWithValue("@ID", selectedRule.ID);
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
                // Xóa khỏi UI
                discountRules.Remove(selectedRule);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một mức giảm giá để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

    }
}