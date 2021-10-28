using System;
using System.Windows;
using System.Windows.Controls;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for ImageFileTypeSelector.xaml
    /// </summary>
    public partial class ImageFileTypeSelector : UserControl
    {
        public enum FileType
        {
            text = 1,
            binary8bit = 2,
            binary64bit = 3,

            jpeg = 4,
            bitmap = 5,
            png = 6
        };

        #region Properties
        public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register("Title", typeof(string), typeof(ImageFileTypeSelector), new UIPropertyMetadata("File Type:", new PropertyChangedCallback(OnTitleChanged)));
        private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageFileTypeSelector obj = (ImageFileTypeSelector)d;
            obj.Title = (string)e.NewValue;
        }

        public String Title
        {
            get { return (string)fileTypeGroupBox.Header; }
            set { fileTypeGroupBox.Header = value; }
        }

        public static readonly DependencyProperty EnableImageProperty =
        DependencyProperty.Register("EnableImage", typeof(Boolean), typeof(ImageFileTypeSelector), new UIPropertyMetadata(true, new PropertyChangedCallback(OnEnableImageChanged)));
        private static void OnEnableImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageFileTypeSelector obj = (ImageFileTypeSelector)d;
            obj.EnableImage = (Boolean)e.NewValue;
        }

        private Boolean enableImage = true;
        public Boolean EnableImage
        {
            get { return enableImage; }
            set
            {
                enableImage = value;
                if(enableImage)
                {
                    imageRadioButton.IsEnabled = true;
                    imageGrid.IsEnabled = true;
                    mainGrid.ColumnDefinitions[3].Width = new GridLength(2, GridUnitType.Pixel);
                    mainGrid.ColumnDefinitions[4].Width = new GridLength(1, GridUnitType.Star);
                }
                else
                {
                    if (imageRadioButton.IsChecked == true)
                        byteRadioButton.IsChecked = true;
                    imageRadioButton.IsEnabled = false;
                    imageGrid.IsEnabled = false;
                    mainGrid.ColumnDefinitions[3].Width = new GridLength(0, GridUnitType.Pixel);
                    mainGrid.ColumnDefinitions[4].Width = new GridLength(0, GridUnitType.Pixel);
                }
            }
        }

        public static readonly DependencyProperty EnableSetingPrecisionProperty =
        DependencyProperty.Register("EnableSetingPrecision", typeof(Boolean), typeof(ImageFileTypeSelector), new UIPropertyMetadata(true, new PropertyChangedCallback(OnEnableSetingPrecisionChanged)));
        private static void OnEnableSetingPrecisionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ImageFileTypeSelector obj = (ImageFileTypeSelector)d;
            obj.EnableSetingPrecision = (Boolean)e.NewValue;
        }

        private Boolean enableSetingPrecision = true;
        public Boolean EnableSetingPrecision
        {
            get { return enableSetingPrecision; }
            set
            {
                enableSetingPrecision = value;
                if (enableSetingPrecision)
                {
                    decimalPlacesNumericUpDown.IsEnabled = true;
                }
                else
                {
                    decimalPlacesNumericUpDown.IsEnabled = false;
                }
            }
        }

        private FileType selectedFileType = FileType.binary8bit;
        public FileType SelectedFileType
        {
            private set { selectedFileType = value; }
            get { return selectedFileType; }
        }

        public int DecimalPlaces
        {
            get { return (int)decimalPlacesNumericUpDown.Value; }
        }
        #endregion

        public ImageFileTypeSelector()
        {
            InitializeComponent();
        }

        private void BinaryRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (binaryRadioButton?.IsChecked == true)
            {
                if (doubleRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.binary64bit;
                if (byteRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.binary8bit;
            }
        }

        private void TextRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (textRadioButton?.IsChecked == true)
                SelectedFileType = FileType.text;
        }

        private void DoubleRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (binaryRadioButton?.IsChecked == true)
            {
                if (doubleRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.binary64bit;
            }
        }

        private void ByteRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (binaryRadioButton?.IsChecked == true)
            {
                if (byteRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.binary8bit;
            }
        }

        private void ImageRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (imageRadioButton?.IsChecked == true)
            {
                if (jpgRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.jpeg;
                if (bmpRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.bitmap;
                if (pngRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.png;
            }
        }

        private void JpgRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (imageRadioButton?.IsChecked == true)
            {
                if (jpgRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.jpeg;
            }
        }

        private void BmpRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (imageRadioButton?.IsChecked == true)
            {
                if (bmpRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.bitmap;
            }
        }

        private void PngRadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (imageRadioButton?.IsChecked == true)
            {
                if (pngRadioButton?.IsChecked == true)
                    SelectedFileType = FileType.png;
            }
        }
    }
}
