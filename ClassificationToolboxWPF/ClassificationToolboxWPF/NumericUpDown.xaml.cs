using System;
using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Windows.Input;

namespace ClassificationToolboxWPF
{
    /// <summary>
    /// Interaction logic for NumericUpDown.xaml
    /// </summary>
    public partial class NumericUpDown : UserControl
    {
        #region Properties
        private static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register("Maximum", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(100M));

        private static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register("Minimum", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(0M));

        private static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register("Value", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(0M));

        private static readonly DependencyProperty IncrementProperty =
        DependencyProperty.Register("Increment", typeof(decimal), typeof(NumericUpDown), new PropertyMetadata(1M));

        private static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register("DecimalPlaces", typeof(int), typeof(NumericUpDown), new PropertyMetadata(0));

        public static readonly DependencyProperty VerticalTextAligmentProperty =
        DependencyProperty.Register("VerticalTextAligment", typeof(VerticalAlignment), typeof(NumericUpDown));

        public static readonly DependencyProperty HorizontalTextAligmentProperty =
        DependencyProperty.Register("HorizontalTextAligment", typeof(HorizontalAlignment), typeof(NumericUpDown));

        //-------------------------------------

        public VerticalAlignment VerticalTextAligment
        {
            get { return (VerticalAlignment)GetValue(VerticalTextAligmentProperty); }
            set { SetValue(VerticalTextAligmentProperty, value); }
        }

        public HorizontalAlignment HorizontalTextAligment
        {
            get { return (HorizontalAlignment)GetValue(HorizontalTextAligmentProperty); }
            set { SetValue(HorizontalTextAligmentProperty, value); }
        }

        public decimal Maximum
        {
            get { return (decimal)GetValue(MaximumProperty); }
            set
            {
                decimal maximum;
                if (value < Minimum)
                    maximum = Minimum;
                else
                    maximum = value;
                SetValue(MaximumProperty, maximum);

                //if (maximum < Value)
                //    Value = maximum;
            }
        }

        public decimal Minimum
        {
            get { return (decimal)GetValue(MinimumProperty); }
            set
            {
                decimal minimum;
                if (value > Maximum)
                    minimum = Maximum;
                else
                    minimum = value;
                SetValue(MinimumProperty, minimum);

                //if (minimum > Value)
                //    Value = minimum;
            }
        }
        
        public int DecimalPlaces
        {
            get { return (int)GetValue(DecimalPlacesProperty); }
            set
            {
                int decimalPlaces;
                if (value < 0)
                    decimalPlaces = 0;
                else
                    decimalPlaces = value;
                SetValue(DecimalPlacesProperty, decimalPlaces);
                numberTextBox.Text = DecimalToString(Value);
            }
        }
        
        public decimal Value
        {
            get { return (decimal)GetValue(ValueProperty); }
            set
            {
                decimal newValue = value;
                if (newValue < Minimum)
                    newValue = Minimum;
                else if (newValue > Maximum)
                    newValue = Maximum;

                decimal previousValue = (decimal)GetValue(ValueProperty);
                if (previousValue != newValue)
                {
                    SetValue(ValueProperty, newValue);
                    numberTextBox.Text = DecimalToString(newValue);

                    OnValueChanged(new ValueChangedEventArg(previousValue, newValue));
                }
            }
        }
        
        public decimal Increment
        {
            get { return (decimal)GetValue(IncrementProperty); }
            set
            {
                decimal increment = value;
                if (increment > Maximum - Minimum)
                    increment = Maximum - Minimum;
                if (increment < 1.0M / (int)Math.Pow(10, DecimalPlaces))
                    increment = 1.0M / (int)Math.Pow(10, DecimalPlaces);
                SetValue(IncrementProperty, increment);
            }
        }
        #endregion

        public NumericUpDown()
        {
            InitializeComponent();
        }

        private string DecimalToString(decimal value)
        {
             return DecimalToString(value, (DecimalPlaces >= 0) ? DecimalPlaces : 0);
        }

        private static string DecimalToString(decimal number, int decimalPlaces)
        {
            string format = "0" + ((decimalPlaces > 0) ? "." + new String('0', decimalPlaces) : "");
            return number.ToString(format, CultureInfo.InvariantCulture);
        }

        private decimal ValidateAndParse()
        {
            if (!Decimal.TryParse(numberTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
                number = Value;
            if (number < Minimum)
                number = Minimum;
            else if (number > Maximum)
                number = Maximum;
            int shift = (int)Math.Pow(10, DecimalPlaces);
            number = Math.Floor(number * shift) / shift;

            return number;
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (Value + Increment <= Maximum)
                Value += Increment;
            else
                Value = Maximum;
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            if (Value - Increment >= Minimum)
                Value -= Increment;
            else
                Value = Minimum;
        }

        private void NumberTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            bool handled = true;
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
                handled = false;
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                handled = false;
            else if (e.Key == Key.OemPeriod)
                handled = false;
            else if ((e.Key == Key.OemMinus || e.Key == Key.Subtract))
                handled = false;

            e.Handled = handled;
        }

        private void NumberTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Value = ValidateAndParse();

                numberTextBox.Text = DecimalToString(Value);
            }
        }

        private void NumberTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Value = ValidateAndParse();

            numberTextBox.Text = DecimalToString(Value);
        }

        private void NumericUpDown_Loaded(object sender, RoutedEventArgs e)
        {
            if (Value < Minimum)
                Value = Minimum;
            else if (Value > Maximum)
                Value = Maximum;

            numberTextBox.Text = DecimalToString(Value);
        }

        public event EventHandler<ValueChangedEventArg> ValueChanged;
        protected virtual void OnValueChanged(ValueChangedEventArg e)
        {
            ValueChanged?.Invoke(this, e);
        }
    }
}