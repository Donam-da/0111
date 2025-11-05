using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;


namespace namm
{
    public partial class CategoryView : UserControl
    {
        private readonly string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable? categoryDataTable;

        public CategoryView()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadCategoriesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi tải danh mục: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            ResetFields();
        }

        private async Task LoadCategoriesAsync()
        {
            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "SELECT ID, Name, IsActive FROM Category";
                var adapter = new SqlDataAdapter(query, connection);
                categoryDataTable = new DataTable();
                categoryDataTable.Columns.Add("STT", typeof(int));
                categoryDataTable.Columns.Add("StatusText", typeof(string));

                try
                {
                    await Task.Run(() => adapter.Fill(categoryDataTable));
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi SQL khi tải danh mục: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                UpdateStatusText();
                dgCategories.ItemsSource = categoryDataTable.DefaultView;
            }
        }

        private void UpdateStatusText()
        {
            if (categoryDataTable == null) return;
            foreach (DataRow row in categoryDataTable.Rows)
            {   // Xử lý trường hợp IsActive có thể là DBNull
                row["StatusText"] = (row["IsActive"] != DBNull.Value && (bool)row["IsActive"]) ? "Sử dụng" : "Ngưng";
            }
        }

        private void DgCategories_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgCategories.SelectedItem is DataRowView row)
            {
                txtName.Text = row["Name"].ToString();
                chkIsActive.IsChecked = (bool)row["IsActive"];

                btnAdd.IsEnabled = false;
                btnEdit.IsEnabled = true;
                btnDelete.IsEnabled = true;
            }
            else
            {
                ResetFields();
            }
        }

        private async void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Tên loại đồ uống không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "INSERT INTO Category (Name, IsActive) VALUES (@Name, @IsActive)";
                var command = new SqlCommand(query, connection);
                AddParameters(command);

                try
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Thêm loại đồ uống thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadCategoriesAsync();
                    ResetFields();
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi thêm: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgCategories.SelectedItem == null) return;
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Tên loại đồ uống không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var row = (DataRowView)dgCategories.SelectedItem;
            int categoryId = (int)row["ID"];

            using (var connection = new SqlConnection(connectionString))
            {
                const string query = "UPDATE Category SET Name = @Name, IsActive = @IsActive WHERE ID = @ID";
                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", categoryId);
                AddParameters(command);

                try
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                    MessageBox.Show("Cập nhật thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadCategoriesAsync();
                    ResetFields();
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Lỗi khi cập nhật: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgCategories.SelectedItem == null) return;

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa loại đồ uống này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var row = (DataRowView)dgCategories.SelectedItem;
                int categoryId = (int)row["ID"];

                using (var connection = new SqlConnection(connectionString))
                {
                    const string query = "DELETE FROM Category WHERE ID = @ID";
                    var command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", categoryId);
                    try
                    {
                        await connection.OpenAsync();
                        await command.ExecuteNonQueryAsync();
                        MessageBox.Show("Xóa thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                        await LoadCategoriesAsync();
                        ResetFields();
                    }
                    catch (SqlException ex)
                    {
                        MessageBox.Show($"Lỗi khi xóa: {ex.Message}", "Lỗi SQL", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            ResetFields();
        }

        private void ResetFields()
        {
            txtName.Clear();
            chkIsActive.IsChecked = true;
            dgCategories.SelectedItem = null;
            btnAdd.IsEnabled = true;
            btnEdit.IsEnabled = false;
            btnDelete.IsEnabled = false;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (categoryDataTable != null)
            {
                categoryDataTable.DefaultView.RowFilter = $"Name LIKE '%{txtSearch.Text}%'";
            }
        }

        private void AddParameters(SqlCommand command)
        {
            command.Parameters.AddWithValue("@Name", txtName.Text);
            command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);
        }
    }
}