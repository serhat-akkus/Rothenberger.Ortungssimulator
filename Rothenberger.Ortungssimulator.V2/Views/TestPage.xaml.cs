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
            CanvasView.Drawable = new TestPageDrawable(this);
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void CanvasView_StartInteraction(object sender, TouchEventArgs e)
        {
            var point = e.Touches.First();
            _viewModel.OnCanvasClicked(point);
            _viewModel.UpdateArrow(point);
            CanvasView.Invalidate();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Canvas neu zeichnen, wenn Sichtbarkeit geändert wurde
            if (e.PropertyName == nameof(TestPageViewModel.IsProbeVisible) ||
                e.PropertyName == nameof(TestPageViewModel.IsClickPointVisible))
            {
                CanvasView.Invalidate();
            }
        }

        private class TestPageDrawable : IDrawable
        {
            private readonly TestPage _page;

            public TestPageDrawable(TestPage page)
            {
                _page = page;
            }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                // Hintergrund hellgrau füllen
                canvas.FillColor = Colors.LightGray;
                canvas.FillRectangle(dirtyRect);

                var vm = _page._viewModel;
                if (vm == null)
                    return;

                // Sonde zeichnen (X-förmig, rot)
                if (vm.IsProbeVisible)
                {
                    var p = vm.ProbePosition;
                    canvas.StrokeColor = Colors.Red;
                    canvas.StrokeSize = 4;
                    canvas.DrawLine((float)p.X - 10, (float)p.Y - 10, (float)p.X + 10, (float)p.Y + 10);
                    canvas.DrawLine((float)p.X + 10, (float)p.Y - 10, (float)p.X - 10, (float)p.Y + 10);
                }
                // Klickpunkt (blauer Punkt)
                if (vm.IsClickPointVisible)
                {
                    var p = vm.ClickPointPosition;
                    canvas.FillColor = Colors.Blue;
                    canvas.FillCircle((float)p.X, (float)p.Y, 5);
                }
            }
        }
    }
}