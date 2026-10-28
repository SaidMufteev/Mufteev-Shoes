using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Mufteev_Shoe
{
    public partial class AuthPage : Page
    {
        public AuthPage()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string inputLogin = TBoxLogin.Text.Trim();

            if (string.IsNullOrEmpty(inputLogin))
            {
                MessageBox.Show("Пожалуйста, введите логин для входа!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Ищем пользователя в БД по введенному логину
            var user = MufteevShoeDBEntities.GetContext().users.FirstOrDefault(u => u.login == inputLogin);

            if (user != null)
            {
                // КРИТИЧЕСКИ ВАЖНО: сохраняем пользователя, чтобы приложение его не "забывало"
                Manager.CurrentUser = user;

                MessageBox.Show($"Успешный вход! Вы вошли как: {user.first_name} {user.last_name}", "Авторизация", MessageBoxButton.OK, MessageBoxImage.Information);
                Manager.MainFrame.Navigate(new ProductPage());
            }
            else
            {
                MessageBox.Show("Пользователь с таким логином не найден в системе!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnGuest_Click(object sender, RoutedEventArgs e)
        {
            // Для гостя принудительно очищаем переменную в null
            Manager.CurrentUser = null;

            Manager.MainFrame.Navigate(new ProductPage());
        }
    }
}
