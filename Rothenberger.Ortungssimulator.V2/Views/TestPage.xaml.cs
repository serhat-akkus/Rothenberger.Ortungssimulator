using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Rothenberger.Ortungssimulator.V2.ViewModels;

namespace Rothenberger.Ortungssimulator.V2.Views
{
    public partial class TestPage : ContentPage
    {
        private readonly TestPageViewModel _viewModel;

        public TestPage()
        {
            InitializeComponent();
            _viewModel = new TestPageViewModel();
            BindingContext = _viewModel;
            CanvasView.StartInteraction += CanvasView_StartInteraction;
            CanvasView.MoveHoverInteraction += CanvasView_MoveHoverInteraction;
            CanvasView.Drawable = new TestPageDrawable(this);
            ArrowView.Drawable = new ArrowBoxDrawable(_viewModel);
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.CurrentMousePosition = new Point(500, 500); // Canvas-Mitte
        }

        private void Canvas_PointerMoved(object sender, PointerEventArgs e)
        {
            if (BindingContext is TestPageViewModel vm)
            {
                var position = e.GetPosition((Element?)sender);
                vm.UpdateCurrentMousePosition(new Point(position.Value.X, position.Value.Y));
            }
        }

        private void CanvasView_StartInteraction(object sender, TouchEventArgs e)
        {
            var point = e.Touches.First();
            _viewModel.OnCanvasClicked(point);

            CanvasView.Invalidate();
            ArrowView.Invalidate();
        }

        private void CanvasView_MoveHoverInteraction(object sender, TouchEventArgs e)
        {
            var point = e.Touches.First();
            _viewModel.CurrentMousePosition = point;
            _viewModel.UpdateArrowToProbe();
            ArrowView.Invalidate();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TestPageViewModel.IsProbeVisible) ||
                e.PropertyName == nameof(TestPageViewModel.IsClickPointVisible))
            {
                CanvasView.Invalidate();
            }
            if (e.PropertyName == nameof(TestPageViewModel.IsArrowBoxVisible) ||
                e.PropertyName == nameof(TestPageViewModel.ArrowRotation))
            {
                ArrowView.Invalidate();
            }
        }

        private class TestPageDrawable : IDrawable
        {
            private readonly TestPage _page;
            public TestPageDrawable(TestPage page) { _page = page; }
            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                // Hintergrund
                canvas.FillColor = Colors.LightGray;
                canvas.FillRectangle(dirtyRect);

                // Schwarzer Rand
                canvas.StrokeColor = Colors.Black;
                canvas.StrokeSize = 2;
                canvas.DrawRectangle(dirtyRect);

                var vm = _page._viewModel;
                if (vm == null) return;
                if (vm.IsProbeVisible)
                {
                    var p = vm.ProbePosition;
                    canvas.StrokeColor = Colors.Red;
                    canvas.StrokeSize = 4;
                    canvas.DrawLine((float)p.X - 10, (float)p.Y - 10, (float)p.X + 10, (float)p.Y + 10);
                    canvas.DrawLine((float)p.X + 10, (float)p.Y - 10, (float)p.X - 10, (float)p.Y + 10);
                }
                if (vm.IsClickPointVisible)
                {
                    var p = vm.ClickPointPosition;
                    canvas.FillColor = Colors.Blue;
                    canvas.FillCircle((float)p.X, (float)p.Y, 5);
                }
            }
        }

        private class ArrowBoxDrawable : IDrawable
        {
            private readonly TestPageViewModel _vm;
            public ArrowBoxDrawable(TestPageViewModel vm) { _vm = vm; }
            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                if (!_vm.IsArrowBoxVisible) return;
                canvas.SaveState();

                // Mittelpunkt der Box
                float centerX = dirtyRect.Width / 2;
                float centerY = dirtyRect.Height / 2;
                canvas.Translate(centerX, centerY);
                canvas.Rotate((float)_vm.ArrowRotation);

                // Schaft
                float shaftLength = 28;
                float arrowHeadLength = 12;
                float arrowHeadWidth = 16;

                // Linie endet direkt vor dem Dreieck
                canvas.StrokeColor = Colors.Black;
                canvas.StrokeSize = 6;
                canvas.DrawLine(-shaftLength / 2, 0, shaftLength / 2, 0);

                // Dreieck: Spitze auf der Linie, Basis hinter dem Linienende
                canvas.FillColor = Colors.Black;
                var arrowHead = new PathF();
                arrowHead.MoveTo(shaftLength / 2 + arrowHeadLength, 0); // Spitze des Dreiecks (vorne)
                arrowHead.LineTo(shaftLength / 2, -arrowHeadWidth / 2); // links hinten
                arrowHead.LineTo(shaftLength / 2, arrowHeadWidth / 2);  // rechts hinten
                arrowHead.Close();
                canvas.FillPath(arrowHead);

                canvas.RestoreState();
            }
        }
    }
}