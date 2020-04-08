using System;
using System.Collections.Generic;
using System.Text;

namespace PreProcessor
{
    internal class CovidDataPoint
    {
        public DateTime UpdateTime { get; set; }

        public string Country { get; set; }

        public int Confirmed { get; set; }

        public int Deaths { get; set; }

        public int Recoveries { get; set; }
    }

    internal class CountyDataPoint : StateDataPoint
    {
        public string County { get; set; }
    }

    internal class StateDataPoint : CovidDataPoint
    {
        public string State { get; set; }
    }
}
