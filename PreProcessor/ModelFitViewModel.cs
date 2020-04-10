using GalaSoft.MvvmLight.Command;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

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
        private PlotModel _modelDataDerivativeOverlay;

        public ModelFitViewModel(double[] timeData, double[] caseData)
        {
            _timeData = timeData;
            _caseData = caseData;

            var pairedData = _timeData.Zip(_caseData, (time, cases) => new { Days = time, CaseCount = cases });

            _peakActiveCaseCount = _caseData.Max();
            _daysSinceReferenceForPeak = pairedData.First(data => data.CaseCount == _caseData.Max()).Days;
            _rateFactor = 15;
            _extendModelDays = 5;

            UpdatePlots();
        }

        public double PeakActiveCaseCount
        {
            get => _peakActiveCaseCount;
            set
            {
                if (_peakActiveCaseCount == value) return;
                _peakActiveCaseCount = value;
                OnPropertyChanged();
                UpdatePlots();
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
                UpdatePlots();
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
                UpdatePlots();
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
                UpdatePlots();
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

        public PlotModel ModelDataDerivativeOverlay
        {
            get => _modelDataDerivativeOverlay;
            set
            {
                if (_modelDataDerivativeOverlay == value) return;
                _modelDataDerivativeOverlay = value;
                OnPropertyChanged();
            }
        }

        public ICommand AutoFit
        {
            get { return new RelayCommand(() => 
            {
                double[] newCoefficients = FitModel();
                PeakActiveCaseCount = newCoefficients[0];
                DaysSinceReferenceForPeak = newCoefficients[1];
                RateFactor = newCoefficients[2];
            }); }
        }

        private void UpdatePlots()
        {
            /* ---------- Quantity ---------- */
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
            LinearAxis measurementXAxis = new LinearAxis
            {
                Title = "Days Since Reference",
                MajorGridlineStyle = LineStyle.Dash,
                Position = AxisPosition.Bottom
            };
            LinearAxis measurementYAxis = new LinearAxis 
            { 
                Title = "Case Count",
                MajorGridlineStyle = LineStyle.Dash,
                Position = AxisPosition.Left
            };
            model.Axes.Add(measurementXAxis);
            model.Axes.Add(measurementYAxis);
            
            ModelDataOverlay = model;

            /* ---------- Derivative ---------- */
            var measurementDerivativeScatter = new ScatterSeries() { MarkerType = MarkerType.Circle, MarkerFill = OxyColors.DodgerBlue };
            double[] measurementDerivative = Utilities.ComputeDerivative(_timeData, _caseData);
            measurementDerivativeScatter.Points.AddRange(_timeData.Zip(measurementDerivative, (x, y) => new ScatterPoint(x, y)));

            // Easier than finding the analytic derivative of a Gauss function, and keeps methods the same
            double[] modelDerivative = Utilities.ComputeDerivative(xModel.ToArray(), yModel.ToArray());
            var modelDerivativeSeries = new LineSeries { Color = OxyColors.Red };
            modelDerivativeSeries.Points.AddRange(xModel.Zip(modelDerivative, (x, y) => new DataPoint(x, y)));

            var derivativeModel = new PlotModel();
            derivativeModel.Series.Add(measurementDerivativeScatter);
            derivativeModel.Series.Add(modelDerivativeSeries);
            LinearAxis derivativeXAxis = new LinearAxis
            {
                Title = "Days Since Reference",
                MajorGridlineStyle = LineStyle.Dash,
                Position = AxisPosition.Bottom
            };
            LinearAxis derivativeYAxis = new LinearAxis
            {
                Title = "Daily Case Rate",
                MajorGridlineStyle = LineStyle.Dash,
                Position = AxisPosition.Left
            };
            derivativeModel.Axes.Add(derivativeXAxis);
            derivativeModel.Axes.Add(derivativeYAxis);
            ModelDataDerivativeOverlay = derivativeModel;
        }

        private double[] FitModel()
        {
            double[,] xData = new double[_timeData.Length, 2];
            for (int i = 0; i < _timeData.Length; i++)
            {
                xData[i, 0] = _timeData[i];
            }

            // Lower and upper bounds are in order peak case count, day on which peak occurs, and rate factor
            double[] lowerBounds = { _caseData.Max(), _timeData[0], 0.5 };
            double[] upperBounds = { 5e6, _timeData.Last() + 60, 50.0 };
            double epsx = 0.000001;
            int maxits = 0;
            int info;
            alglib.lsfitstate state;
            alglib.lsfitreport rep;
            double diffstep = 0.0001;
            double[] newCoefficients;

            // Guess / seed values will be current user values 
            alglib.lsfitcreatef(xData, _caseData, new[] { PeakActiveCaseCount, DaysSinceReferenceForPeak, RateFactor }, diffstep, out state);
            alglib.lsfitsetbc(state, lowerBounds, upperBounds);
            alglib.lsfitsetcond(state, epsx, maxits);
            alglib.lsfitfit(state, Gauss.EvaluateWrapper, null, null);
            alglib.lsfitresults(state, out info, out newCoefficients, out rep);

            return newCoefficients;
        }
    }
}
