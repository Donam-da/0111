using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace namm
{
    public partial class UnitView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;
        private DataTable unitDataTable = new DataTable();

        public UnitView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUnits();
        }

        private void LoadUnits()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT ID, Name, Abbreviation, Description, IsActive FROM Unit";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                unitDataTable = new DataTable();
                unitDataTable.Columns.Add("STT", typeof(int));
                unitDataTable.Columns.Add("StatusText", typeof(string)); // Cột ảo để hiển thị text trạng thái
                adapter.Fill(unitDataTable);

                UpdateStatusText();
                dgUnits.ItemsSource = unitDataTable.DefaultView;
            }
        }

        private void DgUnits_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }

        private void DgUnits_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgUnits.SelectedItem is DataRowView row)
            {
                txtName.Text = row["Name"].ToString();
                txtAbbreviation.Text = row["Abbreviation"].ToString();
                txtDescription.Text = row["Description"].ToString();
                chkIsActive.IsChecked = (bool)row["IsActive"];
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Tên đơn vị tính không được để trống.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "INSERT INTO Unit (Name, Abbreviation, Description, IsActive) VALUES (@Name, @Abbreviation, @Description, @IsActive)";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@Name", txtName.Text);
                command.Parameters.AddWithValue("@Abbreviation", string.IsNullOrWhiteSpace(txtAbbreviation.Text) ? (object)DBNull.Value : txtAbbreviation.Text);
                command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text);
                command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);


                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Thêm đơn vị tính thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUnits();
                ResetFields();
            }
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (dgUnits.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đơn vị tính để sửa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DataRowView row = (DataRowView)dgUnits.SelectedItem;
            int unitId = (int)row["ID"];

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "UPDATE Unit SET Name = @Name, Abbreviation = @Abbreviation, Description = @Description, IsActive = @IsActive WHERE ID = @ID";
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@ID", unitId);
                command.Parameters.AddWithValue("@Name", txtName.Text);
                command.Parameters.AddWithValue("@Abbreviation", string.IsNullOrWhiteSpace(txtAbbreviation.Text) ? (object)DBNull.Value : txtAbbreviation.Text);
                command.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(txtDescription.Text) ? (object)DBNull.Value : txtDescription.Text);
                command.Parameters.AddWithValue("@IsActive", chkIsActive.IsChecked ?? false);

                connection.Open();
                command.ExecuteNonQuery();
                MessageBox.Show("Cập nhật đơn vị tính thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadUnits();
                ResetFields();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgUnits.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn một đơn vị tính để xóa.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Bạn có chắc chắn muốn xóa đơn vị tính này?", "Xác nhận xóa", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                DataRowView row = (DataRowView)dgUnits.SelectedItem;
                int unitId = (int)row["ID"];

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    string query = "DELETE FROM Unit WHERE ID = @ID";
                    SqlCommand command = new SqlCommand(query, connection);
                    command.Parameters.AddWithValue("@ID", unitId);
                    connection.Open();
                    command.ExecuteNonQuery();
                    MessageBox.Show("Xóa đơn vị tính thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUnits();
                    ResetFields();
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
            txtAbbreviation.Clear();
            txtDescription.Clear();
            chkIsActive.IsChecked = true;
            dgUnits.SelectedItem = null;
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = txtSearch.Text;
            if (unitDataTable != null)
            {
                if (string.IsNullOrEmpty(filter))
                {
                    unitDataTable.DefaultView.RowFilter = "";
                }
                else
                {
                    // Lọc theo tên đơn vị tính
                    unitDataTable.DefaultView.RowFilter = $"Name LIKE '%{filter}%'";
                }
            }
        }

        private void UpdateStatusText()
        {
            foreach (DataRow row in unitDataTable.Rows)
            {
                row["StatusText"] = (bool)row["IsActive"] ? "Kích hoạt" : "Vô hiệu hóa";
            }
        }
    }
}