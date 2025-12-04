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

            // PointerExited existiert nicht, daher MoveHoverInteraction für das Mouse-Leave-Handling verwenden.
            CanvasView.MoveHoverInteraction += (s, e) =>
            {
                // Prüfe, ob die Maus außerhalb des Canvas ist (z.B. X oder Y < 0 oder > Canvasgröße)
                var point = e.Touches.First();
                bool isInCanvas = point.X >= 0 && point.Y >= 0 && point.X < CanvasView.Width && point.Y < CanvasView.Height;
                // Beispiel: Setze die Mausposition auf (-1, -1), wenn außerhalb (hier nur als Platzhalter)
                if (!isInCanvas)
                {
                    _viewModel.CurrentMousePosition = new Point(-1, -1);
                    _viewModel.UpdateAudioForMousePosition();
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

            // Keine Änderung der Sondenposition beim Hover/Move
            _viewModel.CurrentMousePosition = point;

            if (!_viewModel.IsRunNotStarted)
            {
                _viewModel.UpdateAudioForMousePosition();
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