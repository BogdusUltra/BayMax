using BayMax.Services;
using NetMQ;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using BayMax.Models;

namespace BayMax
{
    public partial class MainWindow : Window
    {

        public BayMaxCore Core;
        
        public MainWindow()
        {
            InitializeComponent();
        
            Nodes.NodeRegistry.Initialize();
            Core = new BayMaxCore();

            TopMenu.BindCore(Core);
            TopMenu.BindCanvas(MainEditor);

            MainEditor.BindCore(Core);

            //LoggerService.OnLog += OnLogReceived;
        }

        protected override void OnClosed(EventArgs e)
        {
            Core.Shutdown();
            base.OnClosed(e);
            Environment.Exit(0);
        }
    }
}