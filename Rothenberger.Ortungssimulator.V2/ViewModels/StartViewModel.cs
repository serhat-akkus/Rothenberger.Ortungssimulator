using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;
using Rothenberger.Ortungssimulator.V2.Models;
using Rothenberger.Ortungssimulator.V2.Views;
using System.Linq;

namespace Rothenberger.Ortungssimulator.V2.ViewModels
{
    public partial class StartViewModel : ObservableObject
    {
        [ObservableProperty] private string userName = "Proband";
        [ObservableProperty] private ToneType selectedToneType;
        [ObservableProperty] private int testRunCount = 7;
        [ObservableProperty] private bool showArrow = true;
        [ObservableProperty] private bool showProbeRadius = true;
        [ObservableProperty] private bool isEinklinkSoundEnabled = false;
        [ObservableProperty] private string userNameError = "";
        [ObservableProperty] private string toneTypeError = "";
        [ObservableProperty] private string runCountError = "";
        [ObservableProperty] private double probeDepthMeters = 0.0;
        [ObservableProperty] private bool isProbeRadiusRandom = true;

        public ObservableCollection<ToneType> ToneTypes { get; set; } = new();

        public IRelayCommand StartCommand { get; }

        public StartViewModel()
        {
            LoadToneTypes();
            StartCommand = new RelayCommand(OnStart);
        }

        private void LoadToneTypes()
        {
            var exeDir = AppContext.BaseDirectory;
            var jsonPath = Path.Combine(exeDir, "tonetypes.json");
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException($"Die Datei 'tonetypes.json' wurde im Anwendungsverzeichnis nicht gefunden: {jsonPath}");

            var lines = File.ReadAllLines(jsonPath);
            var jsonLines = lines
                .SkipWhile(line => !line.TrimStart().StartsWith("["))
                .ToArray();
            var json = string.Join(Environment.NewLine, jsonLines);

            var types = JsonSerializer.Deserialize<List<ToneType>>(json);
            ToneTypes.Clear();
            foreach (var type in types)
                ToneTypes.Add(type);

            SelectedToneType = ToneTypes.FirstOrDefault();
        }

        private async void OnStart()
        {
            bool valid = true;
            UserNameError = "";
            ToneTypeError = "";
            RunCountError = "";

            if (string.IsNullOrWhiteSpace(UserName))
            {
                UserNameError = "Bitte Namen eingeben.";
                valid = false;
            }
            if (SelectedToneType == null)
            {
                ToneTypeError = "Bitte Tontyp auswählen.";
                valid = false;
            }
            if (TestRunCount < 1)
            {
                RunCountError = "Anzahl der Runs muss mindestens 1 sein.";
                valid = false;
            }
            if (ProbeDepthMeters < 0.0 || ProbeDepthMeters > 3.0)
            {
                valid = false;
            }

            if (!valid)
                return;

            var testPage = new TestPage();
            if (testPage.BindingContext is TestPageViewModel vm)
            {
                vm.Initialize(
                    UserName,
                    SelectedToneType,
                    TestRunCount,
                    ShowArrow,
                    ShowProbeRadius,
                    ProbeDepthMeters,
                    IsEinklinkSoundEnabled,
                    IsProbeRadiusRandom
                );
            }
            await Shell.Current.Navigation.PushAsync(testPage);
        }
    }
}