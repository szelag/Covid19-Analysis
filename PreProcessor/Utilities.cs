using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PreProcessor
{
    internal static class Utilities
    {
        internal static double[] ComputeDerivative(double[] timeData, double[] measurementData)
        {
            double[] dataOut = new double[timeData.Length];

            for (int i = 1; i < timeData.Length; i++)
            {
                dataOut[i] = (measurementData[i] - measurementData[i - 1]) / (timeData[i] - timeData[i - 1]);
            }
            dataOut[0] = dataOut[1];

            return dataOut;
        }
    }
}
