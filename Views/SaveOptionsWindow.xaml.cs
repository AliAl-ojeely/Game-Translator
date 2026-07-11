using System.Windows;

namespace GameTranslator
{
    public enum SaveAction
    {
        None,
        Overwrite,
        SaveAsNew
    }

    public partial class SaveOptionsWindow : Window
    {
        public SaveAction SelectedAction { get; private set; } = SaveAction.None;

        public SaveOptionsWindow()
        {
            InitializeComponent();
        }

        private void BtnOverwrite_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = SaveAction.Overwrite;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = SaveAction.SaveAsNew;
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            SelectedAction = SaveAction.None;
            this.DialogResult = false;
            this.Close();
        }
    }
}