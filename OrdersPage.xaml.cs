using System;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Mufteev_Shoe
{
    /// <summary>
    /// Логика взаимодействия для OrdersPage.xaml
    /// </summary>
    public partial class OrdersPage : Page
    {
        public OrdersPage()
        {
            InitializeComponent();

            // Загружаем списки сразу при инициализации страницы
            RefreshOrders();
            LoadStockItemsCombo();
        }

        /// <summary>
        /// 1. МЕТОД ЗАГРУЗКИ СПИСКА ВСЕХ ЗАКАЗОВ ИЗ БД
        /// </summary>
        private void RefreshOrders()
        {
            // Отключаем ленивую загрузку на время выгрузки, чтобы "битые" записи не ломали вывод
            MufteevShoeDBEntities.GetContext().Configuration.LazyLoadingEnabled = false;

            // Загружаем заказы вместе со связанными клиентами
            var allOrders = MufteevShoeDBEntities.GetContext().orders.Include("users").ToList();

            MufteevShoeDBEntities.GetContext().Configuration.LazyLoadingEnabled = true;

            // Выводим данные в верхнюю таблицу на экране
            DGOrders.ItemsSource = allOrders;
            DGOrderDetails.ItemsSource = null; // Очищаем нижнюю таблицу состава
        }

        /// <summary>
        /// 2. МЕТОД ЗАГРУЗКИ ОБУВИ И РАЗМЕРОВ В ВЫПАДАЮЩИЙ СПИСОК
        /// </summary>
        private void LoadStockItemsCombo()
        {
            // Формируем понятный список: "Название обуви (Размер: ХХ) — В наличии: Х шт."
            var items = MufteevShoeDBEntities.GetContext().stock_items.ToList().Select(s => new
            {
                StockObject = s,
                DisplayTitle = $"{s.products.title} (Размер: {s.sizes.size_value}) — В наличии: {s.quantity} шт."
            }).ToList();

            ComboAddStockItem.ItemsSource = items;
            ComboAddStockItem.DisplayMemberPath = "DisplayTitle";
            ComboAddStockItem.SelectedValuePath = "StockObject";
        }

        /// <summary>
        /// 3. СЛУЖЕБНОЕ СОБЫТИЕ: Срабатывает, когда страница открывается на экране
        /// </summary>
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                // Перечитываем кэш СУБД и обновляем списки
                MufteevShoeDBEntities.GetContext().ChangeTracker.Entries().ToList().ForEach(o => o.Reload());
                RefreshOrders();
                LoadStockItemsCombo();

                // РАЗГРАНИЧЕНИЕ ПО ТЗ: Изменять состав чека может ТОЛЬКО Администратор (role_id == 1)
                if (Manager.CurrentUser != null && Manager.CurrentUser.role_id == 1)
                {
                    ColQuantity.IsReadOnly = false;
                    BtnDeleteDetail.Visibility = Visibility.Visible;
                    PanelAddPosition.Visibility = Visibility.Visible;
                }
                else
                {
                    // Менеджеры и гости изменять состав заказа не имеют права по ТЗ
                    ColQuantity.IsReadOnly = true;
                    BtnDeleteDetail.Visibility = Visibility.Collapsed;
                    PanelAddPosition.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// 4. ПРОСМОТР СОСТАВА КОНКРЕТНОГО ЗАКАЗА ПРИ КЛИКЕ ПО СТРОКЕ ТАБЛИЦЫ
        /// </summary>
        private void DGOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedOrder = DGOrders.SelectedItem as orders;
            if (selectedOrder != null)
            {
                // Фильтруем таблицу деталей по ID выбранного нами заказа
                var details = MufteevShoeDBEntities.GetContext().order_details
                    .Where(d => d.order_id == selectedOrder.id).ToList();

                DGOrderDetails.ItemsSource = details;
            }
        }

        /// <summary>
        /// 5. ДОБАВЛЕНИЕ НОВОЙ ТОВАРНОЙ ПОЗИЦИИ В СОСТАВ ТЕКУЩЕГО ЗАКАЗА ПО ТЗ
        /// </summary>
        private void BtnAddPositionToOrder_Click(object sender, RoutedEventArgs e)
        {
            var selectedOrder = DGOrders.SelectedItem as orders;
            if (selectedOrder == null)
            {
                MessageBox.Show("Пожалуйста, сначала выберите заказ в верхней таблице!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ComboAddStockItem.SelectedValue == null)
            {
                MessageBox.Show("Выберите модель обуви и нужный размер из выпадающего списка!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Получаем выбранную ячейку склада (сочетание модель + размер)
            var targetStock = ComboAddStockItem.SelectedValue as stock_items;

            // Ищем, нет ли уже точно такой же обуви этого же размера в данном заказе
            var existingDetail = MufteevShoeDBEntities.GetContext().order_details
                .FirstOrDefault(d => d.order_id == selectedOrder.id && d.product_item_id == targetStock.id);

            if (existingDetail != null)
            {
                // Если обувь уже добавлена в чек — увеличиваем количество на 1
                existingDetail.quantity++;
            }
            else
            {
                // Если такой пары в чеке еще нет — создаем новую строчку в order_details
                order_details newDetail = new order_details
                {
                    order_id = selectedOrder.id,
                    product_item_id = targetStock.id,
                    quantity = 1
                };
                MufteevShoeDBEntities.GetContext().order_details.Add(newDetail);
            }

            // Сохраняем новую строку в базу и пересчитываем общую итоговую сумму заказа
            MufteevShoeDBEntities.GetContext().SaveChanges();
            RecalculateOrderTotal(selectedOrder);

            MessageBox.Show("Товарная позиция успешно добавлена в состав заказа!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 6. АВТОМАТИЧЕСКИЙ ПЕРЕСЧЕТ СУММЫ ПРИ ИЗМЕНЕНИИ КОЛИЧЕСТВА ПАР В ТАБЛИЦЕ АДМИНИСТРАТОРОМ
        /// </summary>
        private void DGOrderDetails_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            var selectedOrder = DGOrders.SelectedItem as orders;
            if (selectedOrder == null) return;

            // Вызываем пересчет стоимости с микро-задержкой, чтобы ячейка успела обновить значение
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                RecalculateOrderTotal(selectedOrder);
            }));
        }

        /// <summary>
        /// 7. УДАЛЕНИЕ ОДНОЙ КОНКРЕТНОЙ ПОЗИЦИИ ОБУВИ ИЗ СОСТАВА ЧЕКА (Администратор)
        /// </summary>
        private void BtnDeleteDetail_Click(object sender, RoutedEventArgs e)
        {
            var selectedOrder = DGOrders.SelectedItem as orders;
            var selectedDetail = DGOrderDetails.SelectedItem as order_details;

            if (selectedOrder == null || selectedDetail == null)
            {
                MessageBox.Show("Выберите заказ сверху и конкретную строчку обуви снизу для её удаления!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить «{selectedDetail.stock_items.products.title}» из этого заказа?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                // Удаляем строчку состава из БД и запускаем автоматический пересчет стоимости заказа
                MufteevShoeDBEntities.GetContext().order_details.Remove(selectedDetail);
                MufteevShoeDBEntities.GetContext().SaveChanges();

                RecalculateOrderTotal(selectedOrder);
                MessageBox.Show("Позиция успешно удалена из состава заказа!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// ВСПОМОГАТЕЛЬНЫЙ МЕТОД: Рассчитывает сумму заказа на основе таблицы order_details и сохраняет в БД
        /// </summary>
        private void RecalculateOrderTotal(orders order)
        {
            // Считаем сумму по формуле: Количество * Цена товара
            var details = MufteevShoeDBEntities.GetContext().order_details.Where(d => d.order_id == order.id).ToList();
            order.total_sum = details.Sum(d => d.quantity * d.stock_items.products.price);

            // Синхронизируем изменения с MS SQL Server
            MufteevShoeDBEntities.GetContext().SaveChanges();

            // Перерисовываем элементы таблиц на экране
            DGOrders.Items.Refresh();
            DGOrderDetails.ItemsSource = details;
            LoadStockItemsCombo(); // Обновляем остатки в комбобоксе
        }

        /// <summary>
        /// 8. ДОБАВЛЕНИЕ НОВОГО ПУСТОГО ЗАКАЗА В СИСТЕМУ СОТРУДНИКОМ
        /// </summary>
        private void BtnCreateOrder_Click(object sender, RoutedEventArgs e)
        {
            orders manualOrder = new orders
            {
                order_date = DateTime.Now,
                user_id = 1, // По дефолту привязываем к первому клиенту в базе
                total_sum = 0
            };

            MufteevShoeDBEntities.GetContext().orders.Add(manualOrder);
            MufteevShoeDBEntities.GetContext().SaveChanges();

            MessageBox.Show($"Новый пустой заказ №{manualOrder.id} успешно сгенерирован!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshOrders();
        }

        /// <summary>
        /// 9. УДАЛЕНИЕ ВСЕГО ЗАКАЗА ЦЕЛИКОМ ИЗ СИСТЕМЫ (Доступно Менеджеру и Админу)
        /// </summary>
        private void BtnDeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            var selectedOrder = DGOrders.SelectedItem as orders;
            if (selectedOrder == null)
            {
                MessageBox.Show("Пожалуйста, выберите заказ для удаления!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show($"Вы уверены, что хотите полностью стереть заказ № {selectedOrder.id} из базы данных?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    // Очищаем зависимые строки состава из order_details, чтобы не нарушать связи NOT NULL в СУБД
                    var details = MufteevShoeDBEntities.GetContext().order_details.Where(d => d.order_id == selectedOrder.id).ToList();
                    MufteevShoeDBEntities.GetContext().order_details.RemoveRange(details);
                    // Удаляем сам заказ
                    MufteevShoeDBEntities.GetContext().orders.Remove(selectedOrder);
                    MufteevShoeDBEntities.GetContext().SaveChanges();
                    MessageBox.Show("Заказ успешно удален из системы!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshOrders();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка СУБД при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}