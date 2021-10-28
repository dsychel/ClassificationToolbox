using System.Windows;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ParallelitySettings.xaml
    /// </summary>
    public partial class ParallelitySettings : Window
    {
        public ParallelitySettings()
        {
            InitializeComponent();

            openMPthreadsNumericUpDown.Value = Properties.Settings.Default.openMPthreads;
            progressBufferNumericUpDown.Value = Properties.Settings.Default.progressBuffer;
            tplMaxThreadsNumericUpDown.Value = Properties.Settings.Default.tplThreads;
        }

        private void TplMaxThreadsNumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            decimal previousValue = (decimal)e.PreviousValue;
            decimal currentValue = (decimal)e.CurrentValue;

            if (previousValue > 0 && currentValue == 0)
                tplMaxThreadsNumericUpDown.Value = -1;
            else if (previousValue == -1 && currentValue == 0)
                tplMaxThreadsNumericUpDown.Value = 1;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.openMPthreads = (int)openMPthreadsNumericUpDown.Value;
            Properties.Settings.Default.progressBuffer = (int)progressBufferNumericUpDown.Value;
            if ((int)tplMaxThreadsNumericUpDown.Value != 0)
                Properties.Settings.Default.tplThreads = (int)tplMaxThreadsNumericUpDown.Value;
            Properties.Settings.Default.Save();

            NativeMethods.SetParallelity((int)Properties.Settings.Default.openMPthreads,
                (int)Properties.Settings.Default.progressBuffer);

            this.Close();
        }
    }
}
