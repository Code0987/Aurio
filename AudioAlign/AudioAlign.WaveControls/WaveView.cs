﻿using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Globalization;
using System.Diagnostics;
using AudioAlign.Audio;

namespace AudioAlign.WaveControls {
    public partial class WaveView : VirtualViewBase {

        private VisualizingAudioStream16 audioStream;

        // variables used for mouse dragging
        private bool dragging = false;
        private Point previousMousePosition;

        public WaveView() {
            // event gets triggered when ActualWidth or ActualHeight change
            SizeChanged += WaveView_SizeChanged;
        }

        public bool Antialiased {
            get { return ((EdgeMode)GetValue(RenderOptions.EdgeModeProperty)) != EdgeMode.Aliased; }
            set { SetValue(RenderOptions.EdgeModeProperty, !value ? EdgeMode.Aliased : EdgeMode.Unspecified); }
        }

        protected override void OnRender(DrawingContext drawingContext) {
            base.OnRender(drawingContext);
            bool debug = DebugOutput;

            if (audioStream != null) {
                Interval audioInterval = new Interval(TrackOffset, TrackOffset + audioStream.TimeLength.Ticks);
                Interval viewportInterval = VirtualViewportInterval;

                if (!audioInterval.Intersects(viewportInterval)) {
                    Debug.WriteLine("nothing to draw!");
                    return;
                }

                double sampleLength = AudioUtil.CalculateSampleTicks(audioStream.Properties);
                Interval visibleAudioInterval = audioInterval.Intersect(viewportInterval);
                Interval audioToLoadInterval = visibleAudioInterval - TrackOffset;

                if (visibleAudioInterval.Length < sampleLength) {
                    drawingContext.DrawText(DebugText("VISIBLE INTERVAL WARNING: " + visibleAudioInterval.Length + " < SAMPLE LENGTH " + sampleLength), new Point(0, 0));
                    return;
                }

                // align interval to samples
                Interval audioToLoadIntervalAligned = AudioUtil.AlignToSamples(audioToLoadInterval, audioStream.Properties);
                int samplesToLoad = AudioUtil.CalculateSamples(audioStream.Properties, new TimeSpan(audioToLoadIntervalAligned.Length));

                // calculate drawing measures
                double viewportToDrawingScaleFactor = ActualWidth / VirtualViewportWidth;
                double drawingOffset = ((audioToLoadIntervalAligned.From - audioToLoadInterval.From) + (visibleAudioInterval.From - viewportInterval.From)) * viewportToDrawingScaleFactor;
                double drawingOffset2 = (visibleAudioInterval.From - viewportInterval.From) * viewportToDrawingScaleFactor;
                double drawingWidth = (samplesToLoad - 1) * sampleLength * viewportToDrawingScaleFactor;

                DateTime beforeLoading = DateTime.Now;

                // load audio samples
                audioStream.TimePosition = new TimeSpan(audioToLoadIntervalAligned.From);
                bool peaks;
                Interval readInterval;
                List<Point>[] samples = audioStream.Read(audioToLoadIntervalAligned, samplesToLoad > drawingWidth ? (int)drawingWidth : samplesToLoad, out readInterval, out peaks);
                int samplesLoaded = peaks ? samples[0].Count / 2 : samples[0].Count;

                //Debug.WriteLine("drawing width: {0} offset: {1}, samples requested: {2}, samples loaded: {3}", drawingWidth, drawingOffset, samplesToLoad, samplesLoaded);

                DateTime afterLoading = DateTime.Now;

                if (samplesLoaded <= 1) {
                    drawingContext.DrawText(DebugText("SAMPLE WARNING: " + samplesLoaded), new Point(0, 0));
                    return;
                }

                DateTime beforeDrawing = DateTime.Now;

                // draw background
                drawingContext.DrawRectangle(WaveformBackground, null, new Rect(drawingOffset, 0, drawingWidth, ActualHeight));
                if (debug) {
                    drawingContext.DrawRectangle(null, new Pen(Brushes.Brown, 4), new Rect(drawingOffset, 0, drawingWidth, ActualHeight));
                }

                GuidelineSet guidelineSet = new GuidelineSet();
                drawingContext.PushGuidelineSet(guidelineSet);

                // draw waveform guides & create drawing guidelines
                int channels = audioStream.Properties.Channels;
                double channelHeight = ActualHeight / channels;
                double channelHalfHeight = channelHeight / 2;
                for (int channel = 0; channel < channels; channel++) {
                    // waveform zero-line
                    guidelineSet.GuidelinesY.Add((channelHeight * channel + channelHalfHeight) + 0.5);
                    drawingContext.DrawLine(new Pen(Brushes.LightGray, 1),
                        new Point(drawingOffset, channelHeight * channel + channelHalfHeight),
                        new Point(drawingOffset + drawingWidth, channelHeight * channel + channelHalfHeight));
                    // waveform spacers
                    if (channel > 0) {
                        guidelineSet.GuidelinesY.Add((channelHeight * channel) + 0.5);
                        drawingContext.DrawLine(new Pen(Brushes.DarkGray, 1),
                            new Point(drawingOffset, channelHeight * channel),
                            new Point(drawingOffset + drawingWidth, channelHeight * channel));
                    }
                }

                // draw waveforms
                if (channelHeight >= 1) {
                    IWaveformRenderer renderer = null;
                    switch (RenderMode) {
                        case WaveViewRenderMode.Bitmap:
                            renderer = new WaveformBitmapRenderer();
                            break;
                        case WaveViewRenderMode.Geometry:
                            renderer = new WaveformGeometryRenderer();
                            break;
                    }
                    for (int channel = 0; channel < channels; channel++) {
                        Drawing waveform = renderer.Render(samples[channel], peaks, (int)Math.Round(drawingWidth), (int)channelHeight);
                        DrawingGroup drawing = new DrawingGroup();
                        drawing.Children.Add(waveform);
                        drawing.Transform = new TranslateTransform((int)drawingOffset, (int)(channelHeight * channel));
                        drawingContext.DrawDrawing(drawing);
                    }
                }

                DateTime afterDrawing = DateTime.Now;

                drawingContext.Pop();

                // draw track name
                if (DrawTrackName) {
                    FormattedText formattedTrackName = new FormattedText(AudioTrack.FileInfo.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10f, Brushes.White);
                    drawingContext.DrawRectangle(Brushes.Black, null, new Rect(4 + drawingOffset2, 5, formattedTrackName.Width + 4, formattedTrackName.Height + 2));
                    drawingContext.DrawText(formattedTrackName, new Point(6 + drawingOffset2, 6));
                }

                if (debug) {
                    // DEBUG OUTPUT
                    drawingContext.DrawText(DebugText(String.Format("source:" + audioStream.Stats.ToString() + " load:{0}ms render:{1}ms", (afterLoading - beforeLoading).TotalMilliseconds, (afterDrawing - beforeDrawing).TotalMilliseconds)),
                        new Point(0, 20));
                    drawingContext.DrawText(DebugText("visibleAudioInterval: " + visibleAudioInterval + ", audioToLoadInterval: " + audioToLoadInterval + ", audioToLoadIntervalAligned: " + audioToLoadIntervalAligned),
                        new Point(0, ActualHeight) + new Vector(0, -50));
                    drawingContext.DrawText(DebugText("Drawing Offset: " + drawingOffset + ", Width: " + drawingWidth + ", ScalingFactor: " + viewportToDrawingScaleFactor + ", Samples: " + samplesLoaded + ", Peakratio 1:" + Math.Round(VirtualViewportWidth / sampleLength / ActualWidth, 2)),
                        new Point(0, ActualHeight) + new Vector(0, -40));
                }
            }

            if (debug) {
                // DEBUG OUTPUT
                drawingContext.DrawText(DebugText("ActualWidth: " + ActualWidth + ", ActualHeight: " + ActualHeight),
                    new Point(0, ActualHeight) + new Vector(0, -30));
                drawingContext.DrawText(DebugText("TrackLength: " + TrackLength + ", TrackOffset: " + TrackOffset),
                    new Point(0, ActualHeight) + new Vector(0, -20));
                drawingContext.DrawText(DebugText("ViewportOffset: " + VirtualViewportOffset + ", ViewportWidth: " + VirtualViewportWidth),
                    new Point(0, ActualHeight) + new Vector(0, -10));
            }
        }

        private FormattedText DebugText(string text) {
            return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, 
                new Typeface("Tahoma"), 8, Brushes.Black);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
            base.OnMouseDown(e);
            Point mouseDownPosition = Mouse.GetPosition(this);
            //Debug.WriteLine("WaveView OnMouseDown @ " + mouseDownPosition);
            dragging = true;
            previousMousePosition = mouseDownPosition;
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);
            if (dragging) {
                if (e.LeftButton == MouseButtonState.Pressed) {
                    Point mouseMovePosition = Mouse.GetPosition(this);
                    Debug.WriteLine("WaveView OnMouseMove @ " + mouseMovePosition);

                    double physicalDelta = mouseMovePosition.X - previousMousePosition.X;
                    //long virtualDelta = PhysicalToVirtualOffset((long)physicalDelta);

                    previousMousePosition = mouseMovePosition;
                    //Debug.WriteLine("WaveView DragDelta: " + physicalDelta + "p / " + virtualDelta + "v");

                    TrackOffset += (long)(VirtualViewportWidth / ActualWidth * physicalDelta);
                    e.Handled = true;
                }
                else {
                    dragging = false;
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            base.OnMouseUp(e);
            Point mouseUpPosition = Mouse.GetPosition(this);
            //Debug.WriteLine("WaveView OnMouseUp @ " + mouseUpPosition);
            dragging = false;
            e.Handled = true;
        }

        protected override void OnMouseLeave(MouseEventArgs e) {
            base.OnMouseLeave(e);
            //Debug.WriteLine("WaveView OnMouseLeave");
            dragging = false;
            e.Handled = true;
        }
    }
}
