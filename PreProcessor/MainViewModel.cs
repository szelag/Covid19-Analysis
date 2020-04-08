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
        private string _selectedCountry;
        private string _selectedState;
        private string _selectedCounty;
        private string _message;
        private ObservableCollection<string> _allCountries = new ObservableCollection<string>();
        private ObservableCollection<string> _allStatesThisCountry = new ObservableCollection<string>();
        private ObservableCollection<string> _allCountiesThisState = new ObservableCollection<string>();
        private ConcurrentBag<CovidDataPoint> _allNationalData = new ConcurrentBag<CovidDataPoint>();
        private ConcurrentBag<StateDataPoint> _allStateData = new ConcurrentBag<StateDataPoint>();
        private ConcurrentBag<CountyDataPoint> _allCountyData = new ConcurrentBag<CountyDataPoint>();

        public string Message
        {
            get => _message;
            set
            {
                if (_message == value) return;
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

                    await Task.Factory.StartNew(() => ParseFilesParallel(allReportFiles));

                    AllCountries = new ObservableCollection<string>(_allNationalData
                        .Select(data => data.Country)
                        .Distinct()
                        .OrderBy(name => name));

                    SelectedCountry = "US";

                });
            }
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

            SelectedCounty = AllCountiesThisState.First();
        }

        private void ParseFilesParallel(string[] allReportFiles)
        {
            Message = "Processing, please wait";

            Parallel.ForEach(allReportFiles, filePath =>
            {
                string[] allLines = File.ReadAllLines(filePath);
                var thisDayStateData = new List<StateDataPoint>();

                if (allLines[0].StartsWith("FIPS"))
                {
                    IEnumerable<CountyDataPoint> thisDayCountyData = ParseAsCountyData(allLines.Skip(1));
                    foreach (CountyDataPoint item in thisDayCountyData)
                    {
                        _allCountyData.Add(item);
                    }
                    thisDayStateData.AddRange(SummarizeByState(thisDayCountyData));
                }
                else
                {
                    thisDayStateData.AddRange(ParseAsStateData(allLines.Skip(1)));
                }

                foreach (StateDataPoint item in thisDayStateData)
                {
                    _allStateData.Add(item);
                }

                foreach (CovidDataPoint item in SummarizeNational(thisDayStateData))
                {
                    _allNationalData.Add(item);
                }
            });

            Message = "";
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
            DateTime thisDate = thisDayCountyData.First().UpdateTime;

            foreach (string country in thisDayCountyData.Select(data => data.Country).Distinct())
            {

                foreach (string state in thisDayCountyData
                    .Where(data => data.Country == country) 
                    .Select(data => data.State).Distinct())
                {
                    IEnumerable <CountyDataPoint> relevantData = thisDayCountyData.Where(data => data.Country == country && data.State == state);

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
                // Messy...
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
            Match monthDayYearLong = new Regex(@"\d{1,2}\/\d{1,2}\/\d{4}").Match(dateString);
            Match monthDayYearShort = new Regex(@"\d{1,2}\/\d{1,2}\/\d{2}\s").Match(dateString);
            Match yearMonthDay = new Regex(@"\d{4}-\d{2}-\d{2}").Match(dateString);

            int year = 1985;
            int month = 2;
            int day = 22;

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
