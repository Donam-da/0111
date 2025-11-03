using System.Windows;
using System.Windows.Input;

namespace namm
{
    public partial class AddDrinkDialog : Window
    {
        public int Quantity { get; private set; }

        public AddDrinkDialog(string drinkName)
        {
            InitializeComponent();
            tbDrinkName.Text = drinkName;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtQuantity.Text, out int quantity) && quantity > 0)
            {
                this.Quantity = quantity;
                this.DialogResult = true;
            }
            else
            {
                MessageBox.Show("Số lượng phải là một số nguyên dương.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            txtQuantity.Focus();
            txtQuantity.SelectAll();
        }

        private void TxtQuantity_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnOk_Click(sender, e);
            }
        }
    }
}