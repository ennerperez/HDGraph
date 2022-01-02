﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using HDGraph.Interfaces.ScanEngines;
using System.Diagnostics;
using HDGraph.Interfaces.DrawEngines;
using System.ComponentModel;

namespace HDGraph.WpfDrawEngine
{
    /// <summary>
    /// Interaction logic for Arc.xaml
    /// </summary>
    public partial class Arc : UserControl
    {
        static Arc()
        {
            FontSizeProperty.OverrideMetadata(typeof(Arc), new FrameworkPropertyMetadata(new PropertyChangedCallback(OnFontSizePropertyChanged)));
        }

        public bool HideTooSmallText { get; set; }

        public Arc()
        {
            InitializeComponent();
            this.ToolTipOpening += new ToolTipEventHandler(Arc_ToolTipOpening);
            this.ToolTip = "TODO : Tooltip here."; // TODO.
        }

        #region Path Style


        public Style PathStyle
        {
            get { return (Style)GetValue(PathStyleProperty); }
            set { SetValue(PathStyleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PathStyle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PathStyleProperty =
            DependencyProperty.Register("PathStyle", typeof(Style), typeof(Arc), new UIPropertyMetadata(null));



        #endregion

        private const int LimitVisibleSize = 15;

        public static void OnFontSizePropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs arg)
        {

            Arc arc = (Arc)obj;
            if (!arc.HideTooSmallText)
                return;
            TextBlock textBlock = arc.TextBlockArcName;
            if (!textBlock.IsLoaded)
                return;
            Point upperLeft = textBlock.PointToScreen(new Point(0, 0));
            Point downRight = textBlock.PointToScreen(new Point(textBlock.ActualWidth, textBlock.ActualHeight));
            double width = downRight.X - upperLeft.X;
            double height = downRight.Y - upperLeft.Y;
            if (height < LimitVisibleSize || width < LimitVisibleSize)
                textBlock.Visibility = Visibility.Hidden;
            else
                textBlock.Visibility = Visibility.Visible;
        }

        void Arc_ToolTipOpening(object sender, ToolTipEventArgs e)
        {

            //this.ToolTip = ((Node == null) ? String.Empty : Node.Path) +
            //                "; StartAngle:" + StartAngle + "; StopAngle:" + StopAngle;
        }



        public DrawOptions DrawOptions
        {
            get { return (DrawOptions)GetValue(DrawOptionsProperty); }
            set { SetValue(DrawOptionsProperty, value); }
        }

        // Using a DependencyProperty as the backing store for DrawOption.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty DrawOptionsProperty =
            DependencyProperty.Register("DrawOptions", typeof(DrawOptions), typeof(Arc), new UIPropertyMetadata(null));



        #region Dependency Properties


        public float StartAngle
        {
            get { return (float)GetValue(StartAngleProperty); }
            set { SetValue(StartAngleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StartAngle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StartAngleProperty =
            DependencyProperty.Register("StartAngle", typeof(float), typeof(Arc), new UIPropertyMetadata(0f, new PropertyChangedCallback(OnDesignPropertyChanged)));



        private float MiddleAngle
        {
            get { return (float)GetValue(MiddleAngleProperty); }
            set { SetValue(MiddleAngleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for MiddleAngle.  This enables animation, styling, binding, etc...
        private static readonly DependencyProperty MiddleAngleProperty =
            DependencyProperty.Register("MiddleAngle", typeof(float), typeof(Arc), new UIPropertyMetadata(0f));


        public float StopAngle
        {
            get { return (float)GetValue(StopAngleProperty); }
            set { SetValue(StopAngleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StopAngle.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StopAngleProperty =
            DependencyProperty.Register("StopAngle", typeof(float), typeof(Arc), new UIPropertyMetadata(45f, new PropertyChangedCallback(OnDesignPropertyChanged)));



        public float LargeRadius
        {
            get { return (float)GetValue(LargeRadiusProperty); }
            set { SetValue(LargeRadiusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LargeRadius.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LargeRadiusProperty =
            DependencyProperty.Register("LargeRadius", typeof(float), typeof(Arc), new UIPropertyMetadata(200f, new PropertyChangedCallback(OnDesignPropertyChanged)));


        public float SmallRadius
        {
            get { return (float)GetValue(SmallRadiusProperty); }
            set { SetValue(SmallRadiusProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SmallRadius.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SmallRadiusProperty =
            DependencyProperty.Register("SmallRadius", typeof(float), typeof(Arc), new UIPropertyMetadata(50f, new PropertyChangedCallback(OnDesignPropertyChanged)));


        public IDirectoryNode Node
        {
            get { return (IDirectoryNode)GetValue(NodeProperty); }
            set { SetValue(NodeProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Node.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty NodeProperty =
            DependencyProperty.Register("Node", typeof(IDirectoryNode), typeof(Arc), new UIPropertyMetadata(null, new PropertyChangedCallback(OnNodePropertyChanged)));



        public float TextRotation
        {
            get { return (float)GetValue(TextRotationProperty); }
            set { SetValue(TextRotationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for TextRotation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty TextRotationProperty =
            DependencyProperty.Register("TextRotation", typeof(float), typeof(Arc), new UIPropertyMetadata(0f));



        #endregion

        public static void OnNodePropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs arg)
        {
            //Arc arc = obj as Arc;
            //if (arc == null)
            //    return;
            ////if (arc.Node.DirectoryType == SpecialDirTypes.UnknownPart)
            //// TODO
        }

        private bool editing;

        public void BeginEdit()
        {
            editing = true;
        }

        public void EndEdit()
        {
            if (editing)
            {
                editing = false;
                UpdateDesign();
            }
        }

        private void UpdateDesign()
        {
            MiddleAngle = StartAngle + StopAngle / 2;
            float newRadius = this.LargeRadius;

            // Create a StreamGeometry to use to specify myPath.
            StreamGeometry geometry = new StreamGeometry();
            geometry.FillRule = FillRule.EvenOdd;

            double stopAngleInRadian = WpfUtils.GetRadianFromDegree(this.StopAngle);
            double stopAngleCos = Math.Cos(stopAngleInRadian);
            double stopAngleSin = Math.Sin(stopAngleInRadian);
            double xLarge = stopAngleCos * this.LargeRadius;
            double yLarge = stopAngleSin * this.LargeRadius;
            double xSmall = stopAngleCos * this.SmallRadius;
            double ySmall = stopAngleSin * this.SmallRadius;

            if (StopAngle == 360)
            {
                // Open a StreamGeometryContext that can be used to describe this StreamGeometry 
                // object's contents.
                using (StreamGeometryContext ctx = geometry.Open())
                {

                    ctx.BeginFigure(new Point(newRadius, 0), true, true);
                    ctx.ArcTo(new Point(-newRadius, 0),
                              new Size(newRadius, newRadius),
                              180,
                              false,
                              SweepDirection.Clockwise,
                              true,
                              false);
                    ctx.ArcTo(new Point(newRadius, 0),
                              new Size(newRadius, newRadius),
                              180,
                              false,
                              SweepDirection.Clockwise,
                              true,
                              false);
                }
                radiusLabelTranslation.X = 0;
            }
            else
            {
                // Open a StreamGeometryContext that can be used to describe this StreamGeometry 
                // object's contents.
                using (StreamGeometryContext ctx = geometry.Open())
                {
                    ctx.BeginFigure(new Point(newRadius, 0), true, true);
                    ctx.ArcTo(new Point(xLarge, yLarge),
                              new Size(newRadius, newRadius),
                              StopAngle,
                              (this.StopAngle > 180),
                              SweepDirection.Clockwise,
                              true,
                              false);
                    ctx.LineTo(new Point(xSmall, ySmall), true, false);
                    ctx.ArcTo(new Point(this.SmallRadius, 0),
                              new Size(this.SmallRadius, this.SmallRadius),
                              StopAngle,
                              (this.StopAngle > 180),
                              SweepDirection.Counterclockwise,
                              true,
                              false);
                }
            }
            // Freeze the geometry (make it unmodifiable)
            // for additional performance benefits.
            geometry.Freeze();

            // Specify the shape (triangle) of the Path using the StreamGeometry.
            path1.Data = geometry;
        }

        public static void OnDesignPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs arg)
        {
            Arc arc = obj as Arc;
            if (arc == null)
                return;
            if (!arc.editing)
                // Update design now only if not in edit mode
                // (Update will be called at the end of the "edit mode" if it is enabled).
                arc.UpdateDesign();
        }
    }
}
