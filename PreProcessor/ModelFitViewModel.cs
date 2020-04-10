using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PreProcessor
{
    public class ModelFitViewModel : NotifyBase
    {
        private readonly double[] _timeData;
        private readonly double[] _caseData;
        private double _peakActiveCaseCount;
        private double _daysSinceReferenceForPeak;
        private double _rateFactor;
        private double _extendModelDays;
        private PlotModel _modelDataOverlay;

        public ModelFitViewModel(double[] timeData, double[] caseData)
        {
            _timeData = timeData;
            _caseData = caseData;

            _peakActiveCaseCount = _caseData.Max();
            // TODO Find based on peak number
            _daysSinceReferenceForPeak = _timeData.Max();
            _rateFactor = 15;

            UpdatePlot();
        }

        public double PeakActiveCaseCount
        {
            get => _peakActiveCaseCount;
            set
            {
                if (_peakActiveCaseCount == value) return;
                _peakActiveCaseCount = value;
                OnPropertyChanged();
                UpdatePlot();
            }
        }

        public double DaysSinceReferenceForPeak
        {
            get => _daysSinceReferenceForPeak;
            set
            {
                if (_daysSinceReferenceForPeak == value) return;
                _daysSinceReferenceForPeak = value;
                OnPropertyChanged();
                UpdatePlot();
            }
        }

        public double RateFactor
        {
            get => _rateFactor;
            set
            {
                if (_rateFactor == value) return;
                _rateFactor = value;
                OnPropertyChanged();
                UpdatePlot();
            }
        }

        public double ExtendModelDays
        {
            get => _extendModelDays;
            set
            {
                if (_extendModelDays == value) return;
                _extendModelDays = value;
                OnPropertyChanged();
            }
        }

        public PlotModel ModelDataOverlay
        {
            get => _modelDataOverlay;
            set
            {
                if (_modelDataOverlay == value) return;
                _modelDataOverlay = value;
                OnPropertyChanged();
            }
        }

        private void UpdatePlot()
        {
            var measurementScatter = new ScatterSeries() { MarkerType = MarkerType.Circle, MarkerFill = OxyColors.DodgerBlue };
            measurementScatter.Points.AddRange(_timeData.Zip(_caseData, (x, y) => new ScatterPoint(x, y)));

            int nModelPoints = 100;
            double modelXMin = _timeData[0];
            double modelXMax = _timeData.Last() + ExtendModelDays;
            double xStep = (modelXMax - modelXMin) / (nModelPoints - 1);
            IEnumerable<double> xModel = Enumerable.Range(0, nModelPoints).Select(i => modelXMin + (double)i * xStep);
            IEnumerable<double> yModel = xModel.Select(x => Gauss.Evaluate(x, PeakActiveCaseCount, DaysSinceReferenceForPeak, RateFactor));
            var modelLineSeries = new LineSeries { Color = OxyColors.Red };
            modelLineSeries.Points.AddRange(xModel.Zip(yModel, (x, y) => new DataPoint(x, y)));

            var model = new PlotModel();
            model.Series.Add(measurementScatter);
            model.Series.Add(modelLineSeries);

            ModelDataOverlay = model;
        }

        private double[] FitModel()
        {
            throw new NotImplementedException();
        }
    }
}
