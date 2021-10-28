using System.Windows;
using System.Collections.ObjectModel;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for FeatureSelectorWindow.xaml
    /// </summary>
    public partial class FeatureSelectorWindow : Window
    {
        public struct ResamplingSettings
        {
            public bool forceSeeds;
            public int validSeed1;
            public int validSeed2;
            public int trainSeed1;
            public int trainSeed2;

            public bool resizeSetsWhenResampling;
            public int resamplingMaxValSize;
            public int resamplingMaxTrainSize1;
            public int resamplingMaxTrainSize2;

            public int repetitionPerImage;
            public int resScales;
            public int minWindow;
            public double jumpingFactor;
            public double scaleFactor;
        }

        public string ExtractorName => featureSelector.ExtractorName;
        public ReadOnlyDictionary<string, int> Parameters => featureSelector.Parameters;
        public int[] ParametersArray => featureSelector.ParametersArray;
        public string FileName => featureSelector.ExtractorFileName;

        public ResamplingSettings ResamplingParameters
        {
            get
            {
                ResamplingSettings resamplingParameters;
                resamplingParameters.forceSeeds = forceSeedsCheckBox.IsChecked == true;
                resamplingParameters.validSeed1 = (int)valStartSeedNumericUpDown.Value;
                resamplingParameters.validSeed2 = (int)valStepSeedNumericUpDown.Value;
                resamplingParameters.trainSeed1 = (int)trainStartSeedNumericUpDown.Value;
                resamplingParameters.trainSeed2 = (int)trainStepSeedNumericUpDown.Value;

                resamplingParameters.resizeSetsWhenResampling = resizeSetsWhenResamplingCheckBox.IsChecked == true;
                resamplingParameters.resamplingMaxValSize = (int)resamplingMaxValSizeNumericUpDown.Value;
                resamplingParameters.resamplingMaxTrainSize1 = (int)resamplingMaxTrainSize1NumericUpDown.Value;
                resamplingParameters.resamplingMaxTrainSize2 = (int)resamplingMaxTrainSize2NumericUpDown.Value;

                resamplingParameters.repetitionPerImage = (int)repetitionPerImageNumericUpDown.Value;
                resamplingParameters.resScales = (int)scalesNumericUpDown.Value;
                resamplingParameters.minWindow = (int)minWindowNumericUpDown.Value;
                resamplingParameters.jumpingFactor = (double)jumpingFactorNumericUpDown.Value;
                resamplingParameters.scaleFactor = (double)scaleFactorNumericUpDown.Value;

                return resamplingParameters;
            }
        }

        private FeatureSelectorWindow()
        {
            InitializeComponent();
        }

        public FeatureSelectorWindow(string feature, ResamplingSettings resamplingParameters) : this()
        {
            featureSelector.TrySetFromString(feature);

            forceSeedsCheckBox.IsChecked = resamplingParameters.forceSeeds;
            valStartSeedNumericUpDown.Value = resamplingParameters.validSeed1;
            valStepSeedNumericUpDown.Value = resamplingParameters.validSeed2;
            trainStartSeedNumericUpDown.Value = resamplingParameters.trainSeed1;
            trainStepSeedNumericUpDown.Value = resamplingParameters.trainSeed2;

            resizeSetsWhenResamplingCheckBox.IsChecked = resamplingParameters.resizeSetsWhenResampling;
            resamplingMaxValSizeNumericUpDown.Value = resamplingParameters.resamplingMaxValSize;
            resamplingMaxTrainSize1NumericUpDown.Value = resamplingParameters.resamplingMaxTrainSize1;
            resamplingMaxTrainSize2NumericUpDown.Value = resamplingParameters.resamplingMaxTrainSize2;

            repetitionPerImageNumericUpDown.Value = resamplingParameters.repetitionPerImage;
            scalesNumericUpDown.Value = resamplingParameters.resScales;
            minWindowNumericUpDown.Value = resamplingParameters.minWindow;
            jumpingFactorNumericUpDown.Value = (decimal)resamplingParameters.jumpingFactor;
            scaleFactorNumericUpDown.Value = (decimal)resamplingParameters.scaleFactor;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
