using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace namm
{
    public partial class DashboardView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public DashboardView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTables();
        }

        private void LoadTables()
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT Name, Status FROM TableFood";
                SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
                DataTable dataTable = new DataTable();
                dataTable.Columns.Add("STT", typeof(int));
                adapter.Fill(dataTable);
                dgTables.ItemsSource = dataTable.DefaultView;
            }
        }

        private void DgTables_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView rowView)
            {
                // Gán giá trị cho cột STT
                rowView["STT"] = e.Row.GetIndex() + 1;
            }
        }
    }
}