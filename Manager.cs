using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Mufteev_Shoe
{
    public static class Manager
    {
        public static Frame MainFrame { get; set; }

        public static users CurrentUser { get; set; }

        public class CartItem
        {
            public products Product { get; set; }   // Сам товар из БД
            public sizes SelectedSize { get; set; }  // Выбранный размер из БД
            public int Quantity { get; set; }       // Сколько штук покупают
        }

        // НОВОЕ: Глобальный список памяти, где будут временно лежать товары корзины
        public static List<CartItem> SelectedItemsInCart { get; set; } = new List<CartItem>();
    }
}
