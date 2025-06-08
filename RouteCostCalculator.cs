 using System;
using System.Collections.Generic;
using System.Linq;

namespace RouteFinder
{
    /// <summary>
    /// Calculates per‐ton transport cost (CapEx, OpEx, spoilage, handling) for a given route and mode.
    /// </summary>
    public static class RouteCostCalculator
    {
        // Total graphite shipped over the project horizon (2026–2030) in tons.
        private const double TotalTonnageHorizon = 20.25e6;

        /// <summary>
        /// Computes the transport cost per ton for the specified route and mode.
        /// </summary>
        /// <param name="path">Ordered list of grid cells on the route.</param>
        /// <param name="mode">Transport mode (diesel train, electric train, diesel truck, etc.).</param>
        /// <param name="expectedPricePerTon">Projected revenue per ton, used for spoilage calculation.</param>
        /// <param name="useInsurance">
        /// If true, spoilage is covered by insurance and no per‐ton spoilage penalty applies.
        /// If false, spoilage cost (3% of price) applies for each risky region.
        /// </param>
        /// <returns>Total transport cost per ton (USD).</returns>
        public static double ComputePerTonCost(
            IReadOnlyList<GridCell> path,
            TransportMode mode,
            double expectedPricePerTon,
            bool useInsurance = false,
            bool useSecurity = false)
        {
            // Retrieve per‐region CapEx/OpEx specs for this mode
            var specs = TransportModels.ModeSpecsMap[mode];

            // Compute region-specific CapEx total (USD) based on mode and cell build costs
            double totalCapExUsd;
            switch (mode)
            {
                case TransportMode.DieselTrain:
                    totalCapExUsd = path.Sum(cell => cell.StandardRailBuildCost * 1_000_000.0);
                    break;
                case TransportMode.ElectricTrain:
                    totalCapExUsd = path.Sum(cell => cell.ElectricRailBuildCost * 1_000_000.0);
                    break;
                case TransportMode.DieselTruck:
                case TransportMode.ElectricTruck:
                    totalCapExUsd = path.Sum(cell => cell.RoadBuildCost * 1_000_000.0);
                    break;
                default:
                    totalCapExUsd = 0.0;
                    break;
            }

            // Amortized CapEx per ton
            double capExPerTon = totalCapExUsd / TotalTonnageHorizon;

            // Operating cost per ton
            double opExPerTon = specs.OpExPerTon_USD;

            double spoilagePerTon = 0.0;
            double securitySurchargePerTon = 0.0;

            if (useInsurance)
            {
                // Insurance covers losses: no spoilage or surcharge here.
            }
            else if (useSecurity)
            {
                // Private security: add 15% OpEx surcharge for each risky region
                securitySurchargePerTon = path.Sum(cell => cell.SecuritySurchargeRate * opExPerTon);
            }
            else
            {
                // No mitigation: apply 3% spoilage for each risky region
                spoilagePerTon = path.Sum(cell => cell.SpoilageLossPerTon(expectedPricePerTon));
            }

            // Apply handling fee ($50/ton) only if the route mixes road and rail segments
            bool hasRoad = path.Any(cell => cell.RoadBuildCost > 0);
            bool hasRail = path.Any(cell => cell.StandardRailBuildCost > 0 || cell.ElectricRailBuildCost > 0);
            double handlingFeePerTon = (hasRoad && hasRail) ? specs.ModeChangeFee_USD : 0.0;

            // Total per‐ton transport cost
            return capExPerTon
                + opExPerTon
                + spoilagePerTon
                + securitySurchargePerTon
                + handlingFeePerTon;
        }
    }
}