using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class RecipeView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable recipeDataTable = new DataTable();

        public RecipeView()
        {
            InitializeComponent();
            dgRecipe.ItemsSource = recipeDataTable.DefaultView;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadFormulatedDrinksToComboBox();
            LoadMaterialsToComboBox();
            InitializeRecipeDataTable();
        }

        private void InitializeRecipeDataTable()
        {
            recipeDataTable.Columns.Clear();
            recipeDataTable.Columns.Add("MaterialID", typeof(int));
            recipeDataTable.Columns.Add("MaterialName", typeof(string));
            recipeDataTable.Columns.Add("Quantity", typeof(decimal));
            recipeDataTable.Columns.Add("UnitName", typeof(string));
        }

        private void LoadFormulatedDrinksToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Tải tất cả đồ uống để người dùng có thể chọn và thiết lập công thức
                string query = "SELECT ID, Name FROM Drink ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable drinkTable = new DataTable();
                adapter.Fill(drinkTable);
                cbDrink.ItemsSource = drinkTable.DefaultView;
            }
        }

        private void LoadMaterialsToComboBox()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT ID, Name FROM Material WHERE IsActive = 1 ORDER BY Name";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable materialTable = new DataTable();
                adapter.Fill(materialTable);
                cbMaterial.ItemsSource = materialTable.DefaultView;
            }
        }

        private void CbDrink_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            recipeDataTable.Rows.Clear();
            if (cbDrink.SelectedItem == null)
            {
                txtDrinkCode.Clear();
                return;
            }

            string drinkName = ((DataRowView)cbDrink.SelectedItem)["Name"].ToString();
            txtDrinkCode.Text = GenerateRecipeCode(drinkName);

            int drinkId = (int)cbDrink.SelectedValue;
            LoadRecipeForDrink(drinkId);
        }

        private void LoadRecipeForDrink(int drinkId)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = @"
                    SELECT r.MaterialID, m.Name AS MaterialName, r.Quantity, u.Name AS UnitName
                    FROM Recipe r
                     JOIN Material m ON r.MaterialID = m.ID
                     JOIN Unit u ON m.UnitID = u.ID
                    WHERE r.DrinkID = @DrinkID";
                
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                adapter.SelectCommand.Parameters.AddWithValue("@DrinkID", drinkId);
                adapter.Fill(recipeDataTable);
            }
        }

        private void BtnAddIngredient_Click(object sender, RoutedEventArgs e)
        {
            if (cbMaterial.SelectedItem == null || string.IsNullOrWhiteSpace(txtQuantity.Text))
            {
                MessageBox.Show("Vui lòng chọn nguyên liệu và nhập số lượng.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtQuantity.Text, out decimal quantity))
            {
                MessageBox.Show("Số lượng phải là một số hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DataRowView selectedMaterial = (DataRowView)cbMaterial.SelectedItem;
            int materialId = (int)selectedMaterial["ID"];
            string materialName = selectedMaterial["Name"].ToString();

            // Lấy đơn vị tính của nguyên liệu
            string unitName = GetUnitForMaterial(materialId);

            recipeDataTable.Rows.Add(materialId, materialName, quantity, unitName);

            // Reset input
            cbMaterial.SelectedIndex = -1;
            txtQuantity.Clear();
        }

        private string GetUnitForMaterial(int materialId)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT u.Name FROM Material m JOIN Unit u ON m.UnitID = u.ID WHERE m.ID = @MaterialID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@MaterialID", materialId);
                connection.Open();
                return command.ExecuteScalar()?.ToString() ?? string.Empty;
            }
        }

        private void DeleteIngredient_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.CommandParameter is DataRowView rowView)
            {
                recipeDataTable.Rows.Remove(rowView.Row);
            }
        }

        private void BtnSaveRecipe_Click(object sender, RoutedEventArgs e)
        {
            if (cbDrink.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đồ uống để lưu công thức.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int drinkId = (int)cbDrink.SelectedValue;

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlTransaction transaction = connection.BeginTransaction();

                try
                {
                    // Xóa công thức cũ
                    SqlCommand deleteCmd = new SqlCommand("DELETE FROM Recipe WHERE DrinkID = @DrinkID", connection, transaction);
                    deleteCmd.Parameters.AddWithValue("@DrinkID", drinkId);
                    deleteCmd.ExecuteNonQuery();

                    // Thêm công thức mới từ DataGrid
                    foreach (DataRow row in recipeDataTable.Rows)
                    {
                        SqlCommand insertCmd = new SqlCommand("INSERT INTO Recipe (DrinkID, MaterialID, Quantity) VALUES (@DrinkID, @MaterialID, @Quantity)", connection, transaction);
                        insertCmd.Parameters.AddWithValue("@DrinkID", drinkId);
                        insertCmd.Parameters.AddWithValue("@MaterialID", row["MaterialID"]);
                        insertCmd.Parameters.AddWithValue("@Quantity", row["Quantity"]);
                        insertCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                    MessageBox.Show("Lưu công thức thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    MessageBox.Show("Lỗi khi lưu công thức: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string GenerateRecipeCode(string drinkName)
        {
            string temp = drinkName.ToLower();
            temp = Regex.Replace(temp, "[áàảãạâấầẩẫậăắằẳẵặ]", "a");
            temp = Regex.Replace(temp, "[éèẻẽẹêếềểễệ]", "e");
            temp = Regex.Replace(temp, "[íìỉĩị]", "i");
            temp = Regex.Replace(temp, "[óòỏõọôốồổỗộơớờởỡợ]", "o");
            temp = Regex.Replace(temp, "[úùủũụưứừửữự]", "u");
            temp = Regex.Replace(temp, "[ýỳỷỹỵ]", "y");
            temp = Regex.Replace(temp, "[đ]", "d");
            temp = Regex.Replace(temp.Replace(" ", ""), "[^a-z0-9]", "");
            return temp + "_PC"; // Thêm hậu tố _PC
        }
    }
}