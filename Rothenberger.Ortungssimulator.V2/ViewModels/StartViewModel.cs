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
        [ObservableProperty] private string userName = "Probant";
        [ObservableProperty] private ToneType selectedToneType;
        [ObservableProperty] private int testRunCount = 10;
        [ObservableProperty] private bool showArrow;
        [ObservableProperty] private string userNameError = "";
        [ObservableProperty] private string toneTypeError = "";
        [ObservableProperty] private string runCountError = "";

        public ObservableCollection<ToneType> ToneTypes { get; set; } = new();

        public IRelayCommand StartCommand { get; }

        public StartViewModel()
        {
            LoadToneTypes();
            StartCommand = new RelayCommand(OnStart);
        }

        private void LoadToneTypes()
        {
            var json = File.ReadAllText("Data/tonetypes.json");
            var types = JsonSerializer.Deserialize<List<ToneType>>(json);
            ToneTypes.Clear();
            foreach (var type in types)
                ToneTypes.Add(type);

            // Shepard vorauswählen
            SelectedToneType = ToneTypes.FirstOrDefault(t => t.Id == "shepard");
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

            if (!valid)
                return;

            var testPage = new TestPage();
            if (testPage.BindingContext is TestPageViewModel vm)
            {
                vm.Initialize(UserName, SelectedToneType, TestRunCount, ShowArrow);
            }
            await Shell.Current.Navigation.PushAsync(testPage);
        }
    }
}