using System.Windows;
using System.Windows.Controls;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for FeaturesPostprocessor.xaml
    /// </summary>
    public partial class FeaturesPostprocessor : UserControl
    {
        public FeaturesPostprocessor()
        {
            InitializeComponent();

            GlobalFunctions.InitializePath(ref inputRemoveTextbox, "forRemovingFeaturesPath");
        }

        private void SelectSamplesForRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalFunctions.SelectFeatures(ref inputRemoveTextbox, "forRemovingFeaturesPath");
        }

        private void SamplesRemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (inputRemoveTextbox.Text == "")
            {
                MessageBox.Show("Features path is empty or file doesn't exist.");
                return;
            }

            if (MessageBox.Show("Are you sure that you want to delete samples from selected file?", "Samples Removing", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                bool removeNegative = (negativeRemovingRadioButton.IsChecked == null || negativeRemovingRadioButton.IsChecked == false) ? false : true; ;
                bool removePositive = (positiveRemovingRadioButton.IsChecked == null || positiveRemovingRadioButton.IsChecked == false) ? false : true; ;
                string removePath = inputRemoveTextbox.Text;
                NativeMethods.RemoveSamples(removePath, removeNegative, removePositive);
            }
        }
    }
}
