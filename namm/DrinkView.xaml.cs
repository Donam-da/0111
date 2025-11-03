﻿using System;
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
            LoadDrinksToComboBox();
            LoadDrinks();
        }

        private void LoadDrinksToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name FROM Drink WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable categoryTable = new DataTable();
                adapter.Fill(categoryTable);
                cbDrink.ItemsSource = categoryTable.DefaultView;
            }
        }

        private void LoadDrinks()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT 
                        d.ID, d.DrinkCode, d.Name, d.Price, d.ActualPrice, d.IsActive, d.CategoryID,
                        ISNULL(c.Name, 'N/A') AS CategoryName 
                    FROM Drink d
                    LEFT JOIN Category c ON d.CategoryID = c.ID
                    WHERE d.DrinkCode LIKE '%_NB'"; // Chỉ hiển thị các đồ uống đã được gán là nguyên bản

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
            // Khi chọn từ Grid, đồng bộ lên ComboBox và các trường
            if (dgDrinks.SelectedItem is DataRowView row)
            {
                cbDrink.SelectionChanged -= CbDrink_SelectionChanged; // Tạm ngắt event
                cbDrink.SelectedValue = row["ID"];
                cbDrink.SelectionChanged += CbDrink_SelectionChanged; // Bật lại event

                txtDrinkCode.Text = row["DrinkCode"] as string ?? string.Empty;
                txtPrice.Text = Convert.ToDecimal(row["Price"]).ToString("G0"); // Bỏ phần thập phân .00
                txtActualPrice.Text = Convert.ToDecimal(row["ActualPrice"]).ToString("G0"); // Bỏ phần thập phân .00
                chkIsActive.IsChecked = (bool)row["IsActive"];
                cbDrink.IsEnabled = false; // Không cho đổi đồ uống khi đang sửa
            }
        }

        // Đổi tên BtnAdd và BtnEdit thành một nút BtnSave duy nhất
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (cbDrink.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để gán thuộc tính.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ValidateInput()) return;

            int drinkId = (int)cbDrink.SelectedValue;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Luôn là UPDATE, không có INSERT ở màn hình này
                const string query = "UPDATE Drink SET DrinkCode = @DrinkCode, Price = @Price, ActualPrice = @ActualPrice, IsActive = @IsActive WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command, drinkId); // Truyền ID vào

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
                MessageBox.Show("Vui lòng chọn một đồ uống từ danh sách để gỡ thuộc tính.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn gỡ bỏ thuộc tính 'nguyên bản' của đồ uống này không? Hành động này sẽ xóa mã _NB và đặt giá nhập về 0.", "Xác nhận", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgDrinks.SelectedItem;
                int drinkId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    // Thay vì xóa, chúng ta cập nhật để loại bỏ thuộc tính "nguyên bản"
                    // Xóa DrinkCode và đặt giá nhập về 0
                    string query = "UPDATE Drink SET DrinkCode = NULL, Price = 0 WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", drinkId);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Đã gỡ thuộc tính 'nguyên bản' thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void CbDrink_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbDrink.SelectedItem is DataRowView selectedDrink)
            {
                string drinkName = selectedDrink["Name"] as string ?? "";
                txtDrinkCode.Text = GenerateNBCode(drinkName);
            }
        }

        private string GenerateNBCode(string drinkName)
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
            cbDrink.SelectedIndex = -1;
            txtDrinkCode.Clear();
            txtPrice.Clear();
            txtActualPrice.Clear();
            chkIsActive.IsChecked = true;
            dgDrinks.SelectedItem = null;
            cbDrink.IsEnabled = true;
        }

        private bool ValidateInput()
        {
            if (cbDrink.SelectedItem == null ||
                string.IsNullOrWhiteSpace(txtPrice.Text) || string.IsNullOrWhiteSpace(txtActualPrice.Text))
            {
                MessageBox.Show("Vui lòng chọn đồ uống và nhập đầy đủ giá nhập, giá bán.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            if (!decimal.TryParse(txtPrice.Text, out _) || !decimal.TryParse(txtActualPrice.Text, out _))
            {
                MessageBox.Show("Giá nhập và giá bán phải là số.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
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
            command.Parameters.AddWithValue("@Price", Convert.ToDecimal(txtPrice.Text));
            command.Parameters.AddWithValue("@ActualPrice", Convert.ToDecimal(txtActualPrice.Text));
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }
    }
}