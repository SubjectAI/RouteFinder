 using System.Collections.Generic;

namespace RouteFinder
{
    /// <summary>
    /// Enumerates the available transport modes.
    /// </summary>
    public enum TransportMode
    {
        DieselTrain,
        ElectricTrain,
        DieselTruck,
        ElectricTruck
    }

    /// <summary>
    /// Specifies capital and operating cost parameters for a transport mode.
    /// </summary>
    public class ModeSpecs
    {
        /// <summary>
        /// The transport mode.
        /// </summary>
        public TransportMode Mode { get; set; }

        /// <summary>
        /// Capital expenditure per region in million USD (rail build cost or 0 for trucks).
        /// </summary>
        public double CapExPerRegion_MillionUSD { get; set; }

        /// <summary>
        /// Operating expenditure per ton in USD.
        /// </summary>
        public double OpExPerTon_USD { get; set; }

        /// <summary>
        /// Whether this mode requires rail electrification.
        /// </summary>
        public bool RequiresElectrification { get; set; }

        /// <summary>
        /// Handling or mode-change fee per ton in USD.
        /// </summary>
        public double ModeChangeFee_USD { get; set; }
    }

    /// <summary>
    /// Holds the specification map for each available transport mode.
    /// </summary>
    public static class TransportModels
    {
        public static readonly Dictionary<TransportMode, ModeSpecs> ModeSpecsMap =
            new Dictionary<TransportMode, ModeSpecs>
        {
            [TransportMode.DieselTrain] = new ModeSpecs
            {
                Mode = TransportMode.DieselTrain,
                CapExPerRegion_MillionUSD = 400.0,
                OpExPerTon_USD = 50.0,
                RequiresElectrification = false,
                ModeChangeFee_USD = 50.0
            },
            [TransportMode.ElectricTrain] = new ModeSpecs
            {
                Mode = TransportMode.ElectricTrain,
                CapExPerRegion_MillionUSD = 800.0,
                OpExPerTon_USD = 20.0,
                RequiresElectrification = true,
                ModeChangeFee_USD = 50.0
            },
            [TransportMode.DieselTruck] = new ModeSpecs
            {
                Mode = TransportMode.DieselTruck,
                CapExPerRegion_MillionUSD = 0.0,
                OpExPerTon_USD = 300.0,
                RequiresElectrification = false,
                ModeChangeFee_USD = 50.0
            },
            [TransportMode.ElectricTruck] = new ModeSpecs
            {
                Mode = TransportMode.ElectricTruck,
                CapExPerRegion_MillionUSD = 0.0,
                OpExPerTon_USD = 200.0,
                RequiresElectrification = true,
                ModeChangeFee_USD = 50.0
            }
        };
    }
}