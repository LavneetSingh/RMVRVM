using RMVRVM.Services;
using RMVRVM.Views;
using System;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace RMVRVM
{
    public partial class App : Application
    {

        public App(string downloadsFolder)
        {
            InitializeComponent();
            Downloads = downloadsFolder;
            DependencyService.Register<MockDataStore>();
            MainPage = new AboutPage();
        }

        public string Downloads { get; set; }
    }
}
