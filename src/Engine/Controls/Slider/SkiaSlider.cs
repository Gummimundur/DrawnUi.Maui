﻿using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DrawnUi.Maui.Draw;

public class SkiaSlider : SkiaLayout
{
    #region GESTURES

    protected bool IsUserPanning { get; set; }

    /// <summary>
    /// enlarge hotspot by pts
    /// </summary>
    public double moreHotspotSize = 10.0;

    protected double lastTouchX;

    /// <summary>
    /// track touched area type
    /// </summary>
    protected RangeZone touchArea;

    protected Vector2 _panningStartOffsetPts;

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        if (Trail == null)
        {
            Trail = FindViewByTag("Trail");
        }
    }

    public SkiaControl Trail { get; set; }

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        //Super.Log($"[Touch] SLIDER got {args.Type}");

        bool passedToChildren = false;

        ISkiaGestureListener PassToChildren()
        {
            passedToChildren = true;

            return base.ProcessGestures(args, apply);
        }

        ISkiaGestureListener consumed = null;

        //pass Released always to children first
        if (args.Type == TouchActionResult.Up
            || !IsUserPanning || !RespondsToGestures)
        {
            if (args.Event.NumberOfTouches < 2)
                TouchBusy = false;

            consumed = PassToChildren();
            if (consumed != null && args.Type != TouchActionResult.Up)
            {
                return consumed;
            }
        }

        if (!RespondsToGestures)
            return consumed;

        void ResetPan()
        {
            IsUserPanning = false;
            _panningStartOffsetPts = new(args.Event.Location.X, args.Event.Location.Y);
        }

        switch (args.Type)
        {
            case TouchActionResult.Down:

                if (args.Event.NumberOfTouches < 2)
                {
                    ResetPan();
                }

                var thisOffset = TranslateInputCoords(apply.childOffset);

                var x = args.Event.Location.X + thisOffset.X;
                var y = args.Event.Location.Y + thisOffset.Y;

                var relativeX = x - LastDrawnAt.Left; //inside this control
                var relativeY = y - LastDrawnAt.Top; //inside this control

                var locationX = relativeX / RenderingScale; //from pix to pts
                var locationY = relativeY / RenderingScale; //from pix to pts

                if (EnableRange && locationX >= StartThumbX - moreHotspotSize &&
                    locationX <= StartThumbX + SliderHeight + moreHotspotSize)
                {
                    touchArea = RangeZone.Start;
                }
                else
                if (locationX >= EndThumbX - moreHotspotSize &&
                    locationX <= EndThumbX + SliderHeight + moreHotspotSize)
                {
                    touchArea = RangeZone.End;
                }
                else
                {
                    touchArea = RangeZone.Unknown;
                }

                bool onTrail = true;
                if (Trail != null)
                {
                    var trailOffset = Trail.TranslateInputCoords(thisOffset);
                    onTrail = Trail.HitIsInside(args.Event.Location.X + trailOffset.X, args.Event.Location.Y + trailOffset.Y);
                }
                if (onTrail || touchArea == RangeZone.Start || touchArea == RangeZone.End)
                    IsPressed = true;

                if (touchArea == RangeZone.Unknown && ClickOnTrailEnabled)
                {
                    //clicked on trail maybe

                    if (onTrail)
                    {
                        var halfThumb = SliderHeight / 2f;

                        if (EnableRange)
                        {
                            var half = (Width + AvailableWidthAdjustment) / 2.0;

                            if (locationX > half)
                            {
                                MoveEndThumbHere(locationX - halfThumb);
                            }
                            else
                            if (locationX <= half)
                            {
                                MoveStartThumbHere(locationX - halfThumb);
                            }
                        }
                        else
                        {
                            MoveEndThumbHere(locationX - halfThumb);
                        }
                    }

                }

                if (touchArea == RangeZone.Start)
                {
                    consumed = this;
                    lastTouchX = StartThumbX;
                }
                else
                if (touchArea == RangeZone.End)
                {
                    consumed = this;
                    lastTouchX = EndThumbX;
                }

                break;

            case TouchActionResult.Panning when args.Event.NumberOfTouches == 1:

                //filter correct direction so we could scroll below the control in another direction:
                if (!IsUserPanning && IgnoreWrongDirection)
                {
                    //first panning gesture..
                    var panDirection = GetDirectionType(_panningStartOffsetPts, new Vector2(_panningStartOffsetPts.X + args.Event.Distance.Total.X, _panningStartOffsetPts.Y + args.Event.Distance.Total.Y), 0.8f);

                    if (Orientation == OrientationType.Vertical && panDirection != DirectionType.Vertical)
                    {
                        return null;
                    }
                    if (Orientation == OrientationType.Horizontal && panDirection != DirectionType.Horizontal)
                    {
                        return null;
                    }
                }


                IsUserPanning = true;


                //synch this
                if (touchArea == RangeZone.Start)
                    lastTouchX = StartThumbX;
                else
                if (touchArea == RangeZone.End)
                    lastTouchX = EndThumbX;


                if (touchArea != RangeZone.Unknown)
                {
                    TouchBusy = true;

                    if (touchArea == RangeZone.Start)
                    {
                        var maybe = lastTouchX + args.Event.Distance.Delta.X / RenderingScale;//args.TotalDistance.X;
                        SetStartOffsetClamped(maybe);
                    }
                    else
                    if (touchArea == RangeZone.End)
                    {
                        var maybe = lastTouchX + args.Event.Distance.Delta.X / RenderingScale;
                        SetEndOffsetClamped(maybe);

                        //Super.Log($"[Touch] SLIDER zone END {maybe}");
                    }

                    RecalculateValues();
                }

                consumed = this;
                break;

            case TouchActionResult.Up when args.Event.NumberOfTouches < 2:
                IsUserPanning = false;
                IsPressed = false;
                break;

        }

        if (consumed != null || IsUserPanning)// || args.Event.NumberOfTouches > 1)
        {
            return consumed ?? this;
        }

        if (!passedToChildren)
            return PassToChildren();

        return null;
    }


    private bool TouchBusy;

    #endregion

    #region PROPERTIES

    public static readonly BindableProperty ClickOnTrailEnabledProperty = BindableProperty.Create(nameof(ClickOnTrailEnabled), typeof(bool), typeof(SkiaSlider), true);
    public bool ClickOnTrailEnabled
    {
        get { return (bool)GetValue(ClickOnTrailEnabledProperty); }
        set { SetValue(ClickOnTrailEnabledProperty, value); }
    }

    public static readonly BindableProperty EnableRangeProperty = BindableProperty.Create(nameof(EnableRange), typeof(bool), typeof(SkiaSlider), false);
    public bool EnableRange
    {
        get { return (bool)GetValue(EnableRangeProperty); }
        set { SetValue(EnableRangeProperty, value); }
    }

    public static readonly BindableProperty RespondsToGesturesProperty = BindableProperty.Create(
        nameof(RespondsToGestures),
        typeof(bool),
        typeof(SkiaSlider),
        true);

    /// <summary>
    /// Can be open/closed by gestures along with code-behind, default is true
    /// </summary>
    public bool RespondsToGestures
    {
        get { return (bool)GetValue(RespondsToGesturesProperty); }
        set { SetValue(RespondsToGesturesProperty, value); }
    }

    public static readonly BindableProperty SliderHeightProperty = BindableProperty.Create(nameof(SliderHeight), typeof(double), typeof(SkiaSlider), 22.0); //, BindingMode.TwoWay
    public double SliderHeight
    {
        get { return (double)GetValue(SliderHeightProperty); }
        set { SetValue(SliderHeightProperty, value); }
    }

    public static readonly BindableProperty MinProperty = BindableProperty.Create(nameof(Min), typeof(double), typeof(SkiaSlider), 0.0); //, BindingMode.TwoWay
    public double Min
    {
        get { return (double)GetValue(MinProperty); }
        set { SetValue(MinProperty, value); }
    }

    public static readonly BindableProperty MaxProperty = BindableProperty.Create(nameof(Max), typeof(double), typeof(SkiaSlider), 100.0);
    public double Max
    {
        get { return (double)GetValue(MaxProperty); }
        set { SetValue(MaxProperty, value); }
    }

    public static readonly BindableProperty StartProperty =
        BindableProperty.Create(nameof(Start), typeof(double), typeof(SkiaSlider), 0.0, BindingMode.TwoWay,
            propertyChanged: OnStartPropertyChanged,
            coerceValue: CoerceValue);

    /// <summary>
    /// Enabled for ranged
    /// </summary>
    public double Start
    {
        get { return (double)GetValue(StartProperty); }
        set { SetValue(StartProperty, value); }
    }

    private static void OnStartPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaSlider slider && newValue is double newStartValue)
        {
            slider.OnStartChanged();
        }
    }

    private static object CoerceValue(BindableObject bindable, object value)
    {
        var newValue = (double)value;

        if (bindable is SkiaSlider slider)
        {
            var adjusted = slider.AdjustToStepValue(newValue, slider.Min, slider.Step);
            if (slider.Width >= 0)
            {
                return Math.Clamp(adjusted, slider.Min, slider.Max);
            }
            return adjusted;
        }

        return value;
    }


    public static readonly BindableProperty EndProperty = BindableProperty.Create(
        nameof(End),
        typeof(double),
        typeof(SkiaSlider),
        100.0,
        BindingMode.TwoWay,
        propertyChanged: OnEndPropertyChanged,
        coerceValue: CoerceValue);

    /// <summary>
    /// For non-ranged this is your main value
    /// </summary>
    public double End
    {
        get { return (double)GetValue(EndProperty); }
        set { SetValue(EndProperty, value); }
    }
    private static void OnEndPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkiaSlider slider && newValue is double newEndValue)
        {
            slider.OnEndChanged();
        }
    }

    public static readonly BindableProperty StartThumbXProperty = BindableProperty.Create(nameof(StartThumbX), typeof(double), typeof(SkiaSlider), 0.0);
    public double StartThumbX
    {
        get { return (double)GetValue(StartThumbXProperty); }
        set { SetValue(StartThumbXProperty, value); }
    }
    public static readonly BindableProperty EndThumbXProperty = BindableProperty.Create(nameof(EndThumbX), typeof(double), typeof(SkiaSlider), 0.0);
    public double EndThumbX
    {
        get { return (double)GetValue(EndThumbXProperty); }
        set { SetValue(EndThumbXProperty, value); }
    }

    public static readonly BindableProperty StepProperty = BindableProperty.Create(nameof(Step), typeof(double), typeof(SkiaSlider), 1.0); //, BindingMode.TwoWay
    public double Step
    {
        get { return (double)GetValue(StepProperty); }
        set { SetValue(StepProperty, value); }
    }

    public static readonly BindableProperty RangeMinProperty = BindableProperty.Create(nameof(RangeMin), typeof(double), typeof(SkiaSlider), 0.0); //, BindingMode.TwoWay
    public double RangeMin
    {
        get { return (double)GetValue(RangeMinProperty); }
        set { SetValue(RangeMinProperty, value); }
    }

    public static readonly BindableProperty ValueStringFormatProperty = BindableProperty.Create(nameof(ValueStringFormat), typeof(string), typeof(SkiaSlider), "### ### ##0.##"); //, BindingMode.TwoWay

    public string ValueStringFormat
    {
        get { return (string)GetValue(ValueStringFormatProperty); }
        set { SetValue(ValueStringFormatProperty, value); }
    }

    public static readonly BindableProperty MinMaxStringFormatProperty = BindableProperty.Create(nameof(MinMaxStringFormat), typeof(string), typeof(SkiaSlider), "### ### ##0.##"); //, BindingMode.TwoWay

    public string MinMaxStringFormat
    {
        get { return (string)GetValue(MinMaxStringFormatProperty); }
        set { SetValue(MinMaxStringFormatProperty, value); }
    }

    public static readonly BindableProperty AvailableWidthAdjustmentProperty = BindableProperty.Create(nameof(AvailableWidthAdjustment), typeof(double), typeof(SkiaSlider), 0.0);
    public double AvailableWidthAdjustment
    {
        get { return (double)GetValue(AvailableWidthAdjustmentProperty); }
        set { SetValue(AvailableWidthAdjustmentProperty, value); }
    }

    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation), typeof(OrientationType), typeof(SkiaSlider),
        OrientationType.Horizontal,
        propertyChanged: NeedDraw);
    /// <summary>
    /// <summary>Gets or sets the orientation. This is a bindable property.</summary>
    /// </summary>
    public OrientationType Orientation
    {
        get { return (OrientationType)GetValue(OrientationProperty); }
        set { SetValue(OrientationProperty, value); }
    }

    public static readonly BindableProperty IgnoreWrongDirectionProperty = BindableProperty.Create(
        nameof(IgnoreWrongDirection),
        typeof(bool),
        typeof(SkiaSlider),
        true);

    public bool Invert
    {
        get { return (bool)GetValue(InvertProperty); }
        set { SetValue(InvertProperty, value); }
    }

    public static readonly BindableProperty InvertProperty = BindableProperty.Create(
        nameof(Invert),
        typeof(bool),
        typeof(SkiaSlider),
        false);

    /// <summary>
    /// Will ignore gestures of the wrong direction, like if this Orientation is Horizontal will ignore gestures with vertical direction velocity
    /// </summary>
    public bool IgnoreWrongDirection
    {
        get { return (bool)GetValue(IgnoreWrongDirectionProperty); }
        set { SetValue(IgnoreWrongDirectionProperty, value); }
    }

    #endregion

    #region ENGINE



    protected override void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName.IsEither(nameof(Min),
                nameof(MinMaxStringFormat), nameof(Max)))
        {
            var mask = "{0:" + MinMaxStringFormat + "}";
            MinDesc = string.Format(mask, Min).Trim();
            MaxDesc = string.Format(mask, Max).Trim();
        }

        if (lockInternal)
            return;

        if (propertyName.IsEither(nameof(Width),
            nameof(Min), nameof(Max), nameof(StartThumbX),
            nameof(EndThumbX), nameof(Step), nameof(Start),
            nameof(End), nameof(AvailableWidthAdjustment)))
        {

            if (Width > -1)
            {
                if (EnableRange)
                {
                    StepValue = (Max - Min) / (Width + AvailableWidthAdjustment - SliderHeight);
                }
                else
                {
                    StepValue = (Max - Min) / (Width + AvailableWidthAdjustment * 2 - SliderHeight);
                }

                //if (!TouchBusy)
                //{
                //    var mask = "{0:" + ValueStringFormat + "}";
                //    if (EnableRange)
                //    {
                //        SetStartOffsetClamped((Start - Min) / StepValue - AvailableWidthAdjustment);
                //        StartDesc = string.Format(mask, Start).Trim();
                //    }
                //    SetEndOffsetClamped((End - Min) / StepValue);
                //    EndDesc = string.Format(mask, End).Trim();
                //}
                if (!TouchBusy)
                {
                    var mask = "{0:" + ValueStringFormat + "}";
                    if (EnableRange)
                    {
                        StartThumbX = PositionFromValue(Start) + AvailableWidthAdjustment;
                        StartDesc = string.Format(mask, Start).Trim();
                    }
                    EndThumbX = PositionFromValue(End) - AvailableWidthAdjustment;
                    EndDesc = string.Format(mask, End).Trim();
                }

            }

        }
    }

    public virtual void OnEndChanged()
    {
        EndChanged?.Invoke(this, End);
    }

    public virtual void OnStartChanged()
    {
        StartChanged?.Invoke(this, Start);
    }

    public event EventHandler<double> StartChanged;
    public event EventHandler<double> EndChanged;

    protected double AdjustToStepValue(double value, double minValue, double stepValue)
    {
        if (stepValue <= 0) return value;

        double relativeValue = value - minValue;
        double adjustedStep = Math.Round(relativeValue / stepValue) * stepValue;
        return adjustedStep + minValue;
    }




    protected virtual void RecalculateValues()
    {
        lockInternal = true;

        var mask = "{0:" + ValueStringFormat + "}";
        ConvertOffsetsToValues();
        if (EnableRange)
        {
            StartDesc = string.Format(mask, Start).Trim();
        }
        EndDesc = string.Format(mask, End).Trim();

        lockInternal = false;
    }

    protected void MoveEndThumbHere(double x)
    {
        if (!ClickOnTrailEnabled)
            return;

        touchArea = RangeZone.End;

        lockInternal = true;
        SetEndOffsetClamped(x);
        lockInternal = false;

        RecalculateValues();
    }

    protected void MoveStartThumbHere(double x)
    {
        if (!ClickOnTrailEnabled)
            return;

        touchArea = RangeZone.Start;

        lockInternal = true;

        SetStartOffsetClamped(x);

        lockInternal = false;

        RecalculateValues();
    }


    private volatile bool lockInternal;

    private string _StartDesc;
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string StartDesc
    {
        get { return _StartDesc; }
        set
        {
            if (_StartDesc != value)
            {
                _StartDesc = value;
                OnPropertyChanged();
            }
        }
    }

    private string _EndDesc;
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string EndDesc
    {
        get { return _EndDesc; }
        set
        {
            if (_EndDesc != value)
            {
                _EndDesc = value;
                OnPropertyChanged();
            }
        }
    }

    private string _MinDesc;
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MinDesc
    {
        get { return _MinDesc; }
        set
        {
            if (_MinDesc != value)
            {
                _MinDesc = value;
                OnPropertyChanged();
            }
        }
    }

    private string _MaxDesc;
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MaxDesc
    {
        get { return _MaxDesc; }
        set
        {
            if (_MaxDesc != value)
            {
                _MaxDesc = value;
                OnPropertyChanged();
            }
        }
    }

    private double _StepValue;
    [EditorBrowsable(EditorBrowsableState.Never)]
    public double StepValue
    {
        get
        {
            return _StepValue;
        }
        set
        {
            if (_StepValue != value)
            {
                _StepValue = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _IsPressed;
    public bool IsPressed
    {
        get
        {
            return _IsPressed;
        }
        set
        {
            if (_IsPressed != value)
            {
                _IsPressed = value;
                OnPropertyChanged();
            }
        }
    }


    #endregion

    //protected virtual void ConvertOffsetsToValues()
    //{
    //    if (EnableRange)
    //    {
    //        Start = StepValue * (this.StartThumbX + AvailableWidthAdjustment) + Min;
    //        End = StepValue * (this.EndThumbX - AvailableWidthAdjustment) + Min;
    //    }
    //    else
    //    {
    //        End = StepValue * (this.EndThumbX + AvailableWidthAdjustment) + Min;
    //    }
    //}

    //void SetStartOffsetClamped(double maybe)
    //{
    //    if (maybe < -AvailableWidthAdjustment)
    //    {
    //        StartThumbX = -AvailableWidthAdjustment;
    //    }
    //    else
    //    if (EnableRange && maybe > (EndThumbX - RangeMin / StepValue))
    //    {
    //        StartThumbX = EndThumbX - RangeMin / StepValue;
    //    }
    //    else
    //    {
    //        StartThumbX = maybe;
    //    }
    //}

    //void SetEndOffsetClamped(double maybe)
    //{
    //    if (maybe < -AvailableWidthAdjustment)
    //    {
    //        EndThumbX = -AvailableWidthAdjustment;
    //    }
    //    else
    //    if (maybe > (Width + AvailableWidthAdjustment) - SliderHeight)
    //    {
    //        EndThumbX = (Width + AvailableWidthAdjustment) - SliderHeight;
    //    }
    //    else
    //    if (EnableRange && maybe < (StartThumbX + RangeMin / StepValue))
    //    {
    //        EndThumbX = StartThumbX + RangeMin / StepValue;
    //    }
    //    else
    //    {
    //        EndThumbX = maybe;
    //    }
    //}

    void SetStartOffsetClamped(double maybe)
    {
        double minPosition = GetStartThumbMinPosition();
        double maxPosition = GetStartThumbMaxPosition();

        if (maybe < minPosition)
        {
            StartThumbX = minPosition;
        }
        else if (maybe > maxPosition)
        {
            StartThumbX = maxPosition;
        }
        else
        {
            StartThumbX = maybe;
        }
    }

    void SetEndOffsetClamped(double maybe)
    {
        double minPosition = GetEndThumbMinPosition();
        double maxPosition = GetEndThumbMaxPosition();

        if (maybe < minPosition)
        {
            EndThumbX = minPosition;
        }
        else if (maybe > maxPosition)
        {
            EndThumbX = maxPosition;
        }
        else
        {
            EndThumbX = maybe;
        }
    }


    protected virtual void ConvertOffsetsToValues()
    {
        if (EnableRange)
        {
            Start = AdjustToStepValue(ValueFromPosition(this.StartThumbX + AvailableWidthAdjustment), Min, Step);
            End = AdjustToStepValue(ValueFromPosition(this.EndThumbX - AvailableWidthAdjustment), Min, Step);
        }
        else
        {
            End = AdjustToStepValue(ValueFromPosition(this.EndThumbX + AvailableWidthAdjustment), Min, Step);
        }
    }


    #region Invert

    private double ValueFromPosition(double position)
    {
        double totalLength = Width + AvailableWidthAdjustment - SliderHeight;
        if (totalLength <= 0) return Min; // Avoid division by zero

        double ratio = position / totalLength;
        if (Invert)
        {
            return Max - ratio * (Max - Min);
        }
        else
        {
            return Min + ratio * (Max - Min);
        }
    }

    private double PositionFromValue(double value)
    {
        double totalLength = Width + AvailableWidthAdjustment - SliderHeight;
        if (totalLength <= 0) return 0; // Avoid division by zero

        double ratio;
        if (Invert)
        {
            ratio = (Max - value) / (Max - Min);
        }
        else
        {
            ratio = (value - Min) / (Max - Min);
        }
        return ratio * totalLength;
    }

    private double GetStartThumbMinPosition()
    {
        if (Invert)
        {
            if (EnableRange)
            {
                return EndThumbX + RangeMin / StepValue;
            }
            else
            {
                return -AvailableWidthAdjustment;
            }
        }
        else
        {
            return -AvailableWidthAdjustment;
        }
    }

    private double GetStartThumbMaxPosition()
    {
        if (Invert)
        {
            return (Width + AvailableWidthAdjustment) - SliderHeight;
        }
        else
        {
            if (EnableRange)
            {
                return EndThumbX - RangeMin / StepValue;
            }
            else
            {
                return (Width + AvailableWidthAdjustment) - SliderHeight;
            }
        }
    }

    private double GetEndThumbMinPosition()
    {
        if (Invert)
        {
            return -AvailableWidthAdjustment;
        }
        else
        {
            if (EnableRange)
            {
                return StartThumbX + RangeMin / StepValue;
            }
            else
            {
                return -AvailableWidthAdjustment;
            }
        }
    }

    private double GetEndThumbMaxPosition()
    {
        if (Invert)
        {
            if (EnableRange)
            {
                return StartThumbX - RangeMin / StepValue;
            }
            else
            {
                return (Width + AvailableWidthAdjustment) - SliderHeight;
            }
        }
        else
        {
            return (Width + AvailableWidthAdjustment) - SliderHeight;
        }
    }


    #endregion

}
