using System.Threading.Tasks;
using System.Windows;
using GameTranslator.ViewModels;

namespace GameTranslator
{
    public partial class ReportWindow : Window
    {
        public ReportWindow(TranslationReport report)
        {
            InitializeComponent();

            TxtFileName.Text = $"File: {report.FileName}";
            TxtTimeTaken.Text = $"Total Time: {report.TimeTaken:hh\\:mm\\:ss}";
            TxtTranslatedCount.Text = $"Items Translated: {report.TranslatedCount}";
            TxtCharCount.Text = $"Total Characters Translated: {report.TranslatedCharacters}";
            TxtSkippedCount.Text = $"Items Skipped: {report.SkippedStringIds.Count}";

            ListSkippedIds.ItemsSource = report.SkippedStringIds;
        }

        private async void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (ListSkippedIds.SelectedItem != null)
            {
                Clipboard.SetText(ListSkippedIds.SelectedItem.ToString());

                BtnCopy.Content = "Copied!";
                BtnCopy.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#28A745"));

                await Task.Delay(1500);

                BtnCopy.Content = "Copy Selected";
                BtnCopy.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E"));
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}