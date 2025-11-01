using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace namm
{
    public partial class TableListView : UserControl
    {
        private string connectionString = ConfigurationManager.ConnectionStrings["CafeDB"].ConnectionString;

        public TableListView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTableList();
        }

        void LoadTableList()
        {
            wpTables.Children.Clear();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT Name, Status FROM TableFood";
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string tableName = reader["Name"].ToString();
                    string status = reader["Status"].ToString();

                    Button tableButton = new Button
                    {
                        Content = tableName + Environment.NewLine + (status == "Trống" ? "(Trống)" : "(Có người)"),
                        Width = 120,
                        Height = 120,
                        Margin = new Thickness(10),
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Tag = tableName // Lưu tên bàn để xử lý sau này
                    };

                    // Đặt màu nền dựa trên trạng thái
                    tableButton.Background = status == "Trống" ? Brushes.LightGreen : Brushes.LightCoral;

                    wpTables.Children.Add(tableButton);
                }
            }
        }
    }
}