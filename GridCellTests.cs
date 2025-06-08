using Xunit;
using RouteFinder;

namespace RouteFinder.Tests
{
    public class GridCellTests
    {
        [Fact]
        public void StandardRailBuildCost_MountainousRegion_Returns400()
        {
            var cell = new GridCell { IsMountain = true };
            Assert.Equal(400.0, cell.StandardRailBuildCost);
        }

        [Fact]
        public void StandardRailBuildCost_RainforestRegion_Returns250()
        {
            var cell = new GridCell { IsRainforest = true };
            Assert.Equal(250.0, cell.StandardRailBuildCost);
        }

        [Fact]
        public void RoadBuildCost_MountainousRegion_Returns200()
        {
            var cell = new GridCell { IsMountain = true };
            Assert.Equal(200.0, cell.RoadBuildCost);
        }

        [Fact]
        public void SpoilageLossPerTon_RiskyRegion_Calculates3Percent()
        {
            var cell = new GridCell { IsRisky = true };
            double price = 1000.0;
            Assert.Equal(30.0, cell.SpoilageLossPerTon(price));
        }

        [Fact]
        public void SecuritySurchargeRate_RiskyRegion_Returns0Point15()
        {
            var cell = new GridCell { IsRisky = true };
            Assert.Equal(0.15, cell.SecuritySurchargeRate);
        }

        [Fact]
        public void Cost_Composition_IncludesAllModifiers()
        {
            var cell = new GridCell
            {
                IsRainforest = true,
                IsMountain   = true,
                IsRisky      = true
            };
            // Base(1) + regulatory(2) + rainforest(8) + mountain(3) + risky(4) = 18
            Assert.Equal(18.0, cell.Cost);
        }
    }
}