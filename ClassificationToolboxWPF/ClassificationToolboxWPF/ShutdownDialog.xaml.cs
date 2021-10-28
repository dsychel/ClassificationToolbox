using System;
using System.Windows;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ShutdownDialog.xaml
    /// </summary>
    public partial class ShutdownDialog : Window
    {
        readonly System.Windows.Threading.DispatcherTimer shutdownTimer = new System.Windows.Threading.DispatcherTimer();
        int remainingTime = 11;

        public ShutdownDialog()
        {
            InitializeComponent();

            shutdownTimer.Tick += ShutdownTimer_Tick;
            shutdownTimer.Interval = new TimeSpan(0, 0, 1);
            shutdownTimer.Start();
        }

        private void ShutdownTimer_Tick(object sender, EventArgs e)
        {
            remainingTime--;
            messageLabel.Content = "System will shutdown in " + remainingTime + "s.";

            if (remainingTime == 0)
            {
                this.DialogResult = true;
                this.Close();

                shutdownTimer.Stop();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            shutdownTimer.Stop();

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            shutdownTimer.Stop();

            this.DialogResult = false;
            this.Close();
        }
    }
}
