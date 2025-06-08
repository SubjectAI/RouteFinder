using System.Collections.Generic;

namespace RouteFinder
{
    /// <summary>
    /// Represents one square in the grid. Each cell has (Row,Col), three terrain flags,
    /// a computed scalar Cost, plus adjacency list & A* fields.
    /// </summary>
    public class GridCell
    {
        public int Row { get; set; }
        public int Col { get; set; }

        // Terrain flags
        public bool IsRainforest { get; set; }
        public bool IsMountain   { get; set; }
        public bool IsRisky      { get; set; }
        
        public bool IsUrban      { get; set;  }

        /// <summary>
        /// Standard rail build cost in million USD for this region.
        /// </summary>
        public double StandardRailBuildCost
        {
            get
            {
                if (IsRainforest) return 250;
                if (IsMountain)   return 400;
                if (IsUrban)      return 100;
                return 0;
            }
        }

        /// <summary>
        /// Electric rail build cost (double the standard rail cost) in million USD.
        /// </summary>
        public double ElectricRailBuildCost => StandardRailBuildCost * 2;

        /// <summary>
        /// Road build cost in million USD for this region (only applies to mountainous regions).
        /// </summary>
        public double RoadBuildCost
        {
            get
            {
                if (IsMountain) return 200;
                return 0;
            }
        }

        /// <summary>
        /// Spoilage loss per ton (USD) in this region due to risk (3% of price per ton).
        /// </summary>
        public double SpoilageLossPerTon(double pricePerTon)
        {
            return IsRisky ? 0.03 * pricePerTon : 0.0;
        }

        /// <summary>
        /// Security surcharge rate (fraction) for high‐security in risky regions (15%).
        /// </summary>
        public double SecuritySurchargeRate => IsRisky ? 0.15 : 0.0;

        /// <summary>
        /// Composite cost reflecting base transit, regulatory delays (+2),
        /// rainforest damage (+8), mountain costs (+3), and risky-region penalties (+4).
        /// </summary>
        public double Cost
        {
            get
            {
                // Base transit unit
                double c = 1.0;

                // Environmental/regulatory delay penalty (per region)
                c += 2.0;

                // Deforestation penalty
                if (IsRainforest) c += 8.0;

                // Mountainous construction & fuel penalty
                if (IsMountain)   c += 3.0;

                // Risky terrain spoilage/security penalty
                if (IsRisky)      c += 4.0;

                return c;
            }
        }

        /// <summary>
        /// Adjacency list: neighbors reachable from this cell + cost to move there.
        /// </summary>
        public List<Neighbor> Neighbors { get; } = new List<Neighbor>();

        /// <summary>
        /// For A*: best known cost to reach this cell (g‐score).
        /// </summary>
        public double GScore { get; set; } = double.PositiveInfinity;

        /// <summary>
        /// For A*: f‐score = GScore + heuristic.
        /// </summary>
        public double FScore { get; set; } = double.PositiveInfinity;

        /// <summary>
        /// For path reconstruction: which cell we came from.
        /// </summary>
        public GridCell CameFrom { get; set; } = null;

        /// <summary>
        /// A human‐readable label (“A”, “B”, …, “BZ”) so we can print paths.
        /// </summary>
        public string Label { get; set; }

        public override string ToString()
        {
            return Label ?? $"({Row},{Col})";
        }
    }

    /// <summary>
    /// A small helper pairing a GridCell with the cost to move into it.
    /// </summary>
    public class Neighbor
    {
        public GridCell Cell { get; set; }
        public double MoveCost { get; set; }

        public Neighbor(GridCell cell, double moveCost)
        {
            Cell = cell;
            MoveCost = moveCost;
        }
    }
}