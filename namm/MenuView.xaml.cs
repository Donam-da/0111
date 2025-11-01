using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
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

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDrinksToComboBox();
            LoadMenuItems();
        }

        private void LoadDrinksToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Tải tất cả đồ uống nguyên bản để người dùng chọn
                string query = "SELECT ID, Name FROM Drink ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable drinkTable = new DataTable();
                adapter.Fill(drinkTable);
                cbDrink.ItemsSource = drinkTable.DefaultView;
            }
        }

        private void LoadMenuItems()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT 
                        d.ID, REPLACE(d.DrinkCode, '_NB', '') AS DrinkCode, d.Name, d.ActualPrice, d.IsActive,
                        c.Name AS CategoryName 
                    FROM Drink d
                    JOIN Category c ON d.CategoryID = c.ID";

                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                menuDataTable = new DataTable();
                menuDataTable.Columns.Add("STT", typeof(int));
                menuDataTable.Columns.Add("StatusText", typeof(string));
                adapter.Fill(menuDataTable);

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
                cbDrink.SelectedValue = row["ID"];
                txtDrinkCode.Text = row["DrinkCode"].ToString();
                txtActualPrice.Text = Convert.ToDecimal(row["ActualPrice"]).ToString("G0");
                chkIsActive.IsChecked = (bool)row["IsActive"];
            }
        }

        private void CbDrink_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDrink.SelectedItem is DataRowView selectedDrink)
            {
                string drinkName = selectedDrink["Name"].ToString();
                txtDrinkCode.Text = GenerateMenuCode(drinkName);
            }
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (cbDrink.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để cập nhật.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtActualPrice.Text) || !decimal.TryParse(txtActualPrice.Text, out _))
            {
                MessageBox.Show("Giá bán phải là một số hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int drinkId = (int)cbDrink.SelectedValue;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE Drink SET DrinkCode = @DrinkCode, ActualPrice = @ActualPrice, IsActive = @IsActive WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", drinkId);
                command.Parameters.AddWithValue("@DrinkCode", txtDrinkCode.Text);
                command.Parameters.AddWithValue("@ActualPrice", Convert.ToDecimal(txtActualPrice.Text));
                command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Cập nhật đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadMenuItems();
                ResetFields();
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
            string temp = drinkName.ToLower();
            temp = Regex.Replace(temp, "[áàảãạâấầẩẫậăắằẳẵặ]", "a");
            temp = Regex.Replace(temp, "[éèẻẽẹêếềểễệ]", "e");
            temp = Regex.Replace(temp, "[íìỉĩị]", "i");
            temp = Regex.Replace(temp, "[óòỏõọôốồổỗộơớờởỡợ]", "o");
            temp = Regex.Replace(temp, "[úùủũụưứừửữự]", "u");
            temp = Regex.Replace(temp, "[ýỳỷỹỵ]", "y");
            temp = Regex.Replace(temp, "[đ]", "d");
            // Bỏ các ký tự đặc biệt, khoảng trắng và không thêm hậu tố
            return Regex.Replace(temp.Replace(" ", ""), "[^a-z0-9]", "");
        }

        private void ResetFields()
        {
            cbDrink.SelectedIndex = -1;
            txtDrinkCode.Clear();
            txtActualPrice.Clear();
            chkIsActive.IsChecked = true;
            dgMenuItems.SelectedItem = null;
        }
    }
}