using System.Windows;
using GameTranslator.ViewModels;

namespace GameTranslator
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow(_viewModel.AppSettings)
            {
                Owner = this
            };

            if (settingsWindow.ShowDialog() == true)
            {
                _viewModel.AppSettings = settingsWindow.CurrentSettings;
                _viewModel.SaveCurrentSettings();
            }
        }

        private void BtnSaveTranslation_Click(object sender, RoutedEventArgs e)
        {
            var saveWindow = new SaveOptionsWindow
            {
                Owner = this
            };

            if (saveWindow.ShowDialog() == true)
            {
                if (saveWindow.SelectedAction != SaveAction.None)
                {
                    _viewModel.SaveFile(saveWindow.SelectedAction);
                }
            }
        }

        private void BtnReport_Click(object sender, RoutedEventArgs e)
        {
            var reportWindow = new ReportWindow(_viewModel.LastReport)
            {
                Owner = this
            };
            reportWindow.ShowDialog();
        }
    }
}