using System.Windows;
using System.Windows.Controls;

namespace BayMax.UI.Controls
{
    public partial class TopMenuBar : UserControl
    {
        private NodeCanvas _canvas;

        public TopMenuBar()
        {
            InitializeComponent();
        }

        public void BindCanvas(NodeCanvas canvas)
        {
            _canvas = canvas;

            _canvas.SelectionChanged += UpdateMenuState;

            UpdateMenuState();
        }

        private void UpdateMenuState()
        {
            int count = _canvas.SelectedNodes.Count;

            State0_Menu.Visibility = Visibility.Collapsed;
            State1_Menu.Visibility = Visibility.Collapsed;
            StateMulti_Menu.Visibility = Visibility.Collapsed;

            if (count == 0)
            {
                State0_Menu.Visibility = Visibility.Visible;
            }
            else if (count == 1)
            {
                State1_Menu.Visibility = Visibility.Visible;
            }
            else
            {
                MultiSelectText.Text = $"Выбрано нод: {count}";
                StateMulti_Menu.Visibility = Visibility.Visible;
            }
        }

        private void AddUI_Click(object sender, RoutedEventArgs e) => _canvas.AddUINode();
        private void AddLogic_Click(object sender, RoutedEventArgs e) => _canvas.AddLogicNode();
        private void Delete_Click(object sender, RoutedEventArgs e) => _canvas.DeleteSelectedNodes();

        private void OnMenuBackgroundClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            this.Focus();
        }
    }
}