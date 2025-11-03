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
            // Đăng ký sự kiện IsVisibleChanged để tải lại dữ liệu mỗi khi view được hiển thị
            this.IsVisibleChanged += DrinkView_IsVisibleChanged;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // Tải dữ liệu lần đầu
            LoadData();
        }

        private void DrinkView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Nếu UserControl trở nên sichtbar (visible), tải lại dữ liệu
            if ((bool)e.NewValue)
            {
                LoadData();
            }
        }

        private void LoadData()
        {
            LoadDrinksToComboBox();
            LoadDrinks();
        }

        private void LoadDrinksToComboBox()
        {
            // Lấy cả DrinkCode để hiển thị khi chọn
            const string query = "SELECT ID, Name, DrinkCode FROM Drink WHERE IsActive = 1 ORDER BY Name";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable drinkListTable = new DataTable();
                adapter.Fill(drinkListTable);
                cbDrink.ItemsSource = drinkListTable.DefaultView;
            }
        }

        private void LoadDrinks()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT 
                        d.ID, (d.DrinkCode + '_NB') AS DrinkCode, d.Name, d.OriginalPrice, d.ActualPrice, d.IsActive, d.CategoryID,
                        ISNULL(c.Name, 'N/A') AS CategoryName 
                    FROM Drink d
                    LEFT JOIN Category c ON d.CategoryID = c.ID
                    -- Thay đổi logic: hiển thị đồ uống nếu nó có giá nhập nguyên bản > 0
                    WHERE d.OriginalPrice > 0"; 

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
                txtPrice.Text = Convert.ToDecimal(row["OriginalPrice"]).ToString("G0"); // Bỏ phần thập phân .00
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
                const string query = "UPDATE Drink SET OriginalPrice = @OriginalPrice, ActualPrice = @ActualPrice, IsActive = @IsActive WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                AddParameters(command, drinkId); // Truyền ID vào

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Cập nhật đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadDrinks();
                ResetFields();
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
                // Hiển thị mã đồ uống gốc khi người dùng chọn từ ComboBox
                txtDrinkCode.Text = (selectedDrink["DrinkCode"] as string ?? "") + "_NB";
            }
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
            command.Parameters.AddWithValue("@OriginalPrice", Convert.ToDecimal(txtPrice.Text));
            command.Parameters.AddWithValue("@ActualPrice", Convert.ToDecimal(txtActualPrice.Text));
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }
    }
}