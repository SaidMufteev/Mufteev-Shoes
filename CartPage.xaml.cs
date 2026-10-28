using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Mufteev_Shoe
{
    public partial class CartPage : Page
    {
        public CartPage()
        {
            InitializeComponent();
        }

        private void UpdateCartInfo()
        {
            DGCart.ItemsSource = null;
            DGCart.ItemsSource = Manager.SelectedItemsInCart;

            decimal total = Manager.SelectedItemsInCart.Sum(item => item.Product.price * item.Quantity);
            TBlockTotalSum.Text = $"Итого к оплате: {total:N0} руб.";
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                UpdateCartInfo();
            }
        }

        private void BtnConfirmOrder_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder errors = new StringBuilder();

            foreach (var item in Manager.SelectedItemsInCart)
            {
                if (item.Quantity <= 0)
                {
                    errors.AppendLine($"Количество для товара «{item.Product.title}» должно быть больше 0!");
                    continue;
                }

                var stock = MufteevShoeDBEntities.GetContext().stock_items.FirstOrDefault(s =>
                    s.product_id == item.Product.id && s.size_id == item.SelectedSize.id);

                if (stock == null || stock.quantity < item.Quantity)
                {
                    int available = stock != null ? stock.quantity : 0;
                    errors.AppendLine($"Недостаточно товара «{item.Product.title}» ({item.SelectedSize.size_value} размер)! В наличии: {available} шт.");
                }
            }

            if (errors.Length > 0)
            {
                MessageBox.Show(errors.ToString(), "Ошибка оформления", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            orders newOrder = new orders();
            newOrder.order_date = DateTime.Now;
            newOrder.total_sum = Manager.SelectedItemsInCart.Sum(item => item.Product.price * item.Quantity);

            // Если заказ оформляет залогиненный клиент — пишем его ID. 
            // Если программа запущена в обход авторизации (тестовый режим) — пишем ID = 1 (Иванова), чтобы СУБД не ругалась на NOT NULL.
            newOrder.user_id = Manager.CurrentUser != null ? Manager.CurrentUser.id : 1;

            MufteevShoeDBEntities.GetContext().orders.Add(newOrder);

            try
            {
                MufteevShoeDBEntities.GetContext().SaveChanges();

                foreach (var item in Manager.SelectedItemsInCart)
                {
                    var stock = MufteevShoeDBEntities.GetContext().stock_items.FirstOrDefault(s =>
                        s.product_id == item.Product.id && s.size_id == item.SelectedSize.id);

                    order_details detail = new order_details();
                    detail.order_id = newOrder.id;
                    detail.product_item_id = stock.id;
                    detail.quantity = item.Quantity;

                    MufteevShoeDBEntities.GetContext().order_details.Add(detail);

                    stock.quantity -= item.Quantity;
                }

                MufteevShoeDBEntities.GetContext().SaveChanges();

                MessageBox.Show($"Заказ №{newOrder.id} успешно оформлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                Manager.SelectedItemsInCart.Clear();
                Manager.MainFrame.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка СУБД при записи заказа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
