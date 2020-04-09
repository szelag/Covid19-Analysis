using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PreProcessor
{
    public class PlotHelper : NotifyBase
    {
        private readonly MainViewModel _main;
        private bool _useLogYScale;
        private PlotModel _activePlot;
        private string _selectedPlotDataSource;
        private string _selectedPlotType;
        private string _selectedMeasurement;
        private ObservableCollection<string> _plotDataSources;
        private ObservableCollection<string> _measurements;
        private ObservableCollection<string> _plotTypes;

        public PlotHelper(MainViewModel main)
        { 
            _main = main;
            _main.UserDataSelectionChanged += (sender, args) => UpdatePlot();

            _useLogYScale = false;
            PlotDataSources = new ObservableCollection<string> { "National", "State", "County" };
            PlotTypes = new ObservableCollection<string> { "Quantity" /*, "Daily Rate" */ };
            Measurements = new ObservableCollection<string> { "Confirmed Cases", "Active Cases", "Deaths", "Recoveries" };

            SelectedPlotDataSource = PlotDataSources[0];
            SelectedPlotType = PlotTypes[0];
            SelectedMeasurement = Measurements[0];
        }

        public bool UseLogScale
        {
            get => _useLogYScale;
            set
            {
                if (_useLogYScale == value) return;
                _useLogYScale = value;
                OnPropertyChanged();
                UpdatePlot();
            }
        }

        public string SelectedPlotDataSource
        {
            get => _selectedPlotDataSource;
            set
            {
                if (_selectedPlotDataSource == value) return;
                _selectedPlotDataSource = value;
                OnPropertyChanged();
                UpdatePlot();
            }
        }

        public string SelectedPlotType
        {
            get => _selectedPlotType;
            set
            {
                if (_selectedPlotType == value) return;
                _selectedPlotType = value;
                OnPropertyChanged();
                UpdatePlot();
            }
        }

        public string SelectedMeasurement
        {
            get => _selectedMeasurement;
            set
            {
                if (_selectedMeasurement == value) return;
                _selectedMeasurement = value;
                OnPropertyChanged();
                UpdatePlot();
            }
        }

        public ObservableCollection<string> PlotDataSources
        {
            get => _plotDataSources;
            set
            {
                if (_plotDataSources == value) return;
                _plotDataSources = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> PlotTypes
        {
            get => _plotTypes;
            set
            {
                if (_plotTypes == value) return;
                _plotTypes = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<string> Measurements
        {
            get => _measurements;
            set
            {
                if (_measurements == value) return;
                _measurements = value;
                OnPropertyChanged();
            }
        }

        public PlotModel ActivePlot
        {
            get => _activePlot;
            set
            {
                if (_activePlot == value) return;
                _activePlot = value;
                OnPropertyChanged();
            }
        }

        private void UpdatePlot()
        {
            if (_main.FilteredNationalData.Count() == 0) return;

            IEnumerable<CovidDataPoint> sourceData = null;
            string title = "";
            switch (SelectedPlotDataSource)
            {
                case "National":
                    sourceData = _main.FilteredNationalData;
                    title = _main.SelectedCountry;
                    break;
                case "State":
                    sourceData = _main.FilteredStateData;
                    title = _main.SelectedState;
                    break;
                case "County":
                    sourceData = _main.FilteredCountyData;
                    title = _main.SelectedCounty;
                    break;
            }

            double[] timeData = sourceData.Select(data => (data.UpdateTime - _main.ReferenceDate).TotalDays).ToArray();
            IEnumerable<int> yData = null;

            switch (SelectedMeasurement)
            {
                case "Confirmed Cases":
                    yData = sourceData.Select(data => data.Confirmed);
                    break;
                case "Deaths":
                    yData = sourceData.Select(data => data.Deaths);
                    break;
                case "Active Cases":
                    yData = sourceData.Select(data => data.Active);
                    break;
                case "Recoveries":
                    yData = sourceData.Select(data => data.Recoveries);
                    break;
            }

            // TODO Quantity vs. rate

            ActivePlot = PlotModelBuilder(title, timeData, yData);
        }

        private PlotModel PlotModelBuilder(string title, double[] timeData, IEnumerable<int> yData)
        {
            ScatterSeries xySeries = new ScatterSeries
            {
                MarkerFill = OxyColors.DodgerBlue,
                MarkerType = MarkerType.Circle
            };
            xySeries.Points.AddRange(timeData.Zip(yData, (x, y) => new ScatterPoint(x, y)));

            PlotModel plot = new PlotModel() { Title = title };
            plot.Series.Add(xySeries);

            LinearAxis xAxis = new LinearAxis
            {
                Title = $"Days since {_main.ReferenceDate.ToLongDateString()}",
                MajorGridlineStyle = LineStyle.Dash,
                Position = AxisPosition.Bottom
            };

            Axis yAxis;
            if (UseLogScale)
            {
                yAxis = new LogarithmicAxis();
            }
            else
            {
                yAxis = new LinearAxis();
            }

            yAxis.Title = SelectedMeasurement;
            yAxis.MajorGridlineStyle = LineStyle.Dash;
            yAxis.Position = AxisPosition.Left;

            plot.Axes.Add(xAxis);
            plot.Axes.Add(yAxis);
            return plot;
        }
    }
}
