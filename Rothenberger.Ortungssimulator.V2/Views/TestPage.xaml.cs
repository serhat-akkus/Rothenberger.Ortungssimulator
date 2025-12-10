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
            CanvasView.EndHoverInteraction += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("Maus hat das Canvas verlassen.");
                _viewModel.CurrentMousePosition = new Point(-1, -1);
                _viewModel.UpdateAudioForMousePosition(false);
            };

            CanvasView.Drawable = new TestPageDrawable(this);
            ArrowView.Drawable = new ArrowBoxDrawable(_viewModel);
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            _viewModel.CurrentMousePosition = new Point(500, 500); // Canvas-Mitte

            // PointerExited existiert nicht, daher MoveHoverInteraction für das Mouse-Leave-Handling verwenden.
            CanvasView.MoveHoverInteraction += (s, e) =>
            {
                var point = e.Touches.First();
                bool isInCanvas = point.X >= 0 && point.Y >= 0 && point.X < CanvasView.Width && point.Y < CanvasView.Height;
                if (!isInCanvas)
                {
                    _viewModel.CurrentMousePosition = new Point(-1, -1);
                    bool mouseOverCanvas = _viewModel.IsMouseOverCanvas(point, CanvasView.X, CanvasView.Y, CanvasView.Width, CanvasView.Height);
                    System.Diagnostics.Debug.WriteLine($"mouseOverCanvas: {mouseOverCanvas}");
                    _viewModel.UpdateAudioForMousePosition(mouseOverCanvas);
                }
            };
        }

        private void CanvasView_StartInteraction(object sender, TouchEventArgs e)
        {
            var point = e.Touches.First();

            // Rotes X an der Klickstelle setzen
            _viewModel.OnCanvasClicked(point);
            _viewModel.IsClickPointVisible = true;

            // Blauen Punkt an der (bestehenden) Sondenposition anzeigen
            // WICHTIG: Keine Änderung an ProbePosition
            _viewModel.IsProbeVisible = true;

            CanvasView.Invalidate();
            ArrowView.Invalidate();
        }

        private void CanvasView_MoveHoverInteraction(object sender, TouchEventArgs e)
        {
            var point = e.Touches.First();
            _viewModel.CurrentMousePosition = point;

            if (!_viewModel.IsRunNotStarted)
            {
                _viewModel.UpdateAudioForMousePosition(true);
            }

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
    }
}