using System;
using System.Text;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Mufteev_Shoe
{
    public partial class AddEditPage : Page
    {
        private products _currentProduct = new products();

        public AddEditPage(products selectedProduct)
        {
            InitializeComponent();

            if (selectedProduct != null)
            {
                _currentProduct = selectedProduct;
            }
            else
            {
                var allSizes = MufteevShoeDBEntities.GetContext().sizes.ToList();
                foreach (var size in allSizes)
                {
                    _currentProduct.stock_items.Add(new stock_items
                    {
                        size_id = size.id,
                        quantity = 0
                    });
                }
            }

            DataContext = _currentProduct;
            DGStock.ItemsSource = _currentProduct.stock_items.ToList();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder errors = new StringBuilder();

            if (string.IsNullOrWhiteSpace(_currentProduct.title))
                errors.AppendLine("Укажите название товара!");

            if (_currentProduct.price <= 0)
                errors.AppendLine("Стоимость товара должна быть больше нуля!");

            foreach (var stock in _currentProduct.stock_items)
            {
                if (stock.quantity < 0)
                {
                    errors.AppendLine("Количество на складе не может быть отрицательным!");
                    break;
                }
            }

            if (errors.Length > 0)
            {
                MessageBox.Show(errors.ToString(), "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentProduct.id == 0)
            {
                _currentProduct.category_id = 1;
                _currentProduct.subcategory_id = 1;
                _currentProduct.manufacturer_id = 1;

                MufteevShoeDBEntities.GetContext().products.Add(_currentProduct);
            }

            try
            {
                MufteevShoeDBEntities.GetContext().SaveChanges();
                MessageBox.Show("Информация успешно сохранена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                Manager.MainFrame.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
