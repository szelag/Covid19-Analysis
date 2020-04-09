using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

namespace PreProcessor
{
    public class MainViewModel : NotifyBase
    {
        private bool _isIdle;
        private bool _excludeBeforeReference;
        private string _selectedCountry;
        private string _selectedState;
        private string _selectedCounty;
        private string _message;
        private string _sourceDataPath;
        private string _exportDataPath;
        private DateTime _referenceDate;
        private ObservableCollection<string> _allCountries;
        private ObservableCollection<string> _allStatesThisCountry;
        private ObservableCollection<string> _allCountiesThisState;
        private ConcurrentBag<CovidDataPoint> _allNationalData;
        private ConcurrentBag<StateDataPoint> _allStateData;
        private ConcurrentBag<CountyDataPoint> _allCountyData ;

        public MainViewModel()
        {
            _referenceDate = new DateTime(2020, 3, 1);
            _allNationalData = new ConcurrentBag<CovidDataPoint>();
            _allStateData = new ConcurrentBag<StateDataPoint>();
            _allCountyData = new ConcurrentBag<CountyDataPoint>();
            _allCountries = new ObservableCollection<string>();
            _allStatesThisCountry = new ObservableCollection<string>();
            _allCountiesThisState = new ObservableCollection<string>();
            _isIdle = true;

            SourceDataPath = Properties.Settings.Default.SourceDataLocation;
            ExportDataPath = Properties.Settings.Default.ExportLocation;

            Exporter = new ExportHelper(this);
            Plotter = new PlotHelper(this);
        }

         ~MainViewModel()
        {
            Properties.Settings.Default.SourceDataLocation = SourceDataPath;
            Properties.Settings.Default.ExportLocation = ExportDataPath;
            Properties.Settings.Default.Save();
        }

        public event EventHandler UserDataSelectionChanged;

        private DateTime FilterReferenceDate => ExcludeBeforeReference ? ReferenceDate : new DateTime(1900, 1, 1);

        public ExportHelper Exporter { get; private set; }

        public PlotHelper Plotter { get; private set; }

        public IEnumerable<CovidDataPoint> FilteredNationalData
        {
            get => _allNationalData
                       .Where(data => data.Country == SelectedCountry 
                              && data.UpdateTime >= FilterReferenceDate)
                       .OrderBy(data => data.UpdateTime);
        }

        public IEnumerable<StateDataPoint> FilteredStateData
        {
            get => _allStateData
                       .Where(data => data.Country == SelectedCountry 
                              && data.State == SelectedState 
                              && data.UpdateTime >= FilterReferenceDate)
                       .OrderBy(data => data.UpdateTime);
        }

        public IEnumerable<CountyDataPoint> FilteredCountyData
        {
            get => _allCountyData
                       .Where(data => data.Country == SelectedCountry 
                              && data.State == SelectedState 
                              && data.County == SelectedCounty 
                              && data.UpdateTime >= FilterReferenceDate)
                       .OrderBy(data => data.UpdateTime);
        }

        public bool ExcludeBeforeReference
        {
            get => _excludeBeforeReference;
            set
            {
                if (_excludeBeforeReference == value) ;
                _excludeBeforeReference = value;
                OnPropertyChanged();
                OnUserDataSelectionChanged();
            }
        }

        public bool IsIdle
        {
            get => _isIdle;
            set
            {
                if (_isIdle == value) return;
                _isIdle = value;
                OnPropertyChanged();
            }
        }

        public string SourceDataPath
        {
            get => _sourceDataPath;
            set
            {
                if (_sourceDataPath == value) return;
                _sourceDataPath = value;
                OnPropertyChanged();
            }
        }

        public string ExportDataPath
        {
            get => _exportDataPath;
            set
            {
                if (_exportDataPath == value) return;
                _exportDataPath = value;
                OnPropertyChanged();
            }
        }

        public string Message
        {
            get => _message;
            set
            {
                if (_message == value) return;
                _message = value;
                OnPropertyChanged();
            }
        }

        public string SelectedCountry
        {
            get => _selectedCountry;
            set
            {
                if (_selectedCountry == value) return;
                _selectedCountry = value;
                OnPropertyChanged();
                UpdateStates();
                OnUserDataSelectionChanged();
            }
        }

        public string SelectedState
        {
            get => _selectedState;
            set
            {
                if (_selectedState == value) return;
                _selectedState = value;
                OnPropertyChanged();
                UpdateCounties();
                OnUserDataSelectionChanged();
            }
        }

        public string SelectedCounty
        {
            get => _selectedCounty;
            set
            {
                if (_selectedCounty == value) return;
                _selectedCounty = value;
                OnPropertyChanged();
                OnUserDataSelectionChanged();
            }
        }

        public DateTime ReferenceDate
        {
            get => _referenceDate;
            set
            {
                if (_referenceDate == value) return;
                _referenceDate = value;
                OnPropertyChanged();
                OnUserDataSelectionChanged();
            }
        }

        public ObservableCollection<string> AllCountries
        {
            get => _allCountries;
            set
            {
                if (_allCountries == value) return;
                _allCountries = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AllStatesThisCountry
        {
            get => _allStatesThisCountry;
            set
            {
                if (_allStatesThisCountry == value) return;
                _allStatesThisCountry = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> AllCountiesThisState
        {
            get => _allCountiesThisState;
            set
            {
                if (_allCountiesThisState == value) return;
                _allCountiesThisState = value;
                OnPropertyChanged();
            }
        }

        public ICommand Process
        {
            get
            {
                return new RelayCommand(async () =>
                {
                    if (string.IsNullOrEmpty(SourceDataPath))
                    {
                        PickSourceFolder.Execute(null);
                        if (string.IsNullOrEmpty(SourceDataPath)) return;
                    }

                    string[] allReportFiles = Directory.GetFiles(SourceDataPath, "*.csv");

                    Message = "Processing, please wait";
                    IsIdle = false;

                    await Task.Factory.StartNew(() => ParseFilesParallel(allReportFiles));

                    Message = "";
                    IsIdle = true;

                    AllCountries = new ObservableCollection<string>(_allNationalData
                        .Select(data => data.Country)
                        .Distinct()
                        .OrderBy(name => name));

                    // Because I'm American and this is my primary concern...
                    SelectedCountry = "US";

                });
            }
        }

        public ICommand PickSourceFolder
        {
            get { return new RelayCommand(() => 
            {
                var dialog = new FolderBrowserDialog
                {
                    Description = "Select JHU CSSE Daily Report Location"
                };

                if (dialog.ShowDialog() != DialogResult.OK) return;

                SourceDataPath = dialog.SelectedPath;
            }); }
        }

        public ICommand PickExportFolder
        {
            get { return new RelayCommand(() => 
            {
                var dialog = new FolderBrowserDialog
                {
                    Description = "Select Location for Export"
                };

                if (dialog.ShowDialog() != DialogResult.OK) return;

                ExportDataPath = dialog.SelectedPath;
            }); }
        }

        private void UpdateStates()
        {
            AllStatesThisCountry = new ObservableCollection<string>(_allStateData
                .Where(data => data.Country == SelectedCountry)
                .Select(data => data.State)
                .Distinct()
                .OrderBy(name => name));

            SelectedState = AllStatesThisCountry.First();
        }

        private void UpdateCounties()
        {
            AllCountiesThisState = new ObservableCollection<string>(_allCountyData
                .Where(data => data.Country == SelectedCountry && data.State == SelectedState)
                .Select(data => data.County)
                .Distinct()
                .OrderBy(name => name));

            if (AllCountiesThisState.Count > 0)
            {
                SelectedCounty = AllCountiesThisState.First();
            }
            else
            {
                SelectedCounty = "";
            } 
        }

        private void OnUserDataSelectionChanged() => UserDataSelectionChanged?.Invoke(this, new EventArgs());

        private void ParseFilesParallel(string[] allReportFiles)
        {
            int onFile = 0;
            int nFiles = allReportFiles.Length;

            // Degree of parallelism here is arbitrary, could play around with it
            Parallel.ForEach(allReportFiles, new ParallelOptions { MaxDegreeOfParallelism = 3 }, filePath =>
            {
                onFile++;
                Message = $"Processing {onFile} of {nFiles} reports...";

                string[] allLines = File.ReadAllLines(filePath);
                var thisDayStateData = new List<StateDataPoint>();

                /* About half way through the Johns Hopkins data they change format and start breaking things
                   down by county, not just by state (at least within the US). As a result, two different
                   ways of parsing that data and adding it to the aggregate. */

                if (allLines[0].StartsWith("FIPS"))
                {
                    IEnumerable<CountyDataPoint> thisDayCountyData = Import.ParseAsCountyData(allLines.Skip(1));
                    foreach (CountyDataPoint item in thisDayCountyData)
                    {
                        _allCountyData.Add(item);
                    }
                    thisDayStateData.AddRange(Import.SummarizeByState(thisDayCountyData));
                }
                else
                {
                    thisDayStateData.AddRange(Import.ParseAsStateData(allLines.Skip(1)));
                }

                // Can't .AddRange() on a ConcurrentBag<T> apparently...
                foreach (StateDataPoint item in thisDayStateData) { _allStateData.Add(item); }
                
                foreach (CovidDataPoint item in Import.SummarizeNational(thisDayStateData)) { _allNationalData.Add(item); }
            });
        }

 
    }
}
