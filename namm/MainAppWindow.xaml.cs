﻿﻿﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace namm
{
    /// <summary>
    /// Interaction logic for MainAppWindow.xaml
    /// </summary>
    public partial class MainAppWindow : Window
    {
        private AccountDTO loggedInAccount;

        public MainAppWindow(AccountDTO account)
        {
            InitializeComponent();
            this.loggedInAccount = account;
            Authorize();

            // Hiển thị sơ đồ bàn làm màn hình chính
            MainContent.Children.Add(new DashboardView());
        }

        void Authorize()
        {
            // Nếu không phải là admin (Type = 0 là nhân viên)
            if (loggedInAccount.Type == 0)
            {
                miManageEmployees.Visibility = Visibility.Collapsed;
                miDeleteHistory.Visibility = Visibility.Collapsed;
                miProfitStatistics.Visibility = Visibility.Collapsed; // Chỉ ẩn mục Thống kê lợi nhuận
            }
        }

        private void TopLevelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                // Bật/tắt trạng thái mở của menu con
                menuItem.IsSubmenuOpen = !menuItem.IsSubmenuOpen;
            }
        }

        private void ManageEmployees_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện quản lý nhân viên trong Grid chính
            MainContent.Children.Clear();
            MainContent.Children.Add(new EmployeeView());
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            // Tạo một cửa sổ đăng nhập mới
            MainWindow loginWindow = new MainWindow();
            // Hiển thị cửa sổ đăng nhập
            loginWindow.Show();
            // Đóng cửa sổ chính hiện tại
            this.Close();
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện đổi mật khẩu, truyền thông tin tài khoản đang đăng nhập
            var changePasswordView = new ChangePasswordView(loggedInAccount);

            // Lắng nghe sự kiện yêu cầu đăng xuất từ ChangePasswordView
            changePasswordView.LogoutRequested += (s, args) => Logout_Click(s!, null!);

            MainContent.Children.Clear();
            MainContent.Children.Add(changePasswordView);
        }

        private void ManageTables_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện quản lý bàn
            MainContent.Children.Clear();
            MainContent.Children.Add(new TableView());
        }

        private void ManageUnits_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện quản lý đơn vị tính
            MainContent.Children.Clear();
            MainContent.Children.Add(new UnitView());
        }

        private void ManageMaterials_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện quản lý nguyên liệu
            MainContent.Children.Clear();
            MainContent.Children.Add(new MaterialView());
        }

        private void ManageOriginalDrinks_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện quản lý đồ uống nguyên bản
            MainContent.Children.Clear();
            MainContent.Children.Add(new DrinkView());
        }

        private void ManageMenu_Click(object sender, RoutedEventArgs e)
        {
            // Chỉ thực hiện hành động khi người dùng click trực tiếp vào menu cha,
            // không phải khi click vào một menu con bên trong nó.
            if (e.OriginalSource == sender)
            {
                // Hiển thị giao diện quản lý menu đồ uống
                MainContent.Children.Clear();
                MainContent.Children.Add(new MenuView());
            }
        }

        private void ManageRecipes_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện quản lý công thức (đồ uống pha chế)
            MainContent.Children.Clear();
            MainContent.Children.Add(new RecipeView());
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị sơ đồ bàn làm màn hình chính
            MainContent.Children.Clear();
            MainContent.Children.Add(new DashboardView());
        }

        private void ManageCategories_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện quản lý loại đồ uống
            MainContent.Children.Clear();
            MainContent.Children.Add(new CategoryView());
        }

        private void InvoiceHistory_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện lịch sử hóa đơn
            MainContent.Children.Clear();
            MainContent.Children.Add(new InvoiceHistoryView());
        }

        private void LoyalCustomers_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện thống kê khách hàng thân thiết
            MainContent.Children.Clear();
            MainContent.Children.Add(new LoyalCustomerView());
        }

        private void TopSelling_Click(object sender, RoutedEventArgs e)
        {
            // Hiển thị giao diện thống kê top hàng bán chạy
            MainContent.Children.Clear();
            MainContent.Children.Add(new TopSellingItemsView());
        }
    }
}