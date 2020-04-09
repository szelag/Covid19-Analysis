using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace PreProcessor
{
    public class ExportHelper : NotifyBase
    {
        private readonly MainViewModel _main;
        private bool _doNationalExport = true;
        private bool _doStateExport;
        private bool _doCountyExport;

        public ExportHelper(MainViewModel main) => _main = main;

        public bool DoNationalExport
        {
            get => _doNationalExport;
            set
            {
                if (_doNationalExport == value) return;
                _doNationalExport = value;
                OnPropertyChanged();
            }
        }

        public bool DoStateExport
        {
            get => _doStateExport;
            set
            {
                if (_doStateExport == value) return;
                _doStateExport = value;
                OnPropertyChanged();
            }
        }

        public bool DoCountyExport
        {
            get => _doCountyExport;
            set
            {
                if (_doCountyExport == value) return;
                _doCountyExport = value;
                OnPropertyChanged();
            }
        }

        public ICommand ExportData
        {
            get { return new RelayCommand(() => 
            { 
                if (_main.FilteredNationalData.Count() == 0) { MessageBox.Show("Gotta load data first, champ."); return; }

                if (!DoNationalExport && !DoStateExport && !DoCountyExport) { MessageBox.Show("Need to pick SOMETHING to export..."); return; }

                if (string.IsNullOrEmpty(_main.ExportDataPath))
                {
                    _main.PickExportFolder.Execute(null);
                    if (string.IsNullOrEmpty(_main.ExportDataPath)) return;
                }

                DateTimeFormatInfo dtfi = GetCustomDateFormat();

                if (DoNationalExport)
                {
                    IEnumerable<CovidDataPoint> relevantData = _main.FilteredNationalData;
                    string fileName = $"{_main.SelectedCountry} ({relevantData.Select(data => data.UpdateTime).Max().ToString("d", dtfi)}).csv";
                    Export(relevantData, Path.Combine(_main.ExportDataPath, fileName));
                }

                if (DoStateExport && !string.IsNullOrWhiteSpace(_main.SelectedState))
                {
                    IEnumerable<CovidDataPoint> relevantData = _main.FilteredStateData;
                    string fileName = $"{_main.SelectedCountry}-{_main.SelectedState} ({relevantData.Select(data => data.UpdateTime).Max().ToString("d", dtfi)}).csv";
                    Export(relevantData, Path.Combine(_main.ExportDataPath, fileName));
                }

                if (DoCountyExport && !string.IsNullOrWhiteSpace(_main.SelectedCounty))
                {
                    IEnumerable<CovidDataPoint> relevantData = _main.FilteredCountyData;
                    string fileName = $"{_main.SelectedCountry}-{_main.SelectedState}-{_main.SelectedCounty} ({relevantData.Select(data => data.UpdateTime).Max().ToString("d", dtfi)}).csv";
                    Export(relevantData, Path.Combine(_main.ExportDataPath, fileName));
                }

                MessageBox.Show("Export complete!");
            }); }
        }

        private void Export(IEnumerable<CovidDataPoint> data, string fullPath)
        {
            double[] timeData = data.Select(d => (d.UpdateTime - _main.ReferenceDate).TotalDays).ToArray();

            double[] confirmed = data.Select(d => (double)d.Confirmed).ToArray();
            double[] active = data.Select(d => (double)d.Confirmed).ToArray();
            double[] deaths = data.Select(d => (double)d.Deaths).ToArray();
            double[] recoveries = data.Select(d => (double)d.Recoveries).ToArray();

            double[] confirmed_rate = Utilities.ComputeDerivative(timeData, confirmed);
            double[] active_rate = Utilities.ComputeDerivative(timeData, active);
            double[] deaths_rate = Utilities.ComputeDerivative(timeData, deaths);
            double[] recoveries_rate = Utilities.ComputeDerivative(timeData, recoveries);

            List<string> outputContents = new List<string>();
            DateTimeFormatInfo dtfi = GetCustomDateFormat();
            outputContents.Add($"Days Since {_main.ReferenceDate.ToString("d", dtfi)},Confirmed,Active,Deaths,Recoveries,dConfirmed-dt,dActive-dt,dDeaths-dt,dRecoveries-dt");

            for (int i = 0; i < timeData.Length; i++)
            {
                outputContents.Add(string.Join(",", timeData[i],
                    confirmed[i], active[i], deaths[i], recoveries[i],
                    confirmed_rate[i], active_rate[i], deaths_rate[i], recoveries_rate[i]));
            }

            File.WriteAllLines(fullPath, outputContents);
        }

        private DateTimeFormatInfo GetCustomDateFormat()
        {
            // Just how I like my dates
            DateTimeFormatInfo dtfi = CultureInfo.CreateSpecificCulture("en-US").DateTimeFormat;
            dtfi.DateSeparator = "-";
            dtfi.ShortDatePattern = @"yyyy/MM/dd";
            return dtfi;
        }
    }
}
