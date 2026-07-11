using System.Windows;

namespace GameTranslator
{
    public partial class CustomAlertWindow : Window
    {
        public CustomAlertWindow(string title, string message)
        {
            InitializeComponent();
            TxtTitle.Text = title;
            TxtMessage.Text = message;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}