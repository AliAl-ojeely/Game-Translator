using System.Windows;
using GameTranslator.ViewModels;

namespace GameTranslator.Views
{
    public partial class FindAndReplaceWindow : Window
    {
        public FindAndReplaceWindow(MainViewModel vm)
        {
            InitializeComponent();
            this.DataContext = vm;
        }
    }
}