﻿using GalaSoft.MvvmLight.Command;
using OxyPlot;
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

        public ModelFitViewModel(double[] timeData, double[] caseData)
        {
            _timeData = timeData;
            _caseData = caseData;

            var pairedData = _timeData.Zip(_caseData, (time, cases) => new { Days = time, CaseCount = cases });

            _peakActiveCaseCount = _caseData.Max();
            _daysSinceReferenceForPeak = pairedData.First(data => data.CaseCount == _caseData.Max()).Days;
            _rateFactor = 15;
            _extendModelDays = 5;

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