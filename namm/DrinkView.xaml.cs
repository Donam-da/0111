using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace namm
{
    public partial class DrinkView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable? drinkDataTable;

        public DrinkView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCategoriesToComboBox();
            LoadDrinks();
        }

        private void LoadCategoriesToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT ID, Name FROM Category WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable categoryTable = new DataTable();
                adapter.Fill(categoryTable);
                cbCategory.ItemsSource = categoryTable.DefaultView;
            }
        }

        private void LoadDrinks()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT 
                        d.ID, d.DrinkCode, d.Name, d.Price, d.ActualPrice, d.IsActive, d.CategoryID,
                        c.Name AS CategoryName 
                    FROM Drink d
                    JOIN Category c ON d.CategoryID = c.ID";

                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                drinkDataTable = new DataTable(); // Initialize the DataTable
                drinkDataTable.Columns.Add("STT", typeof(int));
                drinkDataTable.Columns.Add("StatusText", typeof(string));
                adapter.Fill(drinkDataTable);

                UpdateStatusText();
                dgDrinks.ItemsSource = drinkDataTable.DefaultView;
            }
        }

        private void UpdateStatusText()
        {
            if (drinkDataTable != null) foreach (DataRow row in drinkDataTable.Rows)
            {
                row["StatusText"] = (bool)row["IsActive"] ? "Sử dụng" : "Ngưng";
            }
        }

        private void DgDrinks_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgDrinks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgDrinks.SelectedItem is DataRowView row)
            {
                txtName.Text = row["Name"].ToString();
                txtDrinkCode.Text = row["DrinkCode"].ToString();
                txtPrice.Text = Convert.ToDecimal(row["Price"]).ToString("G0");
                txtActualPrice.Text = Convert.ToDecimal(row["ActualPrice"]).ToString("G0");
                chkIsActive.IsChecked = (bool)row["IsActive"];
                cbCategory.SelectedValue = row["CategoryID"];
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput()) return;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "INSERT INTO Drink (DrinkCode, Name, CategoryID, Price, ActualPrice, IsActive) VALUES (@DrinkCode, @Name, @CategoryID, @Price, @ActualPrice, @IsActive)";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command);

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Thêm đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadDrinks();
                ResetFields();
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgDrinks.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ValidateInput()) return;

            DataRowView row = (DataRowView)dgDrinks.SelectedItem;
            int drinkId = (int)row["ID"];

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE Drink SET DrinkCode = @DrinkCode, Name = @Name, CategoryID = @CategoryID, Price = @Price, ActualPrice = @ActualPrice, IsActive = @IsActive WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", drinkId);
                AddParameters(command);

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Cập nhật đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadDrinks();
                ResetFields();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgDrinks.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa đồ uống này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgDrinks.SelectedItem;
                int drinkId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "DELETE FROM Drink WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", drinkId);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Xóa đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadDrinks();
                    ResetFields();
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (drinkDataTable != null)
            {
                drinkDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
            }
        }

        private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            txtDrinkCode.Text = GenerateDrinkCode(txtName.Text);
        }

        private string GenerateDrinkCode(string drinkName)
        {
            // Chuyển thành chữ thường, bỏ dấu
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
            return temp + "_NB";
        }

        private void ResetFields()
        {
            txtName.Clear();
            txtDrinkCode.Clear();
            txtPrice.Clear();
            txtActualPrice.Clear();
            cbCategory.SelectedIndex = -1;
            chkIsActive.IsChecked = true;
            dgDrinks.SelectedItem = null;
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) || cbCategory.SelectedItem == null || 
                string.IsNullOrWhiteSpace(txtPrice.Text) || string.IsNullOrWhiteSpace(txtActualPrice.Text) || string.IsNullOrWhiteSpace(txtDrinkCode.Text))
            {
                MessageBox.Show("Vui lòng nhập đầy đủ thông tin: Tên, loại, giá nhập và giá bán. Mã đồ uống sẽ được tạo tự động.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!decimal.TryParse(txtPrice.Text, out _))
            {
                MessageBox.Show("Giá nhập vào phải là một số hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!decimal.TryParse(txtActualPrice.Text, out _))
            {
                MessageBox.Show("Giá bán phải là một số hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void AddParameters(SqlCommand command)
        {
            command.Parameters.AddWithValue("@DrinkCode", txtDrinkCode.Text);
            command.Parameters.AddWithValue("@Name", txtName.Text);
            command.Parameters.AddWithValue("@CategoryID", cbCategory.SelectedValue);
            command.Parameters.AddWithValue("@Price", Convert.ToDecimal(txtPrice.Text));
            command.Parameters.AddWithValue("@ActualPrice", Convert.ToDecimal(txtActualPrice.Text));
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }
    }
}