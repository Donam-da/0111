using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class MenuView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable? menuDataTable;

        public MenuView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCategoriesToComboBox();
            await LoadMenuItems();
        }

        private async Task LoadCategoriesToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name FROM Category WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable categoryTable = new DataTable();
                await Task.Run(() => adapter.Fill(categoryTable));
                cbCategory.ItemsSource = categoryTable.DefaultView;
            }
        }

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Chỉ tạo mã khi người dùng đang thêm mới (chưa chọn item nào từ grid)
            // Hoặc khi đang ở chế độ thêm mới (chưa có ID)
            if (dgMenuItems.SelectedItem == null || 
                !(((DataRowView)dgMenuItems.SelectedItem)["ID"] is int))
            {
                // Tạo mã không có hậu tố
                txtDrinkCode.Text = GenerateMenuCode(txtName.Text);
            }
        }

        private async Task LoadMenuItems()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = @"
                    SELECT 
                        d.ID, d.DrinkCode, d.Name, d.Price, d.ActualPrice, d.IsActive, d.CategoryID,
                        c.Name AS CategoryName 
                    FROM Drink d
                    JOIN Category c ON d.CategoryID = c.ID";

                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                menuDataTable = new DataTable();
                menuDataTable.Columns.Add("STT", typeof(int));
                menuDataTable.Columns.Add("StatusText", typeof(string));
                await Task.Run(() => adapter.Fill(menuDataTable));

                UpdateStatusText();
                dgMenuItems.ItemsSource = menuDataTable.DefaultView;
            }
        }

        private void UpdateStatusText()
        {
            if (menuDataTable != null) foreach (DataRow row in menuDataTable.Rows)
            {
                row["StatusText"] = (bool)row["IsActive"] ? "Hiển thị" : "Ẩn";
            }
        }

        private void DgMenuItems_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgMenuItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgMenuItems.SelectedItem is DataRowView row)
            {
                txtName.Text = row["Name"].ToString();
                txtDrinkCode.Text = row["DrinkCode"].ToString();
                cbCategory.SelectedValue = row["CategoryID"];
                txtPrice.Text = Convert.ToDecimal(row["Price"]).ToString("G0");
                txtActualPrice.Text = Convert.ToDecimal(row["ActualPrice"]).ToString("G0");
                chkIsActive.IsChecked = (bool)row["IsActive"];
            }
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "INSERT INTO Drink (DrinkCode, Name, CategoryID, Price, ActualPrice, IsActive) VALUES (@DrinkCode, @Name, @CategoryID, @Price, @ActualPrice, @IsActive)";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command);

                try
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Thêm đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadMenuItems();
                    ResetFields();
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi thêm đồ uống: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (dgMenuItems.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để cập nhật.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ValidateInput()) return;

            if (dgMenuItems.SelectedItem is DataRowView row)
            {
                int drinkId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    const string query = "UPDATE Drink SET DrinkCode = @DrinkCode, Name = @Name, CategoryID = @CategoryID, Price = @Price, ActualPrice = @ActualPrice, IsActive = @IsActive WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    AddParameters(command, drinkId);

                    try
                    {
                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                        MessageBox.Show("Cập nhật đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadMenuItems();
                        ResetFields();
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show($"Lỗi khi cập nhật đồ uống: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (menuDataTable != null)
            {
                menuDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
            }
        }

        private string GenerateMenuCode(string drinkName)
        {
            // Logic này giống với DrinkView và RecipeView để tạo mã, nhưng không có hậu tố
            string temp = drinkName.ToLower();
            temp = Regex.Replace(temp, "[áàảãạâấầẩẫậăắằẳẵặ]", "a");
            temp = Regex.Replace(temp, "[éèẻẽẹêếềểễệ]", "e");
            temp = Regex.Replace(temp, "[íìỉĩị]", "i");
            temp = Regex.Replace(temp, "[óòỏõọôốồổỗộơớờởỡợ]", "o");
            temp = Regex.Replace(temp, "[úùủũụưứừửữự]", "u");
            temp = Regex.Replace(temp, "[ýỳỷỹỵ]", "y");
            temp = Regex.Replace(temp, "[đ]", "d");
            // Bỏ các ký tự đặc biệt và khoảng trắng
            temp = Regex.Replace(temp.Replace(" ", ""), "[^a-z0-9]", "");
            // Thêm hậu tố _NB cho đồ uống mới tạo từ đây
            return temp + "_NB";
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || cbCategory.SelectedItem == null ||
                string.IsNullOrWhiteSpace(txtPrice.Text) || string.IsNullOrWhiteSpace(txtActualPrice.Text) || string.IsNullOrWhiteSpace(txtDrinkCode.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin: Tên, loại, giá vốn và giá bán.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!decimal.TryParse(txtPrice.Text, out _))
            {
                MessageBox.Show("Giá vốn phải là một số hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!decimal.TryParse(txtActualPrice.Text, out _))
            {
                MessageBox.Show("Giá bán phải là một số hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void AddParameters(SqlCommand command, int? id = null)
        {
            if (id.HasValue)
            {
                command.Parameters.AddWithValue("@ID", id.Value);
            }
            command.Parameters.AddWithValue("@DrinkCode", txtDrinkCode.Text);
            command.Parameters.AddWithValue("@Name", txtName.Text);
            command.Parameters.AddWithValue("@CategoryID", cbCategory.SelectedValue);
            command.Parameters.AddWithValue("@Price", decimal.Parse(txtPrice.Text));
            command.Parameters.AddWithValue("@ActualPrice", decimal.Parse(txtActualPrice.Text));
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }

        private void ResetFields()
        {
            txtName.Clear();
            txtDrinkCode.Clear();
            cbCategory.SelectedIndex = -1;
            txtPrice.Clear();
            txtActualPrice.Clear();
            chkIsActive.IsChecked = true;
            dgMenuItems.SelectedItem = null;
        }
    }
}