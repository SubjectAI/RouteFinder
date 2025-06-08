 namespace RouteFinder
{
    /// <summary>
    /// Represents the annual production target and expected price per ton.
    /// </summary>
    public class ProductionSchedule
    {
        /// <summary>
        /// Calendar year of production.
        /// </summary>
        public int Year { get; set; }

        /// <summary>
        /// Tons of graphite to produce and ship in the given year.
        /// </summary>
        public double TonsToProduce { get; set; }

        /// <summary>
        /// Expected market price per ton (USD) for that year.
        /// </summary>
        public double ExpectedPrice { get; set; }
    }
}