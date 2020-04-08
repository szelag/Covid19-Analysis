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
        private string _selectedCountry;
        private string _selectedState;
        private string _selectedCounty;
        private string _message;
        private ObservableCollection<string> _allCountries;
        private ObservableCollection<string> _allStatesThisCountry;
        private ObservableCollection<string> _allCountiesThisState;

        public MainViewModel()
        {
            Exporter = new ExportHelper(this);
            _allCountries = new ObservableCollection<string>();
            _allStatesThisCountry = new ObservableCollection<string>();
            _allCountiesThisState = new ObservableCollection<string>();
            _isIdle = true;
        }

        public ConcurrentBag<CovidDataPoint> AllNationalData { get; private set; } = new ConcurrentBag<CovidDataPoint>();
        public ConcurrentBag<StateDataPoint> AllStateData { get; private set; } = new ConcurrentBag<StateDataPoint>();
        public ConcurrentBag<CountyDataPoint> AllCountyData { get; private set; } = new ConcurrentBag<CountyDataPoint>();

        public ExportHelper Exporter { get; private set; }

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
                    var dialog = new FolderBrowserDialog
                    {
                        Description = "Select JHU CSSE Daily Report Location"
                    };

                    if (dialog.ShowDialog() != DialogResult.OK) return;

                    string[] allReportFiles = Directory.GetFiles(dialog.SelectedPath, "*.csv");

                    Message = "Processing, please wait";
                    IsIdle = false;

                    await Task.Factory.StartNew(() => ParseFilesParallel(allReportFiles));

                    Message = "";
                    IsIdle = true;

                    AllCountries = new ObservableCollection<string>(AllNationalData
                        .Select(data => data.Country)
                        .Distinct()
                        .OrderBy(name => name));

                    // Because I'm American and this is my primary concern...
                    SelectedCountry = "US";

                });
            }
        }

        private void UpdateStates()
        {
            AllStatesThisCountry = new ObservableCollection<string>(AllStateData
                .Where(data => data.Country == SelectedCountry)
                .Select(data => data.State)
                .Distinct()
                .OrderBy(name => name));

            SelectedState = AllStatesThisCountry.First();
        }

        private void UpdateCounties()
        {
            AllCountiesThisState = new ObservableCollection<string>(AllCountyData
                .Where(data => data.Country == SelectedCountry && data.State == SelectedState)
                .Select(data => data.County)
                .Distinct()
                .OrderBy(name => name));

            SelectedCounty = AllCountiesThisState.First();
        }

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
                    IEnumerable<CountyDataPoint> thisDayCountyData = ParseAsCountyData(allLines.Skip(1));
                    foreach (CountyDataPoint item in thisDayCountyData)
                    {
                        AllCountyData.Add(item);
                    }
                    thisDayStateData.AddRange(SummarizeByState(thisDayCountyData));
                }
                else
                {
                    thisDayStateData.AddRange(ParseAsStateData(allLines.Skip(1)));
                }

                // Can't .AddRange() on a ConcurrentBag<T> apparently...
                foreach (StateDataPoint item in thisDayStateData) { AllStateData.Add(item); }
                
                foreach (CovidDataPoint item in SummarizeNational(thisDayStateData)) { AllNationalData.Add(item); }
            });
        }

        private IEnumerable<CovidDataPoint> SummarizeNational(List<StateDataPoint> thisDayStateData)
        {
            DateTime thisDate = thisDayStateData.First().UpdateTime;

            foreach (string country in thisDayStateData.Select(data => data.Country).Distinct())
            {
                IEnumerable<StateDataPoint> relevantData = thisDayStateData.Where(data => data.Country == country);

                yield return new CovidDataPoint()
                {
                    Country = country,
                    Confirmed = relevantData.Select(data => data.Confirmed).Sum(),
                    Deaths = relevantData.Select(data => data.Deaths).Sum(),
                    Recoveries = relevantData.Select(data => data.Recoveries).Sum(),
                    UpdateTime = thisDate
                };
            }
        }

        private IEnumerable<StateDataPoint> SummarizeByState(IEnumerable<CountyDataPoint> thisDayCountyData)
        {
            /* Developer note: Found a surprisingly big chunk of speed here by using a couple CountyDataPoint arrays
               rather than just using the input IEnumerable all the way through. */

            CountyDataPoint[] dataArr = thisDayCountyData.ToArray();
            DateTime thisDate = dataArr.First().UpdateTime;

            foreach (string country in dataArr.Select(data => data.Country).Distinct())
            {
                foreach (string state in dataArr
                    .Where(data => data.Country == country) 
                    .Select(data => data.State)
                    .Distinct())
                {
                    CountyDataPoint[] relevantData = dataArr.Where(data => data.Country == country && data.State == state).ToArray();

                    yield return new StateDataPoint()
                    {
                        Country = country,
                        State = state,
                        Confirmed = relevantData.Select(data => data.Confirmed).Sum(),
                        Deaths = relevantData.Select(data => data.Deaths).Sum(),
                        Recoveries = relevantData.Select(data => data.Recoveries).Sum(),
                        UpdateTime = thisDate
                    };
                }
            }
        }

        private IEnumerable<StateDataPoint> ParseAsStateData(IEnumerable<string> allLines)
        {
            // Headers are:
            // Province/State,Country/Region,Last Update,Confirmed,Deaths,Recovered,Latitude,Longitude
            foreach (string line in allLines)
            {
                /* In some early data there's a trickle of entries that start to get added that are like
                   "Boston, MA" etc. That messes with this CSV parsing, so I decide to skip it entirely. */
                if (line.Contains('"')) continue;

                string[] split = line.Split(',');
                
                int.TryParse(split[3], out int confirmed);
                int.TryParse(split[4], out int deaths);
                int.TryParse(split[5], out int recoveries);

                yield return new StateDataPoint()
                {
                    State = split[0],
                    Country = split[1],
                    UpdateTime = DateParser(split[2]),
                    Confirmed = confirmed,
                    Deaths = deaths,
                    Recoveries = recoveries
                };
            }
        }

        private IEnumerable<CountyDataPoint> ParseAsCountyData(IEnumerable<string> allLines)
        {
            // Headers are:
            // FIPS,Admin2,Province_State,Country_Region,Last_Update,Lat,Long_,Confirmed,Deaths,Recovered,Active,Combined_Key
            foreach (string line in allLines)
            {
                string[] split = line.Split(',');
                /* For the time being, not particularly interested in some of the random sparse data that doesn't have 
                   the county, state/province, and nation all paired up */
                if (string.IsNullOrWhiteSpace(split[1]) || string.IsNullOrWhiteSpace(split[2]) || string.IsNullOrWhiteSpace(split[3]))
                { 
                    continue;
                }

                int.TryParse(split[7], out int confirmed);
                int.TryParse(split[8], out int deaths);
                int.TryParse(split[9], out int recoveries);

                yield return new CountyDataPoint()
                {
                    County = split[1],
                    State = split[2],
                    Country = split[3],
                    UpdateTime = DateParser(split[4]),
                    Confirmed = confirmed,
                    Deaths = deaths,
                    Recoveries = recoveries
                };
            }
        }

        private DateTime DateParser(string dateString)
        {
            /* The Johns Hopkins data uses a variety of date/time formats (whyyyy..??), so
               this helper method was needed to sort them out. Was about time I learned how to use
               a Regex... */

            Match monthDayYearLong = new Regex(@"\d{1,2}\/\d{1,2}\/\d{4}").Match(dateString);
            Match monthDayYearShort = new Regex(@"\d{1,2}\/\d{1,2}\/\d{2}\s").Match(dateString);
            Match yearMonthDay = new Regex(@"\d{4}-\d{2}-\d{2}").Match(dateString);

            // Arbitrary values for initialization
            int year = 1970;
            int month = 1;
            int day = 1;

            if (monthDayYearLong.Success)
            {
                string[] split = monthDayYearLong.Value.Split('/');
                year = int.Parse(split[2]);
                month = int.Parse(split[0]);
                day = int.Parse(split[1]);
            }
            else if (monthDayYearShort.Success)
            {
                string[] split = monthDayYearShort.Value.Trim().Split('/');
                year = 2000 + int.Parse(split[2]);
                month = int.Parse(split[0]);
                day = int.Parse(split[1]);
            }
            else if (yearMonthDay.Success)
            {
                string[] split = yearMonthDay.Value.Split('-');
                year = int.Parse(split[0]);
                month = int.Parse(split[1]);
                day = int.Parse(split[2]);
            }

            return new DateTime(year, month, day);
        }
    }
}
