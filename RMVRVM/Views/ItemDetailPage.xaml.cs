using RMVRVM.ViewModels;
using System.ComponentModel;
using Xamarin.Forms;

namespace RMVRVM.Views
{
    public partial class ItemDetailPage : ContentPage
    {
        public ItemDetailPage()
        {
            InitializeComponent();
            BindingContext = new ItemDetailViewModel();
        }
    }
}