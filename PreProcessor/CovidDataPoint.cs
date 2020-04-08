using System;
using System.Collections.Generic;
using System.Text;

namespace PreProcessor
{
    public class CovidDataPoint
    {
        public DateTime UpdateTime { get; set; }

        public string Country { get; set; }

        public int Confirmed { get; set; }

        public int Deaths { get; set; }

        public int Recoveries { get; set; }

        public int Active { get => Confirmed - Deaths - Recoveries; }
    }

    public class CountyDataPoint : StateDataPoint
    {
        public string County { get; set; }
    }

    public class StateDataPoint : CovidDataPoint
    {
        public string State { get; set; }
    }
}
