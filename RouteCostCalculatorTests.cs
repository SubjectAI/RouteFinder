using Xunit;
using System.Collections.Generic;
using RouteFinder;

namespace RouteFinder.Tests
{
    public class RouteCostCalculatorTests
    {
        private const double Tolerance = 1e-6;

        [Fact]
        public void ComputePerTonCost_DieselTrain_NoRisk_CorrectValue()
        {
            var cell = new GridCell { IsMountain = true };
            var path = new List<GridCell> { cell };
            double price = 4700.0;

            double cost = RouteCostCalculator.ComputePerTonCost(
                path, TransportMode.DieselTrain, price, useInsurance: false, useSecurity: false);

            double expectedCapEx = 400_000_000.0 / 20.25e6;
            double expected = expectedCapEx + 50.0;
            Assert.InRange(cost, expected - Tolerance, expected + Tolerance);
        }

        [Fact]
        public void ComputePerTonCost_DieselTrain_WithSpoilage_IncludesSpoilage()
        {
            var cell = new GridCell { IsMountain = true, IsRisky = true };
            var path = new List<GridCell> { cell };
            double price = 1000.0;

            double cost = RouteCostCalculator.ComputePerTonCost(
                path, TransportMode.DieselTrain, price, useInsurance: false, useSecurity: false);

            double capEx = 400_000_000.0 / 20.25e6;
            double spoilage = 0.03 * price;
            double expected = capEx + 50.0 + spoilage;
            Assert.InRange(cost, expected - Tolerance, expected + Tolerance);
        }

        [Fact]
        public void ComputePerTonCost_DieselTrain_WithInsurance_NoSpoilage()
        {
            var cell = new GridCell { IsMountain = true, IsRisky = true };
            var path = new List<GridCell> { cell };
            double price = 1000.0;

            double cost = RouteCostCalculator.ComputePerTonCost(
                path, TransportMode.DieselTrain, price, useInsurance: true, useSecurity: false);

            double capEx = 400_000_000.0 / 20.25e6;
            double expected = capEx + 50.0;
            Assert.InRange(cost, expected - Tolerance, expected + Tolerance);
        }

        [Fact]
        public void ComputePerTonCost_DieselTrain_WithSecurity_NoSpoilage_ButSurcharge()
        {
            var cell = new GridCell { IsMountain = true, IsRisky = true };
            var path = new List<GridCell> { cell };
            double price = 1000.0;

            double cost = RouteCostCalculator.ComputePerTonCost(
                path, TransportMode.DieselTrain, price, useInsurance: false, useSecurity: true);

            double capEx    = 400_000_000.0 / 20.25e6;
            double surcharge = 0.15 * 50.0;
            double expected = capEx + 50.0 + surcharge;
            Assert.InRange(cost, expected - Tolerance, expected + Tolerance);
        }
    }
}