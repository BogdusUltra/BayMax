using System.Windows;

namespace BayMax
{
    public partial class PinWindow : Window
    {
        public string ResultPin { get; private set; }

        public PinWindow()
        {
            InitializeComponent();
            PinInput.Focus(); // Сразу ставим курсор в поле ввода
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            ResultPin = PinInput.Password; // Забираем введенный PIN
            DialogResult = true; // Закрываем окно с положительным результатом
        }
    }
}