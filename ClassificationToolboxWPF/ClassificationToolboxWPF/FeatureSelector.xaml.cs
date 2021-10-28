using System.Collections.Generic;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for FeatureSelector.xaml
    /// </summary>
    public partial class FeatureSelector : UserControl
    {

        readonly ComboBoxItem zernikeFPiiItem = new ComboBoxItem()
        {
            Content = "Zernike (Pełne Podziały)",
            Tag = "ZernikeFPiiExtractor"
        };
        readonly ComboBoxItem zernikeFPiiInvariantsItem = new ComboBoxItem()
        {
            Content = "Zernike Invaraints (Pełne Podziały)",
            Tag = "ZernikeFPiiInvariantsExtractor"
        };
        readonly ComboBoxItem zernikePiiItem = new ComboBoxItem()
        {
            Content = "Zernike (Podziały)",
            Tag = "ZernikePiiExtractor"
        };
        readonly ComboBoxItem zernikeZiiItem = new ComboBoxItem()
        {
            Content = "Zernike (Zakładki)",
            Tag = "ZernikeZiiExtractor"
        };

        private static readonly DependencyProperty IsForDetectionProperty =
        DependencyProperty.Register("IsForDetection", typeof(bool), typeof(FeatureSelector), new PropertyMetadata(false));

        public bool IsForDetection
        {
            get { return (bool)GetValue(IsForDetectionProperty); }
            set { SetValue(IsForDetectionProperty, value); }
        }

        public string ExtractorName { private set; get; } = "ZernikeExtractor";

        readonly private Dictionary<string, int> parameters = new Dictionary<string, int>()
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

        public ReadOnlyDictionary<string, int> Parameters
        {
            get { return new ReadOnlyDictionary<string, int>(parameters); }
        }

        public int[] ParametersArray
        {
            get
            {
                if (ExtractorName == "HaarExtractor")
                    return new int[] { parameters["t"], parameters["s"], parameters["ps"] };
                else if (ExtractorName == "HOGExtractor")
                    return new int[] { parameters["b"], parameters["nx"], parameters["ny"] };
                else if (ExtractorName == "PFMMExtractor")
                    return new int[] { parameters["p"], parameters["q"], parameters["r"], parameters["rt"] };
                else if (ExtractorName == "ZernikeExtractor")
                    return new int[] { parameters["p"], parameters["q"], parameters["r"], parameters["rt"] }; //, parameters["k"] };
                else if (ExtractorName == "ZernikePiiExtractor")
                    return new int[] { parameters["p"], parameters["q"], parameters["r"], parameters["rt"], parameters["d"] }; //, parameters["k"] 
                else if (ExtractorName == "ZernikeInvariantsExtractor")
                    return new int[] { parameters["p"], parameters["q"], parameters["r"], parameters["rt"], parameters["d"] }; //, parameters["k"] };
                else if (ExtractorName == "ZernikeFPiiExtractor")
                    return new int[] { parameters["p"], parameters["q"], parameters["r"], parameters["rt"], parameters["d"] }; //, parameters["k"] };
                else if (ExtractorName == "ZernikeFPiiInvariantsExtractor")
                    return new int[] { parameters["p"], parameters["q"], parameters["r"], parameters["rt"], parameters["d"] }; //, parameters["k"] };
                else if (ExtractorName == "ZernikeZiiExtractor")
                    return new int[] { parameters["p"], parameters["q"], parameters["r"], parameters["rt"], parameters["d"], parameters["w"] }; //, parameters["k"] };
                else
                    return null;
            }
        }

        public string ExtractorFileName
        {
            get
            {
                if (ExtractorName == "HaarExtractor")
                    return "haarFeatures" + parameters["t"] + "t" + parameters["s"] + "s" + parameters["ps"] + "p";
                else if (ExtractorName == "HOGExtractor")
                    return "hogFeatures" + parameters["b"] + "b" + parameters["nx"] + "nx" + parameters["ny"] + "ny";
                else if (ExtractorName == "PFMMExtractor")
                    return "pfmmFeatures" + parameters["p"] + "p" + parameters["q"] + "q" + parameters["r"] + "r" + parameters["rt"] + "rt";
                else if (ExtractorName == "ZernikeExtractor")
                    return "zernikeFeatures" + parameters["p"] + "p" + parameters["q"] + "q" + parameters["r"] + "r" + parameters["rt"] + "rt"; // + parameters["k"] + "k";
                else if (ExtractorName == "ZernikePiiExtractor")
                    return "zernikePiiFeatures" + parameters["p"] + "p" + parameters["q"] + "q" + parameters["r"] + "r" + parameters["rt"] + "rt" /* + parameters["k"] + "k" */ + parameters["d"] + "d";
                else if (ExtractorName == "ZernikeInvariantsExtractor")
                    return "zernikeInvariantsFeatures" + parameters["p"] + "p" + parameters["q"] + "q" + parameters["r"] + "r" + parameters["rt"] + "rt" /* + parameters["k"] + "k" */ + parameters["d"] + "d";
                else if (ExtractorName == "ZernikeFPiiExtractor")
                    return "ZernikeFPiiExtractor" + parameters["p"] + "p" + parameters["q"] + "q" + parameters["r"] + "r" + parameters["rt"] + "rt" /* + parameters["k"] + "k" */ + parameters["d"] + "d";
                else if (ExtractorName == "ZernikeFPiiInvariantsExtractor")
                    return "ZernikeFPiiInvariantsExtractor" + parameters["p"] + "p" + parameters["q"] + "q" + parameters["r"] + "r" + parameters["rt"] + "rt" /* + parameters["k"] + "k" */ + parameters["d"] + "d";
                else if (ExtractorName == "ZernikeZiiExtractor")
                    return "zernikeZiiFeatures" + parameters["p"] + "p" + parameters["q"] + "q" + parameters["r"] + "r" + parameters["rt"] + "rt" /* + parameters["k"] + "k" */ + parameters["d"] + "d" + parameters["w"] + "w";
                else
                    return "";
            }
        }

        public string ExtractorDescription
        {
            get
            {
                if (ExtractorName == "HaarExtractor")
                    return "Haar Features - " + "Templates: " + parameters["t"] + ", Scales: " + parameters["s"] + ", Grid Size: " + parameters["ps"];
                else if (ExtractorName == "HOGExtractor")
                    return "HOG Features - " + "Bins: " + parameters["b"] + ", Blocks (X): " + parameters["nx"] + ", Blocks (Y): " + parameters["ny"];
                else if (ExtractorName == "PFMMExtractor")
                    return "PFMM Features - " + "Harmonic 'p': " + parameters["p"] + ", Degree 'q': " + parameters["q"] + ", Rings: " + parameters["r"] + ", Rings Type: " + parameters["rt"];
                else if (ExtractorName == "ZernikeExtractor")
                    return "Zernike Features - " + "Harmonic 'p': " + parameters["p"] + ", Degree 'q': " + parameters["q"] + ", Rings: " + parameters["r"] + ", Rings Type: " + parameters["rt"]; // + ", Multiplier: " + parameters["k"];
                else if (ExtractorName == "ZernikePiiExtractor")
                    return "Zernike PII Features - " + "Harmonic 'p': " + parameters["p"] + ", Degree 'q': " + parameters["q"] + ", Rings: " + parameters["r"] + ", Rings Type: " + parameters["rt"] /* + ", Multiplier: " + parameters["k"] */ + ", Width: " + parameters["d"];
                else if (ExtractorName == "ZernikeInvariantsExtractor")
                    return "Zernike Invariants Features - " + "Harmonic 'p': " + parameters["p"] + ", Degree 'q': " + parameters["q"] + ", Rings: " + parameters["r"] + ", Rings Type: " + parameters["rt"] /* + ", Multiplier: " + parameters["k"] */ + ", Width: " + parameters["d"];
                else if (ExtractorName == "ZernikeFPiiExtractor")
                    return "Zernike FP Features - " + "Harmonic 'p': " + parameters["p"] + ", Degree 'q': " + parameters["q"] + ", Rings: " + parameters["r"] + ", Rings Type: " + parameters["rt"] /* + ", Multiplier: " + parameters["k"] */ + ", Width: " + parameters["d"];
                else if (ExtractorName == "ZernikeFPiiInvariantsExtractor")
                    return "Zernike FP Invariants Features - " + "Harmonic 'p': " + parameters["p"] + ", Degree 'q': " + parameters["q"] + ", Rings: " + parameters["r"] + ", Rings Type: " + parameters["rt"] /* + ", Multiplier: " + parameters["k"] */ + ", Width: " + parameters["d"];
                else if (ExtractorName == "ZernikeZiiExtractor")
                    return "Zernike ZII Features - " + "Harmonic 'p': " + parameters["p"] + ", Degree 'q': " + parameters["q"] + ", Rings: " + parameters["r"] + ", Rings Type: " + parameters["rt"] /* + ", Multiplier: " + parameters["k"] */ + ", Width: " + parameters["d"] + ", Overlap: " + parameters["w"];
                else
                    return "";
            }
        }

        public FeatureSelector()
        {
            InitializeComponent();
        }

        #region Methods
        public MessageBoxResult CheckFileName(string featuresName)
        {
            bool nameError = false;
            string error = "Current filename is not recommended.\r\nRecomended filename should contain feature type and parameters description.\r\n\r\nWarnings:\r\n";

            bool haarInName = featuresName.ToUpper().Contains("HAAR");
            bool hogInName = featuresName.ToUpper().Contains("HOG");
            bool pfmmInName = featuresName.ToUpper().Contains("PFMM");
            bool zernikeInName = featuresName.ToUpper().Contains("ZERNIKE");
            bool invariantInName = featuresName.ToUpper().Contains("INVARIANTS");

            string extractorName = ExtractorName;
            if (haarInName && extractorName != "HaarExtractor")
            {
                nameError = true;
                error += "--- wrong feature type included;\r\n";
            }
            else if (hogInName && extractorName != "HOGExtractor")
            {
                nameError = true;
                error += "--- wrong feature type included;\r\n";
            }
            else if (pfmmInName && extractorName != "PFMMExtractor")
            {
                nameError = true;
                error += "--- wrong feature type included;\r\n";
            }
            else if (zernikeInName && invariantInName && !extractorName.Contains("Invariants") && !extractorName.Contains("Zernike"))
            {
                nameError = true;
                error += "--- wrong feature type included;\r\n";
            }
            else if (zernikeInName && !invariantInName && (extractorName.Contains("Invariants") || !extractorName.Contains("Zernike")))
            {
                nameError = true;
                error += "--- wrong feature type included;\r\n";
            }
            else if (!haarInName && !pfmmInName && !zernikeInName && !hogInName)
            {
                nameError = true;
                error += "--- filename don't contain feature type;\r\n";
            }
            else
            {
                ReadOnlyDictionary<string, int> param = Parameters;
                Match match;
                featuresName = Path.GetFileName(featuresName);
                if (haarInName)
                {
                    featuresName = featuresName.Substring(featuresName.ToUpper().IndexOf("HAAR"));

                    match = Regex.Match(featuresName, @"[\d]+[tT]");
                    string templates = match.Value;
                    if (templates != "" && Int32.Parse(templates.Remove(templates.Length - 1)) != param["t"])
                    {
                        error += "--- parameter \"t\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (templates == "")
                    {
                        error += "--- filename don't contain parameter \"t\";\r\n";
                        nameError = true;
                    }

                    match = Regex.Match(featuresName, @"[\d]+[pP]");
                    string positions = match.Value;
                    if (positions != "" && Int32.Parse(positions.Remove(positions.Length - 1)) != param["ps"])
                    {
                        error += "--- parameter \"p\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (positions == "")
                    {
                        error += "--- filename don't contain parameter \"p\";\r\n";
                        nameError = true;
                    }

                    match = Regex.Match(featuresName, @"[\d]+[sS]");
                    string scales = match.Value;
                    if (scales != "" && Int32.Parse(scales.Remove(scales.Length - 1)) != param["s"])
                    {
                        error += "--- parameter \"s\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (scales == "")
                    {
                        error += "--- filename don't contain parameter \"s\";\r\n";
                        nameError = true;
                    }
                }
                if (hogInName)
                {
                    featuresName = featuresName.Substring(featuresName.ToUpper().IndexOf("HOG"));

                    match = Regex.Match(featuresName, @"[\d]+[bB]");
                    string bins = match.Value;
                    if (bins != "" && Int32.Parse(bins.Remove(bins.Length - 1)) != param["b"])
                    {
                        error += "--- parameter \"b\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (bins == "")
                    {
                        error += "--- filename don't contain parameter \"b\";\r\n";
                        nameError = true;
                    }

                    match = Regex.Match(featuresName, @"[\d]+[nN][xX]");
                    string positionsX = match.Value;
                    if (positionsX != "" && Int32.Parse(positionsX.Remove(positionsX.Length - 2)) != param["nx"])
                    {
                        error += "--- parameter \"nx\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (positionsX == "")
                    {
                        error += "--- filename don't contain parameter \"nx\";\r\n";
                        nameError = true;
                    }

                    match = Regex.Match(featuresName, @"[\d]+[nN][yY]");
                    string positionsY = match.Value;
                    if (positionsY != "" && Int32.Parse(positionsY.Remove(positionsY.Length - 2)) != param["ny"])
                    {
                        error += "--- parameter \"ny\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (positionsY == "")
                    {
                        error += "--- filename don't contain parameter \"ny\";\r\n";
                        nameError = true;
                    }
                }
                if (pfmmInName)
                {
                    featuresName = featuresName.Substring(featuresName.ToUpper().IndexOf("PFMM"));

                    match = Regex.Match(featuresName, @"[\d]+[rR]");
                    string rings = match.Value;
                    if (rings != "" && Int32.Parse(rings.Remove(rings.Length - 1)) != param["r"])
                    {
                        error += "--- parameter \"r\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (rings == "")
                    {
                        error += "--- filename don't contain parameter \"r\";\r\n";
                        nameError = true;
                    }

                    match = Regex.Match(featuresName, @"[\d]+[pP]");
                    string harmonic = match.Value;
                    if (harmonic != "" && Int32.Parse(harmonic.Remove(harmonic.Length - 1)) != param["p"])
                    {
                        error += "--- parameter \"p\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (harmonic == "")
                    {
                        error += "--- filename don't contain parameter \"p\";\r\n";
                        nameError = true;
                    }

                    match = Regex.Match(featuresName, @"[\d]+[qQ]");
                    string degree = match.Value;
                    if (degree != "" && Int32.Parse(degree.Remove(degree.Length - 1)) != param["q"])
                    {
                        error += "--- parameter \"q\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (degree == "")
                    {
                        error += "--- filename don't contain parameter \"q\";\r\n";
                        nameError = true;
                    }
                }
                if (zernikeInName)
                {
                    featuresName = featuresName.Substring(featuresName.ToUpper().IndexOf("ZERNIKE"));

                    match = Regex.Match(featuresName, @"[\d]+[rR]");
                    string rings = match.Value;
                    if (rings != "" && Int32.Parse(rings.Remove(rings.Length - 1)) != param["r"])
                    {
                        error += "--- parameter \"r\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (rings == "")
                    {
                        error += "--- filename don't contain parameter \"r\";\r\n";
                        nameError = true;
                    }

                    match = Regex.Match(featuresName, @"[\d]+[pP]");
                    string harmonic = match.Value;
                    if (harmonic != "" && Int32.Parse(harmonic.Remove(harmonic.Length - 1)) != param["p"])
                    {
                        error += "--- parameter \"p\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (harmonic == "")
                    {
                        error += "--- filename don't contain parameter \"p\";\r\n";
                        nameError = true;
                    }

                    match = Regex.Match(featuresName, @"[\d]+[qQ]");
                    string degree = match.Value;
                    if (degree != "" && Int32.Parse(degree.Remove(degree.Length - 1)) != param["q"])
                    {
                        error += "--- parameter \"q\" has wrong value;\r\n";
                        nameError = true;
                    }
                    else if (degree == "")
                    {
                        error += "--- filename don't contain parameter \"q\";\r\n";
                        nameError = true;
                    }
                }
            }
            error += "\r\nDo you want to continue?";

            if (nameError)
                return MessageBox.Show(error, "Wrong Filename", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            else
                return MessageBoxResult.OK;
        }

        public bool TrySetFromString(string featuresName)
        {
            Match match;
            featuresName = Path.GetFileName(featuresName);
            if (featuresName.ToUpper().Contains("HAAR"))
            {
                featuresName = featuresName.Substring(featuresName.ToUpper().IndexOf("HAAR"));

                match = Regex.Match(featuresName, @"[\d]+[tT]");
                string templates = match.Value;
                if (templates != "")
                    parameters["t"] = Int32.Parse(templates.Remove(templates.Length - 1));

                match = Regex.Match(featuresName, @"[\d]+[pP]");
                string positions = match.Value;
                if (positions != "")
                    parameters["ps"] = Int32.Parse(positions.Remove(positions.Length - 1));

                match = Regex.Match(featuresName, @"[\d]+[sS]");
                string scales = match.Value;
                if (scales != "")
                    parameters["s"] = Int32.Parse(scales.Remove(scales.Length - 1));

                if (featureTypeComboBox.SelectedItem == harrItem)
                    ComboBox_SelectionChanged(featureTypeComboBox, null);
                else
                    featureTypeComboBox.SelectedItem = harrItem;

                return true;
            }
            else if (featuresName.ToUpper().Contains("HOG"))
            {
                featuresName = featuresName.Substring(featuresName.ToUpper().IndexOf("HOG"));

                match = Regex.Match(featuresName, @"[\d]+[bB]");
                string bins = match.Value;
                if (bins != "")
                    parameters["b"] = Int32.Parse(bins.Remove(bins.Length - 1));

                match = Regex.Match(featuresName, @"[\d]+[nN][xX]");
                string positionsX = match.Value;
                if (positionsX != "")
                    parameters["nx"] = Int32.Parse(positionsX.Remove(positionsX.Length - 2));

                match = Regex.Match(featuresName, @"[\d]+[nN][yY]");
                string positionsY = match.Value;
                if (positionsY != "")
                    parameters["ny"] = Int32.Parse(positionsY.Remove(positionsY.Length - 2));

                if (featureTypeComboBox.SelectedItem == hogItem)
                    ComboBox_SelectionChanged(featureTypeComboBox, null);
                else
                    featureTypeComboBox.SelectedItem = hogItem;

                return true;
            }
            else if (featuresName.ToUpper().Contains("PFMM"))
            {
                featuresName = featuresName.Substring(featuresName.ToUpper().IndexOf("PFMM"));

                match = Regex.Match(featuresName, @"[\d]+[rR]");
                string rings = match.Value;
                if (rings != "")
                    parameters["r"] = Int32.Parse(rings.Remove(rings.Length - 1));

                match = Regex.Match(featuresName, @"[\d]+[rR][tT]");
                string ringsType = match.Value;
                if (ringsType != "")
                    parameters["rt"] = Int32.Parse(ringsType.Remove(ringsType.Length - 2));
                else
                    parameters["rt"] = 1;

                match = Regex.Match(featuresName, @"[\d]+[pP]");
                string harmonic = match.Value;
                if (harmonic != "")
                    parameters["p"] = Int32.Parse(harmonic.Remove(harmonic.Length - 1));

                match = Regex.Match(featuresName, @"[\d]+[qQ]");
                string degree = match.Value;
                if (degree != "")
                    parameters["q"] = Int32.Parse(degree.Remove(degree.Length - 1));

                if (featureTypeComboBox.SelectedItem == pfmmItem)
                    ComboBox_SelectionChanged(featureTypeComboBox, null);
                else
                    featureTypeComboBox.SelectedItem = pfmmItem;

                return true;
            }
            else if (featuresName.ToUpper().Contains("ZERNIKE"))
            {
                string fet = featuresName;
                featuresName = featuresName.Substring(featuresName.ToUpper().IndexOf("ZERNIKE"));
             
                match = Regex.Match(featuresName, @"[\d]+[rR]");
                string rings = match.Value;
                if (rings != "")
                    parameters["r"] = Int32.Parse(rings.Remove(rings.Length - 1));

                match = Regex.Match(featuresName, @"[\d]+[rR][tT]");
                string ringsType = match.Value;
                if (ringsType != "")
                    parameters["rt"] = Int32.Parse(ringsType.Remove(ringsType.Length - 2));
                else
                    parameters["rt"] = 1;

                match = Regex.Match(featuresName, @"[\d]+[pP]");
                string harmonic = match.Value;
                if (harmonic != "")
                    parameters["p"] = Int32.Parse(harmonic.Remove(harmonic.Length - 1));

                match = Regex.Match(featuresName, @"[\d]+[qQ]");
                string degree = match.Value;
                if (degree != "")
                    parameters["q"] = Int32.Parse(degree.Remove(degree.Length - 1));

                //match = Regex.Match(featuresName, @"[\d]+[kK]");
                //string k = match.Value;
                //if (k != "")
                //    parameters["k"] = Int32.Parse(k.Remove(k.Length - 1));

                match = Regex.Match(featuresName, @"[\d]+[dD]");
                string width = match.Value;
                if (width != "")
                    parameters["d"] = Int32.Parse(width.Remove(width.Length - 1));

                match = Regex.Match(featuresName, @"[\d]+[wW]");
                string overlap = match.Value;
                if (overlap != "")
                    parameters["w"] = Int32.Parse(overlap.Remove(overlap.Length - 1));

                if (IsForDetection == true)
                {
                    if (fet.ToUpper().Contains("ZERNIKEPII"))
                    {
                        if (featureTypeComboBox.SelectedItem == zernikePiiItem)
                            ComboBox_SelectionChanged(featureTypeComboBox, null);
                        else
                            featureTypeComboBox.SelectedItem = zernikePiiItem;
                    }
                    else if (fet.ToUpper().Contains("FPIIINVARIANTS"))
                    {
                        if (featureTypeComboBox.SelectedItem == zernikeFPiiInvariantsItem)
                            ComboBox_SelectionChanged(featureTypeComboBox, null);
                        else
                            featureTypeComboBox.SelectedItem = zernikeFPiiInvariantsItem;
                    }
                    else if(fet.ToUpper().Contains("INVARIANTS"))
                    {
                        if (featureTypeComboBox.SelectedItem == zernikeInvariantsItem)
                            ComboBox_SelectionChanged(featureTypeComboBox, null);
                        else
                            featureTypeComboBox.SelectedItem = zernikeInvariantsItem;
                    }
                    else if (fet.ToUpper().Contains("ZERNIKEFPII"))
                    {
                        if (featureTypeComboBox.SelectedItem == zernikeFPiiItem)
                            ComboBox_SelectionChanged(featureTypeComboBox, null);
                        else
                            featureTypeComboBox.SelectedItem = zernikeFPiiItem;
                    }
                    else if (fet.ToUpper().Contains("ZERNIKEZII"))
                    {
                        if (featureTypeComboBox.SelectedItem == zernikeZiiItem)
                            ComboBox_SelectionChanged(featureTypeComboBox, null);
                        else
                            featureTypeComboBox.SelectedItem = zernikeZiiItem;
                    }
                    else
                    {
                        if (featureTypeComboBox.SelectedItem == zernikeItem)
                            ComboBox_SelectionChanged(featureTypeComboBox, null);
                        else
                            featureTypeComboBox.SelectedItem = zernikeItem;
                    }
                }
                else
                {
                    if (fet.ToUpper().Contains("INVARIANTS"))
                    {
                        if (featureTypeComboBox.SelectedItem == zernikeInvariantsItem)
                            ComboBox_SelectionChanged(featureTypeComboBox, null);
                        else
                            featureTypeComboBox.SelectedItem = zernikeInvariantsItem;
                    }
                    else
                    {
                        if (featureTypeComboBox.SelectedItem == zernikeItem)
                            ComboBox_SelectionChanged(featureTypeComboBox, null);
                        else
                            featureTypeComboBox.SelectedItem = zernikeItem;
                    }
                }

                return true;
            }

            return false;
        }
        
        public void SetMinimum(decimal minimum, string tag)
        {
            foreach (UIElement ctrl in mainGrid.Children)
            {
                if (ctrl is NumericUpDown && ((NumericUpDown)ctrl).Tag.ToString() == tag)
                {
                    ((NumericUpDown)ctrl).Minimum = minimum;
                    ((NumericUpDown)ctrl).Value = minimum; // ((NumericUpDown)ctrl).Value;
                }
            }
        }
        #endregion

        #region Triggers
        public event EventHandler<EventArgs> CheckedChanged;
        protected virtual void OnCheckedChanged(EventArgs e)
        {
            CheckedChanged?.Invoke(this, e);
        }

        public event EventHandler<EventArgs> ValueChanged;
        protected virtual void OnValueChanged(EventArgs e)
        {
            ValueChanged?.Invoke(this, e);
        }
        #endregion

        #region Events
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsInitialized)
            {
                ComboBox cb = (ComboBox)sender;
                string extractor = (string)((ComboBoxItem)cb.SelectedItem).Tag;
                ExtractorName = extractor;

                parameter4Label.Visibility = System.Windows.Visibility.Hidden;
                parameter5Label.Visibility = System.Windows.Visibility.Hidden;
                parameter6Label.Visibility = System.Windows.Visibility.Hidden;
                parameter7Label.Visibility = System.Windows.Visibility.Hidden;
                parameter8Label.Visibility = System.Windows.Visibility.Hidden;
                parameter9Label.Visibility = System.Windows.Visibility.Hidden;
                parameter4NumericUpDown.Visibility = System.Windows.Visibility.Hidden;
                parameter5NumericUpDown.Visibility = System.Windows.Visibility.Hidden;
                parameter6NumericUpDown.Visibility = System.Windows.Visibility.Hidden;
                parameter7NumericUpDown.Visibility = System.Windows.Visibility.Hidden;
                parameter8NumericUpDown.Visibility = System.Windows.Visibility.Hidden;
                parameter9NumericUpDown.Visibility = System.Windows.Visibility.Hidden;

                if (extractor == "HaarExtractor")
                {
                    parameter1Label.Content = "Templates:";
                    parameter2Label.Content = "Sizes:";
                    parameter3Label.Content = "Positions:";
                    parameter1NumericUpDown.Tag = "t";
                    parameter2NumericUpDown.Tag = "s";
                    parameter3NumericUpDown.Tag = "ps";

                    parameter1NumericUpDown.Minimum = 1;
                    parameter1NumericUpDown.Maximum = 6;
                    parameter1NumericUpDown.Value = parameters["t"];

                    parameter2NumericUpDown.Minimum = 1;
                    parameter2NumericUpDown.Maximum = 8;
                    parameter2NumericUpDown.Value = parameters["s"];

                    parameter3NumericUpDown.Minimum = 1;
                    parameter3NumericUpDown.Maximum = 8;
                    parameter3NumericUpDown.Value = parameters["ps"];

                    int featureCount = (int)(Math.Pow(parameters["s"], 2) * Math.Pow(parameters["ps"], 2) * parameters["t"]);
                    featureCountLabel.Content = featureCount;
                }
                else if (extractor == "HOGExtractor")
                {
                    parameter1Label.Content = "Bins:";
                    parameter2Label.Content = "Blocks (X):";
                    parameter3Label.Content = "Blocks (Y):";
                    parameter1NumericUpDown.Tag = "b";
                    parameter2NumericUpDown.Tag = "nx";
                    parameter3NumericUpDown.Tag = "ny";

                    parameter1NumericUpDown.Minimum = 4;
                    parameter1NumericUpDown.Maximum = 1024;
                    parameter1NumericUpDown.Value = parameters["b"];

                    parameter2NumericUpDown.Minimum = 1;
                    parameter2NumericUpDown.Maximum = 20;
                    parameter2NumericUpDown.Value = parameters["nx"];

                    parameter3NumericUpDown.Minimum = 1;
                    parameter3NumericUpDown.Maximum = 20;
                    parameter3NumericUpDown.Value = parameters["ny"];

                    int featureCount = (int)(parameters["b"] * parameters["nx"] * parameters["ny"]);
                    featureCountLabel.Content = featureCount;
                }
                else if (extractor == "PFMMExtractor" || extractor.StartsWith("Zernike"))
                {
                    parameter1Label.Content = "Harmonic (\"p\"):";
                    parameter2Label.Content = "Degree (\"q\"):";
                    parameter3Label.Content = "Rings (\"r\"):";
                    parameter1NumericUpDown.Tag = "p";
                    parameter2NumericUpDown.Tag = "q";
                    parameter3NumericUpDown.Tag = "r";

                    parameter1NumericUpDown.Minimum = 1;
                    parameter1NumericUpDown.Maximum = 20;
                    parameter1NumericUpDown.Value = parameters["p"];

                    parameter2NumericUpDown.Minimum = 1;
                    parameter2NumericUpDown.Maximum = 20;
                    parameter2NumericUpDown.Value = parameters["q"];

                    parameter3NumericUpDown.Minimum = 1;
                    parameter3NumericUpDown.Maximum = 20;
                    parameter3NumericUpDown.Value = parameters["r"];

                    parameter4Label.Content = "Rings type (\"rt\"):";
                    parameter4Label.Visibility = System.Windows.Visibility.Visible;
                    parameter4NumericUpDown.Tag = "rt";
                    parameter4NumericUpDown.Visibility = System.Windows.Visibility.Visible;

                    parameter4NumericUpDown.Maximum = 1;
                    parameter4NumericUpDown.Minimum = 0;
                    parameter4NumericUpDown.Value = parameters["rt"];

                    if (extractor == "PFMMExtractor")
                    {
                        int featureCount = 0;
                        if (parameters["rt"] == 0)
                        {
                            featureCount = (int)(parameters["r"] * (parameters["p"] + 1 - Math.Min(parameters["p"], parameters["q"]) / 2.0) * (Math.Min(parameters["p"], parameters["q"]) + 1));
                        }
                        else if (parameters["rt"] == 1)
                        {
                            featureCount = (int)((2 * parameters["r"] - 1) * (parameters["p"] + 1 - Math.Min(parameters["p"], parameters["q"]) / 2.0) * (Math.Min(parameters["p"], parameters["q"]) + 1));
                        }
                        featureCountLabel.Content = featureCount;
                    }
                    else if (extractor == "ZernikeInvariantsExtractor" || extractor == "ZernikeFPiiInvariantsExtractor")
                    {
                        int featureCount = 0;
                        int rings = parameters["r"];
                        int p_max = parameters["p"];
                        int q_max = parameters["q"];
                        int ringsType = parameters["rt"];

                        for (int r = 0; r < rings; r++)
                        {
                            int hMax = 0;
                            if (ringsType == 1)
                                hMax = (r == rings - 1) ? 0 : 1;

                            for (int rt = 0; rt <= hMax; rt++)
                            {
                                featureCount++;

                                for (int k = 1; k <= p_max; k++)
                                {
                                    int q_min = 1;
                                    if (k == 1) q_min = 0;
                                    for (int q = q_min; q <= q_max; q++)
                                    {
                                        int s = k * q;
                                        for (int p = q; p <= p_max; p += 2)
                                        {
                                            int v_min = s;
                                            if (k == 1)
                                            {
                                                v_min = p;
                                                if (q == 0)
                                                    v_min = p + 2;
                                            }
                                            for (int v = v_min; v <= q_max; v += 2)
                                            {
                                                featureCount++;
                                                if (!((k == 1 && p == v) || q == 0))
                                                    featureCount++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        featureCountLabel.Content = featureCount;
                    }
                    else
                    {
                        //parameter5Label.Content = "Multiplier (\"k\"):";
                        //parameter5Label.Visibility = System.Windows.Visibility.Visible;
                        //parameter5NumericUpDown.Tag = "k";
                        //parameter5NumericUpDown.Visibility = System.Windows.Visibility.Visible;

                        //parameter5NumericUpDown.Maximum = 10000;
                        //parameter5NumericUpDown.Minimum = 0;
                        //parameter5NumericUpDown.Value = parameters["k"];

                        int featureCount = 0;
                        int r = parameters["r"];
                        int p = parameters["p"];
                        int q = parameters["q"];
                        //int k = parameters["k"];

                        q = Math.Min(p, q);
                        if (parameters["rt"] == 0)
                        {
                            featureCount = (int)(r * ((2 * Math.Floor(p / 2.0) - Math.Ceiling(q / 2.0) + 3) * Math.Ceiling(q / 2.0) + (1 - q % 2) * (Math.Floor(p / 2.0) - Math.Ceiling(q / 2.0) + 1) - (1 - p % 2) * Math.Ceiling(q / 2.0)));
                        }
                        else if (parameters["rt"] == 1)
                        {
                            featureCount = (int)((2 * r - 1) * ((2 * Math.Floor(p / 2.0) - Math.Ceiling(q / 2.0) + 3) * Math.Ceiling(q / 2.0) + (1 - q % 2) * (Math.Floor(p / 2.0) - Math.Ceiling(q / 2.0) + 1) - (1 - p % 2) * Math.Ceiling(q / 2.0)));
                        }
                        featureCountLabel.Content = featureCount;
                    }

                    if (extractor == "ZernikePiiExtractor" || extractor == "ZernikeZiiExtractor" || extractor == "ZernikeInvariantsExtractor" 
                        || extractor == "ZernikeFPiiExtractor" || extractor == "ZernikeFPiiInvariantsExtractor")
                    {
                        parameter5Label.Content = "Width (\"d\"):";
                        parameter5Label.Visibility = System.Windows.Visibility.Visible;
                        parameter5NumericUpDown.Tag = "d";
                        parameter5NumericUpDown.Visibility = System.Windows.Visibility.Visible;
       
                        parameter5NumericUpDown.Maximum = 10000;
                        parameter5NumericUpDown.Minimum = 40;
                        parameter5NumericUpDown.Value = parameters["d"];

                        if (extractor == "ZernikeZiiExtractor")
                        {
                            parameter6Label.Content = "Overlap (\"w\"):";
                            parameter6Label.Visibility = System.Windows.Visibility.Visible;
                            parameter6NumericUpDown.Tag = "w";
                            parameter6NumericUpDown.Visibility = System.Windows.Visibility.Visible;

                            parameter6NumericUpDown.Maximum = 10000;
                            parameter6NumericUpDown.Minimum = 40;
                            parameter6NumericUpDown.Value = parameters["w"];
                        }
                    }
                }


                OnCheckedChanged(EventArgs.Empty);
            }
        }

        private void NumericUpDown_ValueChanged(object sender, ValueChangedEventArg e)
        {
            NumericUpDown parameter = (NumericUpDown)sender;
            if (parameter.Tag != null)
            {
                parameters[parameter.Tag.ToString()] = (int)(decimal)e.CurrentValue;

                OnValueChanged(EventArgs.Empty);
            }

            if (ExtractorName == "HaarExtractor")
            {
                int featureCount = (int)(Math.Pow(parameters["s"], 2) * Math.Pow(parameters["ps"], 2) * parameters["t"]);
                featureCountLabel.Content = featureCount;
            }
            else if (ExtractorName == "HOGExtractor")
            {
                int featureCount = (int)(parameters["b"] * parameters["nx"] * parameters["ny"]);
                featureCountLabel.Content = featureCount;
            }
            else if (ExtractorName == "PFMMExtractor")
            {
                int featureCount = 0;
                if (parameters["rt"] == 0)
                {
                    featureCount = (int)(parameters["r"] * (parameters["p"] + 1 - Math.Min(parameters["p"], parameters["q"]) / 2.0) * (Math.Min(parameters["p"], parameters["q"]) + 1));
                }
                else if (parameters["rt"] == 1)
                {
                    featureCount = (int)((2 * parameters["r"] - 1) * (parameters["p"] + 1 - Math.Min(parameters["p"], parameters["q"]) / 2.0) * (Math.Min(parameters["p"], parameters["q"]) + 1));
                }
                featureCountLabel.Content = featureCount;
            }
            else if (ExtractorName.StartsWith("Zernike"))
            {
                int featureCount = 0;
                if (!ExtractorName.Contains("Invariants"))
                {
                    int r = parameters["r"];
                    int p = parameters["p"];
                    int q = parameters["q"];

                    q = Math.Min(p, q);
                    if (parameters["rt"] == 0)
                    {
                        featureCount = (int)(r * ((2 * Math.Floor(p / 2.0) - Math.Ceiling(q / 2.0) + 3) * Math.Ceiling(q / 2.0) + (1 - q % 2) * (Math.Floor(p / 2.0) - Math.Ceiling(q / 2.0) + 1) - (1 - p % 2) * Math.Ceiling(q / 2.0)));
                    }
                    else if (parameters["rt"] == 1)
                    {
                        featureCount = (int)((2 * r - 1) * ((2 * Math.Floor(p / 2.0) - Math.Ceiling(q / 2.0) + 3) * Math.Ceiling(q / 2.0) + (1 - q % 2) * (Math.Floor(p / 2.0) - Math.Ceiling(q / 2.0) + 1) - (1 - p % 2) * Math.Ceiling(q / 2.0)));
                    }
                }
                else
                {
                    int rings = parameters["r"];
                    int p_max = parameters["p"];
                    int q_max = parameters["q"];
                    int ringsType = parameters["rt"];

                    for (int r = 0; r < rings; r++)
                    {
                        int hMax = 0;
                        if (ringsType == 1)
                            hMax = (r == rings - 1) ? 0 : 1;

                        for (int rt = 0; rt <= hMax; rt++)
                        {
                            featureCount++;

                            for (int k = 1; k <= p_max; k++)
                            {
                                int q_min = 1;
                                if (k == 1) q_min = 0;
                                for (int q = q_min; q <= q_max; q++)
                                {
                                    int s = k * q;
                                    for (int p = q; p <= p_max; p += 2)
                                    {
                                        int v_min = s;
                                        if (k == 1)
                                        {
                                            v_min = p;
                                            if (q == 0)
                                                v_min = p + 2;
                                        }
                                        for (int v = v_min; v <= q_max; v += 2)
                                        {
                                            featureCount++;
                                            if (!((k == 1 && p == v) || q == 0))
                                                featureCount++;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                featureCountLabel.Content = featureCount;
            }
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsForDetection && featureTypeComboBox.Items.Count == 5)
            {
                featureTypeComboBox.Items.Insert(1, zernikePiiItem);
                featureTypeComboBox.Items.Insert(2, zernikeZiiItem);
                featureTypeComboBox.Items.Insert(3, zernikeFPiiItem);
                featureTypeComboBox.Items.Insert(5, zernikeFPiiInvariantsItem);

                featureTypeComboBox.Items.Refresh();
            }
        }
        #endregion
    }
}
