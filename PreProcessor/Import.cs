using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PreProcessor
{
    internal static class Import
    {
        internal static IEnumerable<CovidDataPoint> SummarizeNational(List<StateDataPoint> thisDayStateData)
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

        internal static IEnumerable<StateDataPoint> SummarizeByState(IEnumerable<CountyDataPoint> thisDayCountyData)
        {
            /* Developer note: Found a surprisingly big chunk of speed here by using a couple CountyDataPoint arrays
               rather than just using the input IEnumerable all the way through. */

            CountyDataPoint[] dataArr = thisDayCountyData.ToArray();
            DateTime thisDate = dataArr.First().UpdateTime;

            foreach (string country in dataArr.Select(data => data.Country).Distinct())
            {
                foreach (string state in dataArr
                    .Where(data => data.Country == country)
                    .Select(data => data.State)
                    .Distinct())
                {
                    CountyDataPoint[] relevantData = dataArr.Where(data => data.Country == country && data.State == state).ToArray();

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

        internal static IEnumerable<StateDataPoint> ParseAsStateData(IEnumerable<string> allLines)
        {
            // Headers are:
            // Province/State,Country/Region,Last Update,Confirmed,Deaths,Recovered,Latitude,Longitude
            foreach (string line in allLines)
            {
                /* In some early data there's a trickle of entries that start to get added that are like
                   "Boston, MA" etc. That messes with this CSV parsing, so I decide to skip it entirely. */
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

        internal static IEnumerable<CountyDataPoint> ParseAsCountyData(IEnumerable<string> allLines)
        {
            // Headers are:
            // FIPS,Admin2,Province_State,Country_Region,Last_Update,Lat,Long_,Confirmed,Deaths,Recovered,Active,Combined_Key
            foreach (string line in allLines)
            {
                string[] split = line.Split(',');

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

        private static DateTime DateParser(string dateString)
        {
            /* The Johns Hopkins data uses a variety of date/time formats (whyyyy..??), so
               this helper method was needed to sort them out. Was about time I learned how to use
               a Regex... */

            Match monthDayYearLong = new Regex(@"\d{1,2}\/\d{1,2}\/\d{4}").Match(dateString);
            Match monthDayYearShort = new Regex(@"\d{1,2}\/\d{1,2}\/\d{2}\s").Match(dateString);
            Match yearMonthDay = new Regex(@"\d{4}-\d{2}-\d{2}").Match(dateString);

            // Arbitrary values for initialization
            int year = 1970;
            int month = 1;
            int day = 1;

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
