using System; 
using System.Collections.Generic;

namespace RouteFinder
{
    /// <summary>
    /// Represents the annual cash flow results for a given year.
    /// </summary>
    public class CashFlow
    {
        public int Year { get; set; }
        public double Revenue { get; set; }
        public double MineOpex { get; set; }
        public double TransportOpex { get; set; }
        public double TrainCapEx { get; set; }
        public double InsuranceCost { get; set; }
        public double NetProfit { get; set; }
    }

    /// <summary>
    /// Builds a multi-year financial projection given transport and production parameters.
    /// </summary>
    public static class FinancialModel
    {
        /// <summary>
        /// Generates cash flows for each year in the production schedule.
        /// </summary>
        /// <param name="perTonTransportCost">Calculated transport cost per ton (USD).</param>
        /// <param name="mineCostPerTon">Mining operating cost per ton (USD).</param>
        /// <param name="trainCapExPerUnit">Capital cost per train unit (USD).</param>
        /// <param name="schedule">Array of yearly production targets and expected prices.</param>
        /// <param name="useInsurance">Whether to include insurance at 1% of revenue.</param>
        /// <returns>An enumerable of CashFlow objects for each year.</returns>
        public static IEnumerable<CashFlow> RunProjection(
            double perTonTransportCost,
            double mineCostPerTon,
            double trainCapExPerUnit,
            ProductionSchedule[] schedule,
            bool useInsurance = true)
        {
            double trainsOwned = 0.0;
            // Capacity per diesel train = 12,000 tons/week * 52 weeks
            const double TonsPerTrainPerYear = 12000.0 * 52.0;

            foreach (var year in schedule)
            {
                // Determine how many trains are required this year
                double trainsNeeded = Math.Ceiling(year.TonsToProduce / TonsPerTrainPerYear);
                double buyTrains = Math.Max(0.0, trainsNeeded - trainsOwned);
                double capEx = buyTrains * trainCapExPerUnit;
                trainsOwned += buyTrains;

                double mineOpex = year.TonsToProduce * mineCostPerTon;
                double transportOpex = year.TonsToProduce * perTonTransportCost;
                double revenue = year.TonsToProduce * year.ExpectedPrice;
                double insuranceCost = useInsurance ? 0.01 * revenue : 0.0;
                double netProfit = revenue - (mineOpex + transportOpex + capEx + insuranceCost);

                yield return new CashFlow
                {
                    Year = year.Year,
                    Revenue = revenue,
                    MineOpex = mineOpex,
                    TransportOpex = transportOpex,
                    TrainCapEx = capEx,
                    InsuranceCost = insuranceCost,
                    NetProfit = netProfit
                };
            }
        }
    }
}