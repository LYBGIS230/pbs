﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using PBS.APP.ViewModels;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PBS.DataSource;

namespace PBS.APP
{
    /// <summary>
    /// Interaction logic for GoogleCorrect.xaml
    /// </summary>
    public partial class GoogleCorrect : Window
    {
        public GoogleCorrect()
        {
            InitializeComponent();
            VMConvertMBtiles vm = new VMConvertMBtiles();
            this.DataContext = vm;
        }
    }
}
