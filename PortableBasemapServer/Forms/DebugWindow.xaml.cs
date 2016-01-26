using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for DebugWindow.xaml
    /// </summary>
    public delegate void NotifyFormClosed(object param);
    public partial class DebugWindow : Window
    {
        public event NotifyFormClosed notifyClosing;
        public DebugWindow()
        {
            InitializeComponent();
        }

        private void Func1_Clicked(object sender, RoutedEventArgs e)
        {
            if (notifyClosing != null)
            {
                notifyClosing("Func1");
            }
        }

        private void Func2_Clicked(object sender, RoutedEventArgs e)
        {
            if (notifyClosing != null)
            {
                notifyClosing("Func2");
            }
        }

        private void Func3_Clicked(object sender, RoutedEventArgs e)
        {
            if (notifyClosing != null)
            {
                notifyClosing("Func3");
            }
        }
    }
}
