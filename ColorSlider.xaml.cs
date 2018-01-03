using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SkyWest.Common.WPF
{
    /*
     * Custom Three-Thumb WPF Color Slider
     */
    public partial class ColorSlider : Grid
    {
        private double BarWidth; //ActualWidth - 17
        private double AdjustedBarWidth; //BarWidth + 7

        private bool IsDragging = false;
        private bool IsLoading = true;

        private double ValueDragStarted;

        public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register("MaxValue", typeof(int), typeof(ColorSlider));

        public static readonly DependencyProperty UpperValueProperty = DependencyProperty.Register("UpperValue", typeof(double), typeof(ColorSlider));

        public static readonly DependencyProperty MidValueProperty = DependencyProperty.Register("MidValue", typeof(double), typeof(ColorSlider));

        public static readonly DependencyProperty LowerValueProperty = DependencyProperty.Register("LowerValue", typeof(double), typeof(ColorSlider));

        public static readonly RoutedEvent UpperValueChangedEvent = EventManager.RegisterRoutedEvent(
       "UpperValueChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ColorSlider));

        public static readonly RoutedEvent MidValueChangedEvent = EventManager.RegisterRoutedEvent(
       "MidValueChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ColorSlider));

        public static readonly RoutedEvent LowerValueChangedEvent = EventManager.RegisterRoutedEvent(
       "LowerValueChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ColorSlider));

        public ColorSlider()
        {
            IsLoading = true;
            InitializeComponent();
        }

        #region Properties and Events

        public bool StrictBoundaries
        {
            get; set;
        }

        public event RoutedEventHandler UpperValueChanged
        {
            add { AddHandler(UpperValueChangedEvent, value); }
            remove { RemoveHandler(UpperValueChangedEvent, value); }
        }

        public void RaiseUpperValueChanged()
        {
            RoutedEventArgs newEventArgs = new RoutedEventArgs(UpperValueChangedEvent);
            RaiseEvent(newEventArgs);
        }

        public event RoutedEventHandler MidValueChanged
        {
            add { AddHandler(MidValueChangedEvent, value); }
            remove { RemoveHandler(MidValueChangedEvent, value); }
        }

        public void RaiseMidValueChanged()
        {
            RoutedEventArgs newEventArgs = new RoutedEventArgs(MidValueChangedEvent);
            RaiseEvent(newEventArgs);
        }

        public event RoutedEventHandler LowerValueChanged
        {
            add { AddHandler(LowerValueChangedEvent, value); }
            remove { RemoveHandler(LowerValueChangedEvent, value); }
        }

        public void RaiseLowerValueChanged()
        {
            RoutedEventArgs newEventArgs = new RoutedEventArgs(LowerValueChangedEvent);
            RaiseEvent(newEventArgs);
        }

        public double UpperValue
        {
            get { return Convert.ToDouble(GetValue(UpperValueProperty)); }
            set
            {
                try
                {
                    if (StrictBoundaries)
                    {
                        if (value < MidValue)
                            throw new Exception("Upper value cannot be below middle value.");

                        if (value > MaxValue)
                            throw new Exception("Upper value cannot be above max value.");

                        if (value < 0)
                            throw new Exception("Upper value cannot be below zero.");
                    }

                    if (value < 0)
                        value = 0;

                    if (UpperValue == value)
                        return;

                    SetValue(UpperValueProperty, value);

                    if (!IsDragging && !IsLoading)
                    {
                        RaiseUpperValueChanged();
                        ResetWidths();
                    }
                } catch(Exception e)
                {
                    MessageBox.Show(e.Message, "Invalid Slider Value", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public double MidValue
        {
            get { return Convert.ToDouble(GetValue(MidValueProperty)); }
            set
            {
                try
                {
                    if (StrictBoundaries)
                    {
                        if (value < LowerValue)
                            throw new Exception("Middle value cannot be below lower value.");

                        if (value > UpperValue)
                            throw new Exception("Middle value cannot be above upper value.");

                        if (value > MaxValue)
                            throw new Exception("Middle value cannot be above max value.");

                        if (value < 0)
                            throw new Exception("Middle value cannot be below zero.");
                    }

                    if (value < 0)
                        value = 0;

                    if (MidValue == value)
                        return;

                    SetValue(MidValueProperty, value);

                    if (!IsDragging && !IsLoading)
                    {
                        RaiseMidValueChanged();
                        ResetWidths();
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Invalid Slider Value", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public double LowerValue
        {
            get { return Convert.ToDouble(GetValue(LowerValueProperty)); }
            set
            {
                try
                {
                    if (StrictBoundaries)
                    {
                        if (value < 0)
                            throw new Exception("Lower value cannot be below zero.");

                        if (value > MidValue)
                            throw new Exception("Lower value cannot be above middle value.");

                        if (value > MaxValue)
                            throw new Exception("Lower value cannot be above max value.");
                    }

                    if (value < 0)
                        value = 0;

                    if (LowerValue == value)
                        return;

                    SetValue(LowerValueProperty, value);

                    if (!IsDragging && !IsLoading)
                    {
                        RaiseLowerValueChanged();
                        LowerSlider.Value = value;
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Invalid Slider Value", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public int MaxValue
        {
            get { return Convert.ToInt32(GetValue(MaxValueProperty)); }
            set {
                SetValue(MaxValueProperty, value); ResetWidths();

                //automatically set any sliders who are now outside the range
                if (UpperValue > MaxValue)
                    UpperValue = MaxValue;

                if (MidValue >= UpperValue)
                    MidValue = UpperValue - .2;

                if (LowerValue >= MidValue)
                    LowerValue = MidValue - .2;
            }
        }

        #endregion Properties and Events

        #region Event Handlers

        public void ColorSlider_Loaded(object sender, RoutedEventArgs e)
        {
            ResetWidths();
            IsLoading = false;
        }

        public void ColorSlider_SizeChanged(object sender, RoutedEventArgs e)
        {
            ResetWidths();
        }

        public void ResetWidths()
        {
            if (IsDragging)
                return;

            BarWidth = ActualWidth - 17;
            AdjustedBarWidth = BarWidth + 7;

            //If MaxValue is not defined, default to 5
            if (MaxValue == 0)
                MaxValue = 5;

            if (UpperSlider.Template.FindName("PART_Track", UpperSlider) != null)
            {
                (UpperSlider.Template.FindName("PART_Track", UpperSlider) as System.Windows.Controls.Primitives.Track).Maximum = MaxValue;
                (MidSlider.Template.FindName("PART_Track", MidSlider) as System.Windows.Controls.Primitives.Track).Maximum = MaxValue;
                (LowerSlider.Template.FindName("PART_Track", LowerSlider) as System.Windows.Controls.Primitives.Track).Maximum = MaxValue;
            }
            UpperSlider.Maximum = MaxValue;

            //reset values with new size (margins to correct number, value to 0)
            double UpperLeftMargin = ValueToMarginInit(UpperValue);
            double MidLeftMargin = ValueToMarginInit(MidValue);

            UpperSlider.Margin = new Thickness((UpperLeftMargin < 20) ? 20 : UpperLeftMargin, 0, 0, 0);
            MidSlider.Margin = new Thickness((MidLeftMargin < 10) ? 10 : MidLeftMargin, 0, 0, 0);

            LowerSlider.Value = LowerValue;
        }

        private void Slider_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            //prevents use of arrow keys on sliders, which messes up functionality.
            if (e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
            {
                e.Handled = true;
            }
        }

        private void UpperSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            IsDragging = true;
            double temp = UpperSlider.Margin.Left;
            UpperSlider.Margin = new Thickness(MidSlider.Margin.Left + 10, 0, 0, 0);
            UpperSlider.Value = (((temp - UpperSlider.Margin.Left) / (BarWidth - UpperSlider.Margin.Left + 7)) * MaxValue);
            ValueDragStarted = UpperValue;
        }

        private void UpperSlider_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double adjustedValue = ((((BarWidth - UpperSlider.Margin.Left) * (UpperSlider.Value / MaxValue)) + UpperSlider.Margin.Left) / BarWidth) * MaxValue;
            UpperValue = Math.Round(adjustedValue, 1);
        }

        private void UpperSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            IsDragging = false;
            UpperSlider.Margin = new Thickness(((BarWidth - UpperSlider.Margin.Left + 7) * (UpperSlider.Value / MaxValue)) + UpperSlider.Margin.Left, 0, 0, 0);
            UpperSlider.Value = 0;

            if (UpperValue >= MaxValue)
                UpperValue = MaxValue;

            if (UpperValue != ValueDragStarted)
                RaiseUpperValueChanged();
        }

        private void MidSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            IsDragging = true;
            double temp = MidSlider.Margin.Left;
            MidSlider.Margin = new Thickness(ValueToMargin(LowerSlider.Value) + 10, 0, 0, 0);
            MidSlider.Value = ((temp - ((LowerSlider.Value / MaxValue) * (BarWidth - 12)) - 10) / (BarWidth - ((LowerSlider.Value / MaxValue) * (BarWidth - 12)) - 3)) * MaxValue; //-3 = -10 + 7
            MidSlider.Maximum = (((UpperSlider.Margin.Left - MidSlider.Margin.Left) / (AdjustedBarWidth - MidSlider.Margin.Left)) * MaxValue) - (10 / ((AdjustedBarWidth - MidSlider.Margin.Left) / MaxValue));
            ValueDragStarted = MidValue;
        }

        private void MidSlider_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double adjustedValue = ((((BarWidth - MidSlider.Margin.Left) * (MidSlider.Value / MaxValue)) + MidSlider.Margin.Left) / BarWidth) * MaxValue;
            MidValue = Math.Round(adjustedValue, 1);
        }

        private void MidSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            IsDragging = false;
            MidSlider.Margin = new Thickness(((BarWidth - MidSlider.Margin.Left + 7) * (MidSlider.Value / MaxValue)) + MidSlider.Margin.Left, 0, 0, 0);
            MidSlider.Value = 0;

            if (MidValue != ValueDragStarted)
                RaiseMidValueChanged();
        }

        private void LowerSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            IsDragging = true;
            LowerSlider.Maximum = MarginToValue(MidSlider.Margin.Left-10);
            ValueDragStarted = LowerValue;
        }

        private void LowerSlider_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double adjustedValue = Math.Round(LowerSlider.Value, 1);
            LowerValue = (adjustedValue > MidValue) ? MidValue : adjustedValue;
        }

        private void LowerSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            IsDragging = false;
            if (LowerValue != ValueDragStarted)
                RaiseLowerValueChanged();
        }

        #endregion Event Handlers

        #region Functions

        public double MarginToValue(double Margin)
        {
            return ((Margin / (BarWidth - 12)) * MaxValue);
        }

        public double ValueToMargin(double Value)
        {
            return ((Value / MaxValue) * (BarWidth - 12));
        }

        public double ValueToMarginInit(double Value)
        {
            return ((Value / MaxValue) * AdjustedBarWidth);
        }

        #endregion Functions
    }
}
