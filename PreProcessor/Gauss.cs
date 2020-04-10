using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PreProcessor
{
    internal static class Gauss
    {
        internal static double Evaluate(double x, double a, double b, double c)
        {
            var v1 = (x - b);
            var v2 = (v1 * v1) / (2 * (c * c));
            var v3 = a * Math.Exp(-v2);
            return v3;
        }

        internal static void EvaluateWrapper(double[] c, double[] x, ref double y, object obj)
        {
            y = Evaluate(x[0], c[0], c[1], c[2]);
        }
    }
}
