using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Mufteev_Shoe
{
    public partial class ProductPage : Page
    {
        public ProductPage()
        {
            InitializeComponent();

            var allManufacturers = MufteevShoeDBEntities.GetContext().manufacturers.ToList();
            allManufacturers.Insert(0, new manufacturers { manufacturer_name = "Все производители" });
            ComboFilter.ItemsSource = allManufacturers;

            ComboSort.SelectedIndex = 0;
            ComboFilter.SelectedIndex = 0;

            UpdateProducts();
            CheckRoles();
        }

        private void CheckRoles()
        {
            if (Manager.CurrentUser == null)
            {
                PanelFilters.Visibility = Visibility.Collapsed;
                BtnAddProduct.Visibility = Visibility.Collapsed;
                BtnViewOrders.Visibility = Visibility.Collapsed;
            }
            else if (Manager.CurrentUser.role_id == 3)
            {
                PanelFilters.Visibility = Visibility.Visible;
                BtnGoToCart.Visibility = Visibility.Visible;
                BtnAddProduct.Visibility = Visibility.Collapsed;
                BtnViewOrders.Visibility = Visibility.Collapsed;
            }
            else
            {
                PanelFilters.Visibility = Visibility.Visible;
                BtnGoToCart.Visibility = Visibility.Visible;
                BtnAddProduct.Visibility = Visibility.Visible;
                BtnViewOrders.Visibility = Visibility.Visible; // Видят Менеджеры и Админы
            }
        }

        private void PanelCartControls_Loaded(object sender, RoutedEventArgs e)
        {
            StackPanel panel = sender as StackPanel;
            if (Manager.CurrentUser == null)
                panel.Visibility = Visibility.Collapsed;
            else
                panel.Visibility = Visibility.Visible;
        }

        private void BtnEditProduct_Loaded(object sender, RoutedEventArgs e)
        {
            Button btnEdit = sender as Button;
            if (Manager.CurrentUser != null && (Manager.CurrentUser.role_id == 1 || Manager.CurrentUser.role_id == 2))
                btnEdit.Visibility = Visibility.Visible;
            else
                btnEdit.Visibility = Visibility.Collapsed;
        }

        private void UpdateProducts()
        {
            var currentProducts = MufteevShoeDBEntities.GetContext().products.ToList();

            if (ComboFilter.SelectedIndex > 0)
            {
                var selectedManufacturer = ComboFilter.SelectedItem as manufacturers;
                if (selectedManufacturer != null)
                {
                    currentProducts = currentProducts.Where(p => p.manufacturer_id == selectedManufacturer.id).ToList();
                }
            }

            if (!string.IsNullOrWhiteSpace(TBoxSearch.Text))
            {
                string searchText = TBoxSearch.Text.ToLower();
                currentProducts = currentProducts.Where(p => p.title.ToLower().Contains(searchText)).ToList();
            }

            if (ComboSort.SelectedIndex == 1)
            {
                currentProducts = currentProducts.OrderBy(p => p.price).ToList();
            }
            else if (ComboSort.SelectedIndex == 2)
            {
                currentProducts = currentProducts.OrderByDescending(p => p.price).ToList();
            }

            ProductListView.ItemsSource = null;
            ProductListView.ItemsSource = currentProducts;
        }

        private void BtnAddToCart_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var currentProduct = button.DataContext as products;

            var parentStackPanel = button.Parent as StackPanel;
            var comboSizes = parentStackPanel.Children.OfType<ComboBox>().FirstOrDefault();

            if (comboSizes == null || comboSizes.SelectedItem == null)
            {
                MessageBox.Show("Пожалуйста, выберите размер обуви!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedStock = comboSizes.SelectedItem as stock_items;
            var targetSize = selectedStock.sizes;

            var existingItem = Manager.SelectedItemsInCart.FirstOrDefault(item =>
                item.Product.id == currentProduct.id && item.SelectedSize.id == targetSize.id);

            if (existingItem != null)
            {
                existingItem.Quantity++;
            }
            else
            {
                Manager.SelectedItemsInCart.Add(new Manager.CartItem
                {
                    Product = currentProduct,
                    SelectedSize = targetSize,
                    Quantity = 1
                });
            }

            MessageBox.Show($"Товар добавен в корзину!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAddProduct_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new AddEditPage(null));
        }

        private void BtnEditProduct_Click(object sender, RoutedEventArgs e)
        {
            var currentProduct = (sender as Button).DataContext as products;
            Manager.MainFrame.Navigate(new AddEditPage(currentProduct));
        }

        private void BtnGoToCart_Click(object sender, RoutedEventArgs e)
        {
            if (Manager.SelectedItemsInCart.Count == 0)
            {
                MessageBox.Show("Ваша корзина пуста!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Manager.MainFrame.Navigate(new CartPage());
        }

        private void BtnViewOrders_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new OrdersPage()); // Переход на управление заказами
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                MufteevShoeDBEntities.GetContext().ChangeTracker.Entries().ToList().ForEach(p => p.Reload());
                UpdateProducts();
                CheckRoles();
            }
        }

        private void TBoxSearch_TextChanged(object sender, TextChangedEventArgs e) => UpdateProducts();
        private void ComboSort_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateProducts();
        private void ComboFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateProducts();
    }
}
