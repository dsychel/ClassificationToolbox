using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ClassifierSelector.xaml
    /// </summary>
    public partial class ClassifierSelector : UserControl
    {
        #region Fields
        string featureName = "";
        string extractorName = "HaarExtractor";
        ReadOnlyDictionary<string, int> extractorParams = null;
        readonly Dictionary<string, Parameter> parameters = new Dictionary<string, Parameter>();
        readonly OrderedDictionary classifiers = new OrderedDictionary();
        FeatureSelectorWindow.ResamplingSettings resamplingParameters;

        public string FeaturesName
        {
            set
            {
                FeatureSelector fs = new FeatureSelector();
                if (fs.TrySetFromString(value))
                {
                    extractorParams = fs.Parameters;
                    extractorName = fs.ExtractorName;
                    featureName = fs.ExtractorFileName.Replace("Features", "").Replace("Extractor", "");

                    ((TextBox)parameters["extractorType"].control).Text = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(featureName));
                    ((TextBox)parameters["extractorType"].control).Foreground = new SolidColorBrush(Colors.Black);
                    ((TextBox)parameters["extractorType"].control).HorizontalContentAlignment = HorizontalAlignment.Left;
                }
            }
        }

        public string ClassifierName
        {
            get { return classifierNameTextBox.Text + classifierExtensionTextBox.Text; }
        }

        public string ClassifierType
        {
            get { return ((Classifier)classifiers[classifierTypeComboBox.SelectedItem.ToString()]).type; }
        }

        public string ClassifierDescription
        {
            get { return GenerateDescription(classifierTypeComboBox.SelectedItem.ToString()); }
        }

        internal NativeMethods.ClassifierParameters ClassifierParameters
        {
            get
            {
                NativeMethods.ClassifierParameters parameters = new NativeMethods.ClassifierParameters() { nonFaceImagesPath = "" };
                GenerateParameters(ref parameters, classifierTypeComboBox.SelectedItem.ToString());

                parameters.extractorType = extractorName;
                parameters.p = extractorParams["p"];
                parameters.q = extractorParams["q"];
                parameters.r = extractorParams["r"];
                parameters.rt = extractorParams["rt"];
                parameters.t = extractorParams["t"];
                parameters.s = extractorParams["s"];
                parameters.ps = extractorParams["ps"];
                parameters.d = extractorParams["d"];
                parameters.w = extractorParams["w"];
                parameters.b = extractorParams["b"];
                parameters.nx = extractorParams["nx"];
                parameters.ny = extractorParams["ny"];
                //parameters.k = extractorParams["k"];

                parameters.forceSeeds = resamplingParameters.forceSeeds;
                parameters.validSeed1 = resamplingParameters.validSeed1;
                parameters.validSeed2 = resamplingParameters.validSeed2;
                parameters.trainSeed1 = resamplingParameters.trainSeed1;
                parameters.trainSeed2 = resamplingParameters.trainSeed2;

                parameters.resizeSetsWhenResampling = resamplingParameters.resizeSetsWhenResampling;
                parameters.resamplingMaxValSize = resamplingParameters.resamplingMaxValSize;
                parameters.resamplingMaxTrainSize1 = resamplingParameters.resamplingMaxTrainSize1;
                parameters.resamplingMaxTrainSize2 = resamplingParameters.resamplingMaxTrainSize2;

                parameters.repetitionPerImage = resamplingParameters.repetitionPerImage;
                parameters.resScales = resamplingParameters.resScales;
                parameters.minWindow = resamplingParameters.minWindow;
                parameters.jumpingFactor = resamplingParameters.jumpingFactor;
                parameters.scaleFactor = resamplingParameters.scaleFactor;

                return parameters;
            }
        }
        #endregion

        #region Structures
        private struct Parameter
        {
            public string key;
            public string type;
            public string shortcut;
            public Label label;
            public Control control;
            public Button button;
        }

        private struct Classifier
        {
            public string type;
            public bool cascadable;
            public bool boostalbe;
            public bool realVaule;
            public List<Parameter> parameters;
        }
        #endregion

        #region Constructors
        public ClassifierSelector()
        {
            InitializeComponent();

            InitalizeParameters();
            InitalizeClassifiers();

            classifierGrid.Children.Remove(nonFaceImagesPathButton);
            classifierGrid.Children.Remove(selectFeaturesButton);

            cascadableGrid.IsEnabled = false;
            boostableGrid.IsEnabled = false;
            mainGrid.RowDefinitions[3].Height = new GridLength(0, GridUnitType.Pixel);
            mainGrid.RowDefinitions[4].Height = new GridLength(0, GridUnitType.Pixel);
            mainGrid.RowDefinitions[5].Height = new GridLength(0, GridUnitType.Pixel);
            mainGrid.RowDefinitions[6].Height = new GridLength(0, GridUnitType.Pixel);

            classifierTypeComboBox.SelectedIndex = 0;
            boostingComboBox.SelectedIndex = 0;
            weakClassifierComboBox.SelectedIndex = 0;

            Dictionary<string, int> extDefParameters = new Dictionary<string, int>
            {
                ["p"] = 8,
                ["q"] = 8,
                ["r"] = 6,
                ["rt"] = 1,
                ["t"] = 6,
                ["s"] = 7,
                ["ps"] = 7,
                ["d"] = 200,
                ["w"] = 200,
                ["b"] = 8,
                ["nx"] = 5,
                ["ny"] = 5
            };
            extractorParams = new ReadOnlyDictionary<string, int>(extDefParameters);

            resamplingParameters.forceSeeds = true;
            resamplingParameters.validSeed1 = 12801;
            resamplingParameters.validSeed2 = 1021;
            resamplingParameters.trainSeed1 = 151;
            resamplingParameters.trainSeed2 = 3457;

            resamplingParameters.resizeSetsWhenResampling = true;
            resamplingParameters.resamplingMaxValSize = 25000;
            resamplingParameters.resamplingMaxTrainSize1 = 50000;
            resamplingParameters.resamplingMaxTrainSize2 = 35000;

            resamplingParameters.repetitionPerImage = 5000;
            resamplingParameters.resScales = 5;
            resamplingParameters.minWindow = 48;
            resamplingParameters.jumpingFactor = 0.05;
            resamplingParameters.scaleFactor = 1.2;
        }
        #endregion

        #region Methods
        private string GenerateDescription(string key)
        {
            StringBuilder description = new StringBuilder(key + ": ");
            foreach (Parameter param in ((Classifier)classifiers[key]).parameters)
            {
                if (param.type == "numeric")
                    description.Append(param.label.Content.ToString() + " " + ((NumericUpDown)param.control).Value + "; ");
                else if (param.type == "bool")
                    description.Append(((CheckBox)param.control).Content.ToString() + ": " + ((CheckBox)param.control).IsChecked + "; ");
                else if (param.type == "string")
                    description.Append(param.label.Content.ToString() + " " + ((ComboBox)param.control).SelectedItem + "; ");
                else if (param.type == "boosting")
                    description.Append(GenerateDescription(boostingComboBox.SelectedItem.ToString()));
                else if (param.type == "weak")
                    description.Append(GenerateDescription(weakClassifierComboBox.SelectedItem.ToString()));
            }
            return description.ToString();
        }

        private void GenerateParameters(ref NativeMethods.ClassifierParameters parameters, string key)
        {
            if(parameters.isGraph == false)
                parameters.isGraph = key.Contains("Graph");
            if (parameters.isDijkstra == false)
                parameters.isDijkstra = key.Contains("Dijkstra");
            foreach (Parameter param in ((Classifier)classifiers[key]).parameters)
            {
                switch (param.key)
                {
                    case "maxFAR":
                        parameters.maxFAR = (double)((NumericUpDown)param.control).Value;
                        break;
                    case "minSpecificity":
                        parameters.minSpecificity = (double)((NumericUpDown)param.control).Value;
                        break;
                    case "cascadeStages":
                        parameters.cascadeStages = (int)((NumericUpDown)param.control).Value;
                        break;
                    case "childsCount":
                        parameters.childsCount = (int)((NumericUpDown)param.control).Value;
                        break;
                    case "splits":
                        parameters.splits = (int)((NumericUpDown)param.control).Value;
                        break;
                    case "boostingStages":
                        parameters.boostingStages = (int)((NumericUpDown)param.control).Value;
                        break;
                    case "pruningFactor":
                        parameters.pruningFactor = (double)((NumericUpDown)param.control).Value;
                        break;
                    case "isUniform":
                        CheckBox cha = ((CheckBox)param.control);
                        parameters.isUniform = cha.IsChecked == false || cha.IsChecked == null ? false : true;
                        break;
                    case "learningMethod":
                        parameters.learningMethod = ((ComboBox)param.control).SelectedItem.ToString();
                        break;
                    case "realBoostBins":
                        parameters.realBoostBins = (int)((NumericUpDown)param.control).Value;
                        break;
                    case "useWeightTrimming":
                        CheckBox chb = ((CheckBox)param.control);
                        parameters.useWeightTrimming = chb.IsChecked == false || chb.IsChecked == null ? false : true;
                        break;
                    case "weightTrimmingThreshold":
                        parameters.weightTrimmingThreshold = (double)((NumericUpDown)param.control).Value;
                        break;
                    case "weightTrimmingMinSamples":
                        parameters.weightTrimmingMinSamples = (double)((NumericUpDown)param.control).Value;
                        break;
                    case "maxIterations":
                        parameters.maxIterations = (int)((NumericUpDown)param.control).Value;
                        break;
                    case "learningRate":
                        parameters.learningRate = (double)((NumericUpDown)param.control).Value;
                        break;
                    case "maxTreeLevel":
                        parameters.maxTreeLevel = (int)((NumericUpDown)param.control).Value;
                        break;
                    case "treeBins":
                        parameters.treeBins = (int)((NumericUpDown)param.control).Value;
                        break;
                    case "outlayerPercent":
                        parameters.outlayerPercent = (double)((NumericUpDown)param.control).Value;
                        break;
                    case "impurityMetric":
                        parameters.impurityMetric = ((ComboBox)param.control).SelectedItem.ToString();
                        break;
                    case "nonFaceImagesPath":
                        if (((TextBox)param.control).Text != "Non-face Images Path")
                            parameters.nonFaceImagesPath = ((TextBox)param.control).Text;
                        else
                            parameters.nonFaceImagesPath = System.Windows.Forms.Application.StartupPath + "\\NonFaces\\";
                        break;
                    case "boostingType":
                        string boostingKey = ((ComboBox)param.control).SelectedItem.ToString();
                        parameters.boostingType = ((Classifier)classifiers[boostingKey]).type;
                        GenerateParameters(ref parameters, boostingKey);
                        break;
                    case "classifierType":
                        string classifierKey = ((ComboBox)param.control).SelectedItem.ToString();
                        parameters.classifierType = ((Classifier)classifiers[classifierKey]).type;
                        GenerateParameters(ref parameters, classifierKey);
                        break;
                }
            }
        }

        private void InitalizeParameters()
        {
            NumericUpDown maxFAR = new NumericUpDown() { Minimum = 0, Maximum = 1, DecimalPlaces = 8, Increment = 0.01M, Value = 0.01M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label maxFARLabel = new Label() { Content = "Max Far:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("maxFAR", new Parameter { key = "maxFAR", type = "numeric", shortcut = "A", control = maxFAR, label = maxFARLabel, button = null });

            NumericUpDown minSpecificity = new NumericUpDown() { Minimum = 0, Maximum = 1, DecimalPlaces = 8, Increment = 0.01M, Value = 0.9M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label maxSensitivityLabel = new Label() { Content = "Min Sensitivity:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("minSpecificity", new Parameter { key = "minSpecificity", shortcut = "D", type = "numeric", control = minSpecificity, label = maxSensitivityLabel, button = null });

            NumericUpDown cascadeStages = new NumericUpDown() { Minimum = 1, Maximum = Int32.MaxValue, Value = 10M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label cascadeStagesLabel = new Label() { Content = "Stages:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("cascadeStages", new Parameter { key = "cascadeStages", type = "numeric", shortcut = "K", control = cascadeStages, label = cascadeStagesLabel, button = null });

            NumericUpDown childsCount = new NumericUpDown() { Minimum = 1, Maximum = Int32.MaxValue, Value = 3M, Increment = 2M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            childsCount.ValueChanged += ChildsCount_ValueChanged;
            Label childsCountLabel = new Label() { Content = "Childs:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("childsCount", new Parameter { key = "childsCount", type = "numeric", shortcut = "Ch", control = childsCount, label = childsCountLabel, button = null });

            NumericUpDown splits = new NumericUpDown() { Minimum = 1, Maximum = Int32.MaxValue, Value = 2M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label splitsLabel = new Label() { Content = "Splits:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("splits", new Parameter { key = "splits", type = "numeric", shortcut = "Sp", control = splits, label = splitsLabel, button = null });

            NumericUpDown boostingStages = new NumericUpDown() { Minimum = 1, Maximum = Int32.MaxValue, Value = 100M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label boostingStagesLabel = new Label() { Content = "Stages:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("boostingStages", new Parameter { key = "boostingStages", type = "numeric", shortcut = "S", control = boostingStages, label = boostingStagesLabel, button = null });

            NumericUpDown realBoostBins = new NumericUpDown() { Minimum = 1, Maximum = 100000, Value = 16M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label realBoostBinsLabel = new Label() { Content = "Bins:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("realBoostBins", new Parameter { key = "realBoostBins", type = "numeric", shortcut = "B", control = realBoostBins, label = realBoostBinsLabel, button = null });

            CheckBox isUniform = new CheckBox() { Content = "Uniform Training Method", IsChecked = false, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("isUniform", new Parameter { key = "isUniform", type = "bool", shortcut = "uni", control = isUniform, label = null, button = null });

            CheckBox useWeightTrimming = new CheckBox() { Content = "Use Weight Trimming", IsChecked = false, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("useWeightTrimming", new Parameter { key = "useWeightTrimming", type = "bool", shortcut = "trimm", control = useWeightTrimming, label = null, button = null });

            NumericUpDown weightTrimmingThreshold = new NumericUpDown() { Minimum = 0, Maximum = 1, DecimalPlaces = 4, Increment = 0.01M, Value = 0.99M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label weightTrimmingThresholdLabel = new Label() { Content = "Trim. Threshold:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("weightTrimmingThreshold", new Parameter { key = "weightTrimmingThreshold", type = "numeric", shortcut = "trimmThr", control = weightTrimmingThreshold, label = weightTrimmingThresholdLabel, button = null });

            NumericUpDown weightTrimmingMinSamples = new NumericUpDown() { Minimum = 0, Maximum = 1, DecimalPlaces = 4, Increment = 0.01M, Value = 0.01M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label weightTrimmingMinSamplesLabel = new Label() { Content = "Trim. Min Samples:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("weightTrimmingMinSamples", new Parameter { key = "weightTrimmingMinSamples", type = "numeric", shortcut = "trimmSmp", control = weightTrimmingMinSamples, label = weightTrimmingMinSamplesLabel, button = null });

            NumericUpDown maxIterations = new NumericUpDown() { Minimum = 1, Maximum = 100000, Value = 150M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label maxIterationsLabel = new Label() { Content = "Max Iterations:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("maxIterations", new Parameter { key = "maxIterations", type = "numeric", shortcut = "IT", control = maxIterations, label = maxIterationsLabel, button = null });

            NumericUpDown learningRate = new NumericUpDown() { Minimum = -10, Maximum = 10, DecimalPlaces = 4, Increment = 0.1M, Value = 0.01M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label learningRateLabel = new Label() { Content = "Learning Rate:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("learningRate", new Parameter { key = "learningRate", type = "numeric", shortcut = "LR", control = learningRate, label = learningRateLabel, button = null });

            NumericUpDown pruningFactor = new NumericUpDown() { Minimum = 0, Maximum = 2, DecimalPlaces = 2, Increment = 0.1M, Value = 0M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label pruningFactorLabel = new Label() { Content = "Pruning Factor:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("pruningFactor", new Parameter { key = "pruningFactor", type = "numeric", shortcut = "PF", control = pruningFactor, label = pruningFactorLabel, button = null });

            NumericUpDown maxTreeLevel = new NumericUpDown() { Minimum = 1, Maximum = 100000, Value = 5M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label maxTreeLevelLabel = new Label() { Content = "Max Tree Level:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("maxTreeLevel", new Parameter { key = "maxTreeLevel", type = "numeric", shortcut = "L", control = maxTreeLevel, label = maxTreeLevelLabel, button = null });

            NumericUpDown treeBins = new NumericUpDown() { Minimum = 1, Maximum = 100000, Value = 16M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label treeBinsLabel = new Label() { Content = "Bins:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("treeBins", new Parameter { key = "treeBins", type = "numeric", shortcut = "B", control = treeBins, label = treeBinsLabel, button = null });

            NumericUpDown outlayerPercent = new NumericUpDown() { Minimum = 0, Maximum = 1, DecimalPlaces = 4, Increment = 0.1M, Value = 0.0M, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalTextAligment = VerticalAlignment.Center, HorizontalTextAligment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            Label outlayerPercentLabel = new Label() { Content = "Outliers [0.0 - 1.0]:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("outlayerPercent", new Parameter { key = "outlayerPercent", type = "numeric", shortcut = "OL", control = outlayerPercent, label = outlayerPercentLabel, button = null });

            ComboBox impurityMetric = new ComboBox() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            impurityMetric.Items.Add("Gini");
            impurityMetric.Items.Add("Entrophy");
            impurityMetric.SelectedIndex = 0;
            Label impurityMetricLabel = new Label() { Content = "Impurity:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("impurityMetric", new Parameter { key = "impurityMetric", type = "string", shortcut = "IM", control = impurityMetric, label = impurityMetricLabel, button = null });

            ComboBox learningMethod = new ComboBox() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 2, 0, 2), FontWeight = FontWeights.Regular };
            learningMethod.Items.Add("VJ");
            learningMethod.Items.Add("UGM");
            learningMethod.Items.Add("UGM-G");
            learningMethod.SelectedIndex = 0;
            Label learningMethodLabel = new Label() { Content = "Training Method:", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular };
            parameters.Add("learningMethod", new Parameter { key = "learningMethod", type = "string", shortcut = "LM", control = learningMethod, label = learningMethodLabel, button = null });

            TextBox nonFaceImagesPath = new TextBox() { IsReadOnly = true, Text = "Non-face Images Path", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0xE4, 0xE4)), Foreground = new SolidColorBrush(Colors.DimGray), Margin = new Thickness(0, 2, 5, 2) };
            nonFaceImagesPathButton.Click += NonFaceFolderButton_Click;
            parameters.Add("nonFaceImagesPath", new Parameter { key = "nonFaceImagesPath", type = "text", shortcut = "NFIP", control = nonFaceImagesPath, label = null, button = nonFaceImagesPathButton });

            TextBox featuresTextBox = new TextBox() { IsReadOnly = true, Text = "haar6t7s7p", HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeights.Regular, Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0xE4, 0xE4)), Foreground = new SolidColorBrush(Colors.DimGray), Margin = new Thickness(0, 2, 5, 2) };
            selectFeaturesButton.Click += SelectFeaturesButton_Click;
            parameters.Add("extractorType", new Parameter { key = "extractorType", type = "shortText", shortcut = "", control = featuresTextBox, label = null, button = selectFeaturesButton });

            parameters.Add("boostingType", new Parameter { key = "boostingType", type = "boosting", shortcut = "", control = boostingComboBox });
            parameters.Add("classifierType", new Parameter { key = "classifierType", type = "weak", shortcut = "", control = weakClassifierComboBox });
            parameters.Add("empty", new Parameter { type = "empty" });
        }

        private void ChildsCount_ValueChanged(object sender, ValueChangedEventArg e)
        {
            NumericUpDown control = sender as NumericUpDown;
            if ((decimal)e.CurrentValue % 2 == 0)
            {
                if ((decimal)e.CurrentValue + 1 < control.Maximum)
                    control.Value = (decimal)e.CurrentValue + 1;
                else
                    control.Value = (decimal)e.CurrentValue - 1;
            }
        }

        private void InitalizeClassifiers()
        {
            Classifier cascade = new Classifier()
            {
                type = "ClassifierCascade",
                cascadable = false,
                boostalbe = false,
                parameters = new List<Parameter>() { parameters["cascadeStages"], parameters["learningMethod"], parameters["empty"], parameters["maxFAR"], parameters["minSpecificity"], parameters["empty"], parameters["boostingType"], parameters["nonFaceImagesPath"], parameters["extractorType"] }
            };
            classifiers.Add("Cascade of Classifiers", cascade);

            Classifier graphcascade = new Classifier()
            {
                type = "ClassifierCascade",
                cascadable = false,
                boostalbe = false,
                parameters = new List<Parameter>() { parameters["cascadeStages"], parameters["maxFAR"], parameters["minSpecificity"], parameters["splits"], parameters["childsCount"], parameters["pruningFactor"], parameters["boostingType"], parameters["learningMethod"], parameters["isUniform"], parameters["empty"], parameters["nonFaceImagesPath"], parameters["extractorType"] }
            };
            classifiers.Add("Cascade of Classifiers - Graph", graphcascade);

            Classifier graphdijkstracascade = new Classifier()
            {
                type = "ClassifierCascade",
                cascadable = false,
                boostalbe = false,
                parameters = new List<Parameter>() { parameters["cascadeStages"], parameters["maxFAR"], parameters["minSpecificity"], parameters["splits"], parameters["childsCount"], parameters["boostingType"], parameters["learningMethod"], parameters["nonFaceImagesPath"], parameters["extractorType"] }
            };
            classifiers.Add("Cascade of Classifiers - Dijkstra (Graph)", graphdijkstracascade);

            Classifier adaboost = new Classifier
            {
                type = "AdaBoost",
                cascadable = true,
                boostalbe = false,
                parameters = new List<Parameter>() { parameters["boostingStages"], parameters["empty"], parameters["empty"], parameters["useWeightTrimming"], parameters["weightTrimmingThreshold"], parameters["weightTrimmingMinSamples"], parameters["classifierType"] }
            };
            classifiers.Add("Ada Boost", adaboost);

            Classifier realBoost = new Classifier
            {
                type = "RealBoost",
                cascadable = true,
                boostalbe = false,
                parameters = new List<Parameter>() { parameters["boostingStages"], parameters["empty"], parameters["empty"], parameters["useWeightTrimming"], parameters["weightTrimmingThreshold"], parameters["weightTrimmingMinSamples"], parameters["classifierType"] }
            };
            classifiers.Add("Real Boost", realBoost);

            Classifier bDecisionStump = new Classifier
            {
                type = "BinnedDecisionStump",
                cascadable = false,
                boostalbe = true,
                realVaule = true,
                parameters = new List<Parameter>() { parameters["treeBins"], parameters["empty"], parameters["empty"], parameters["outlayerPercent"] }
            };
            classifiers.Add("Binned Decision Stump", bDecisionStump);

            Classifier binnedTree = new Classifier
            {
                type = "BinnedTree",
                cascadable = false,
                boostalbe = true,
                realVaule = true,
                parameters = new List<Parameter>() { parameters["impurityMetric"], parameters["empty"], parameters["empty"], parameters["maxTreeLevel"], parameters["treeBins"], parameters["empty"], parameters["outlayerPercent"] }
            };
            classifiers.Add("Binned Tree", binnedTree);

            Classifier decisionStump = new Classifier
            {
                type = "DecisionStump",
                cascadable = false,
                boostalbe = true,
                realVaule = false,
                parameters = new List<Parameter>() { }
            };
            classifiers.Add("Decision Stump", decisionStump);

            Classifier regularBins = new Classifier
            {
                type = "RegularBins",
                cascadable = false,
                boostalbe = true,
                realVaule = true,
                parameters = new List<Parameter>() { parameters["treeBins"], parameters["empty"], parameters["empty"], parameters["outlayerPercent"] }
            };
            classifiers.Add("Regular Bins", regularBins);

            Classifier weakPerceptron = new Classifier
            {
                type = "WeakPerceptron",
                cascadable = false,
                boostalbe = true,
                realVaule = false,
                parameters = new List<Parameter>() { parameters["maxIterations"], parameters["empty"], parameters["empty"], parameters["learningRate"] }
            };
            classifiers.Add("Weak Perceptron", weakPerceptron);

            Classifier zeroRule = new Classifier
            {
                type = "ZeroRule",
                cascadable = false,
                boostalbe = false,
                realVaule = false,
                parameters = new List<Parameter>()
            };
            classifiers.Add("Zero Rule", zeroRule);

            foreach (string key in classifiers.Keys)
            {
                classifierTypeComboBox.Items.Add(key);
                if (((Classifier)classifiers[key]).cascadable)
                    boostingComboBox.Items.Add(key);
                if (((Classifier)classifiers[key]).boostalbe)
                    weakClassifierComboBox.Items.Add(key);
            }
        }

        private string GenerateName(string key)
        {
            bool trimm = false;
            StringBuilder description = new StringBuilder(((Classifier)classifiers[key]).type + "_");
            foreach (Parameter param in ((Classifier)classifiers[key]).parameters)
            {
                if (param.type == "numeric")
                {
                    if (param.shortcut == "OL" && ((NumericUpDown)param.control).Value == 0)
                        continue;
                    if ((param.shortcut == "trimmThr" || param.shortcut == "trimmSmp") && trimm == false)
                        continue;
                    description.Append(param.shortcut + "_" + ((NumericUpDown)param.control).Value + "_");
                }
                else if (param.type == "bool")
                {
                    CheckBox chb = ((CheckBox)param.control);
                    if (chb.IsChecked == true)
                        description.Append(param.shortcut + "_True_");
                    if (param.shortcut == "trimm")
                        trimm = chb.IsChecked == false || chb.IsChecked == null ? false : true;
                }
                else if (param.type == "string")
                    description.Append(param.shortcut + "_" + ((ComboBox)param.control).SelectedItem + "_");
                else if (param.type == "boosting")
                    description.Append(GenerateName(boostingComboBox.SelectedItem.ToString()));
                else if (param.type == "weak")
                    description.Append(GenerateName(weakClassifierComboBox.SelectedItem.ToString()));
            }
            if (key.Contains("Dijkstra"))
                description.Append("DJ_");
            return description.ToString();
        }
        #endregion

        #region Events
        private void NonFaceFolderButton_Click(object sender, RoutedEventArgs e)
        {
            ((Button)sender).Click -= NonFaceFolderButton_Click;

            string directory = Properties.Settings.Default.learningResamplingFolder;
            if (!Directory.Exists(directory))
                directory = "";
            System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog()
            {
                SelectedPath = directory
            };
            if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string text = folderBrowserDialog.SelectedPath;
                //if (text[text.Length - 1] != '\\')
                //    text += '\\';
                ((TextBox)parameters[(string)((Button)sender).Tag].control).Text = text;
                ((TextBox)parameters[(string)((Button)sender).Tag].control).Foreground = new SolidColorBrush(Colors.Black);
                ((TextBox)parameters[(string)((Button)sender).Tag].control).HorizontalContentAlignment = HorizontalAlignment.Left;

                Properties.Settings.Default.learningResamplingFolder = text;
                Properties.Settings.Default.Save();
            }
            folderBrowserDialog.Dispose();


            e.Handled = true;
            Task.Delay(200).ContinueWith(_ => { ((Button)sender).Click += NonFaceFolderButton_Click; });
        }

        private void SelectFeaturesButton_Click(object sender, RoutedEventArgs e)
        {
            ((Button)sender).Click -= SelectFeaturesButton_Click;

            FeatureSelectorWindow fsw = new FeatureSelectorWindow(featureName, resamplingParameters);
            if(fsw.ShowDialog() == true)
            {
                featureName = fsw.FileName.Replace("Features", "").Replace("Extractor", "");
                extractorName = fsw.ExtractorName;
                extractorParams = fsw.Parameters;

                resamplingParameters = fsw.ResamplingParameters;

                ((TextBox)parameters[(string)((Button)sender).Tag].control).Text = featureName;
                ((TextBox)parameters[(string)((Button)sender).Tag].control).Foreground = new SolidColorBrush(Colors.Black);
                ((TextBox)parameters[(string)((Button)sender).Tag].control).HorizontalContentAlignment = HorizontalAlignment.Left;
            }

            e.Handled = true;
            Task.Delay(200).ContinueWith(_ => { ((Button)sender).Click += SelectFeaturesButton_Click; });
        }

        private void GenerateNameButton_Click(object sender, RoutedEventArgs e)
        {
            classifierNameTextBox.Text = GenerateName(classifierTypeComboBox.SelectedItem.ToString());
            classifierNameTextBox.Text += featureName;
            if (classifierNameTextBox.Text[classifierNameTextBox.Text.Length - 1] == '_')
                classifierNameTextBox.Text = classifierNameTextBox.Text.Remove(classifierNameTextBox.Text.Length - 1);
            classifierNameTextBox.Text = classifierNameTextBox.Text.Replace(".", "_");
        }

        private void ClassifierTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsInitialized)
            {
                cascadableGrid.IsEnabled = false;
                boostableGrid.IsEnabled = false;
                mainGrid.Children.Remove(boostableGrid);
                mainGrid.Children.Remove(cascadableGrid);
                mainGrid.RowDefinitions[2].Height = new GridLength(72, GridUnitType.Pixel);
                mainGrid.RowDefinitions[3].Height = new GridLength(0, GridUnitType.Pixel);
                mainGrid.RowDefinitions[4].Height = new GridLength(0, GridUnitType.Pixel);
                mainGrid.RowDefinitions[5].Height = new GridLength(0, GridUnitType.Pixel);
                mainGrid.RowDefinitions[6].Height = new GridLength(0, GridUnitType.Pixel);

                cascadableGrid.Children.Clear();
                boostableGrid.Children.Clear();
                classifierGrid.Children.Clear();
                classifierGrid.Children.Add(classifierTypeLabel);
                classifierGrid.Children.Add(classifierTypeComboBox);

                if (classifierGrid.ColumnDefinitions.Count > 3)
                    for (int i = classifierGrid.ColumnDefinitions.Count - 2; i >= 2; i--)
                        classifierGrid.ColumnDefinitions.RemoveAt(i);

                int columns = 3;
                int colID = 0;
                int rowID = 0;

                Classifier cls = (Classifier)classifiers[classifierTypeComboBox.SelectedItem.ToString()];
                foreach (Parameter param in cls.parameters)
                {
                    if (param.type != "boosting" && param.type != "weak" && param.type != "weakReal")
                    {
                        if (classifierGrid.ColumnDefinitions.Count < 12)
                        {
                            classifierGrid.ColumnDefinitions.Insert(classifierGrid.ColumnDefinitions.Count - 1, new ColumnDefinition { Width = new GridLength(5, GridUnitType.Pixel) });
                            classifierGrid.ColumnDefinitions.Insert(classifierGrid.ColumnDefinitions.Count - 1, new ColumnDefinition { Width = new GridLength(105, GridUnitType.Pixel) });
                            classifierGrid.ColumnDefinitions.Insert(classifierGrid.ColumnDefinitions.Count - 1, new ColumnDefinition { Width = new GridLength(120, GridUnitType.Pixel) });
                            columns += 3;
                        }

                        if (param.type != "empty")
                        {
                            if (param.type == "bool")
                            {
                                classifierGrid.Children.Add(param.control);
                                Grid.SetColumn(param.control, 2 + colID * 3 + 1);
                                Grid.SetRow(param.control, rowID);
                                Grid.SetColumnSpan(param.control, 2);
                            }
                            else if (param.type == "shortText")
                            {
                                classifierGrid.Children.Add(param.control);
                                Grid.SetColumn(param.control, 2 + colID * 3 + 1);
                                Grid.SetRow(param.control, rowID);

                                classifierGrid.Children.Add(param.button);
                                Grid.SetColumn(param.button, 2 + colID * 3 + 2);
                                Grid.SetRow(param.button, rowID);
                            }
                            else if (param.type == "text")
                            {
                                classifierGrid.Children.Add(param.control);
                                Grid.SetColumn(param.control, 2 + colID * 3 + 1);
                                Grid.SetRow(param.control, rowID);
                                Grid.SetColumnSpan(param.control, 4);

                                classifierGrid.Children.Add(param.button);
                                Grid.SetColumn(param.button, 2 + colID * 3 + 5);
                                Grid.SetRow(param.button, rowID);
                                colID++;
                            }
                            else
                            {
                                classifierGrid.Children.Add(param.label);
                                Grid.SetColumn(param.label, 2 + colID * 3 + 1);
                                Grid.SetRow(param.label, rowID);

                                classifierGrid.Children.Add(param.control);
                                Grid.SetColumn(param.control, 2 + colID * 3 + 2);
                                Grid.SetRow(param.control, rowID);
                            }
                        }

                        colID++;
                        if (colID > 2)
                        {
                            colID = 0;
                            rowID++;
                        }
                    }
                    else if (param.type == "boosting")
                    {
                        mainGrid.Children.Add(cascadableGrid);
                        cascadableGrid.IsEnabled = true;
                        cascadableGrid.Children.Add(boostingClsTypelabel);
                        cascadableGrid.Children.Add(boostingComboBox);
                        mainGrid.RowDefinitions[3].Height = new GridLength(2, GridUnitType.Pixel);
                        mainGrid.RowDefinitions[4].Height = new GridLength(70, GridUnitType.Pixel);

                        if (boostingComboBox.SelectedItem != null)
                            BoostingComboBox_SelectionChanged(boostingComboBox, null);
                    }
                    else if (param.type == "weak")
                    {
                        mainGrid.Children.Add(boostableGrid);
                        boostableGrid.IsEnabled = true;
                        boostableGrid.Children.Add(weakClassifierComboBox);
                        boostableGrid.Children.Add(weakClsLabel);
                        Grid.SetRow(boostableGrid, 4);
                        mainGrid.RowDefinitions[3].Height = new GridLength(2, GridUnitType.Pixel);
                        mainGrid.RowDefinitions[4].Height = new GridLength(70, GridUnitType.Pixel);

                        if (weakClassifierComboBox.SelectedItem != null)
                            WeakClassifierComboBox_SelectionChanged(weakClassifierComboBox, null);
                    }
                }

                if(rowID >= 4)
                    mainGrid.RowDefinitions[2].Height = new GridLength(96, GridUnitType.Pixel);
            }
        }

        private void BoostingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            boostableGrid.IsEnabled = false;
            mainGrid.Children.Remove(boostableGrid);
            mainGrid.RowDefinitions[5].Height = new GridLength(0, GridUnitType.Pixel);
            mainGrid.RowDefinitions[6].Height = new GridLength(0, GridUnitType.Pixel);

            boostableGrid.Children.Clear();
            cascadableGrid.Children.Clear();
            cascadableGrid.Children.Add(boostingClsTypelabel);
            cascadableGrid.Children.Add(boostingComboBox);

            if (cascadableGrid.ColumnDefinitions.Count > 3)
                for (int i = cascadableGrid.ColumnDefinitions.Count - 2; i >= 2; i--)
                    cascadableGrid.ColumnDefinitions.RemoveAt(i);

            int columns = 3;
            int colID = 0;
            int rowID = 0;

            Classifier cls = (Classifier)classifiers[boostingComboBox.SelectedItem.ToString()];
            foreach (Parameter param in cls.parameters)
            {
                if (param.type != "weak" && param.type != "weakReal")
                {
                    if (cascadableGrid.ColumnDefinitions.Count < 12)
                    {
                        cascadableGrid.ColumnDefinitions.Insert(cascadableGrid.ColumnDefinitions.Count - 1, new ColumnDefinition { Width = new GridLength(5, GridUnitType.Pixel) });
                        cascadableGrid.ColumnDefinitions.Insert(cascadableGrid.ColumnDefinitions.Count - 1, new ColumnDefinition { Width = new GridLength(105, GridUnitType.Pixel) });
                        cascadableGrid.ColumnDefinitions.Insert(cascadableGrid.ColumnDefinitions.Count - 1, new ColumnDefinition { Width = new GridLength(120, GridUnitType.Pixel) });
                        columns += 3;
                    }

                    if (param.type != "empty")
                    {
                        if (param.type == "bool")
                        {
                            cascadableGrid.Children.Add(param.control);
                            Grid.SetColumn(param.control, 2 + colID * 3 + 1);
                            Grid.SetRow(param.control, rowID);
                            Grid.SetColumnSpan(param.control, 2);
                        }
                        else
                        {
                            cascadableGrid.Children.Add(param.label);
                            Grid.SetColumn(param.label, 2 + colID * 3 + 1);
                            Grid.SetRow(param.label, rowID);

                            cascadableGrid.Children.Add(param.control);
                            Grid.SetColumn(param.control, 2 + colID * 3 + 2);
                            Grid.SetRow(param.control, rowID);
                        }
                    }

                    colID++;
                    if (colID > 2)
                    {
                        colID = 0;
                        rowID++;
                    }
                }
                else if (param.type == "weak")
                {
                    mainGrid.Children.Add(boostableGrid);
                    boostableGrid.IsEnabled = true;
                    boostableGrid.Children.Add(weakClassifierComboBox);
                    boostableGrid.Children.Add(weakClsLabel);
                    Grid.SetRow(boostableGrid, 6);
                    mainGrid.RowDefinitions[5].Height = new GridLength(2, GridUnitType.Pixel);
                    mainGrid.RowDefinitions[6].Height = new GridLength(70, GridUnitType.Pixel);

                    if (weakClassifierComboBox.SelectedItem != null)
                        WeakClassifierComboBox_SelectionChanged(weakClassifierComboBox, null);
                }
            }
        }

        private void WeakClassifierComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            boostableGrid.Children.Clear();
            boostableGrid.Children.Add(weakClsLabel);
            boostableGrid.Children.Add(weakClassifierComboBox);

            if (boostableGrid.ColumnDefinitions.Count > 3)
                for (int i = boostableGrid.ColumnDefinitions.Count - 2; i >= 2; i--)
                    boostableGrid.ColumnDefinitions.RemoveAt(i);

            int columns = 3;
            int colID = 0;
            int rowID = 0;

            Classifier cls = (Classifier)classifiers[weakClassifierComboBox.SelectedItem.ToString()];
            foreach (Parameter param in cls.parameters)
            {
                if (boostableGrid.ColumnDefinitions.Count < 12)
                {
                    boostableGrid.ColumnDefinitions.Insert(boostableGrid.ColumnDefinitions.Count - 1, new ColumnDefinition { Width = new GridLength(5, GridUnitType.Pixel) });
                    boostableGrid.ColumnDefinitions.Insert(boostableGrid.ColumnDefinitions.Count - 1, new ColumnDefinition { Width = new GridLength(105, GridUnitType.Pixel) });
                    boostableGrid.ColumnDefinitions.Insert(boostableGrid.ColumnDefinitions.Count - 1, new ColumnDefinition { Width = new GridLength(120, GridUnitType.Pixel) });
                    columns += 3;
                }

                if (param.type != "empty")
                {
                    if (param.type == "bool")
                    {
                        boostableGrid.Children.Add(param.control);
                        Grid.SetColumn(param.control, 2 + colID * 3 + 1);
                        Grid.SetRow(param.control, rowID);
                        Grid.SetColumnSpan(param.control, 2);
                    }
                    else
                    {
                        boostableGrid.Children.Add(param.label);
                        Grid.SetColumn(param.label, 2 + colID * 3 + 1);
                        Grid.SetRow(param.label, rowID);

                        boostableGrid.Children.Add(param.control);
                        Grid.SetColumn(param.control, 2 + colID * 3 + 2);
                        Grid.SetRow(param.control, rowID);
                    }
                }

                colID++;
                if (colID > 2)
                {
                    colID = 0;
                    rowID++;
                }
            }
        }
        #endregion
    }
}
