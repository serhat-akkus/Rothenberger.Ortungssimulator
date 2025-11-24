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
using Rothenberger.Ortungssimulator.ViewModels;

namespace Rothenberger.Ortungssimulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // DataContext wird in XAML gesetzt
        }

        private void SimCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            var pos = e.GetPosition(SimCanvas);
            vm?.OnCanvasMouseMove(pos.X, pos.Y);
        }

        private void SimCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            var pos = e.GetPosition(SimCanvas);
            vm?.CanvasClickCommand.Execute(new System.Tuple<double, double>(pos.X, pos.Y));
        }
    }
}