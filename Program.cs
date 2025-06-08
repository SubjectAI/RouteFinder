using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace RouteFinder
{
    class Program
    {
        // Grid dimensions (set at runtime)
        private static int N;
        private static int M;

        // The map of region labels, defined in Main
        private static string[,] labels;

        // A 2D array holding every GridCell
        private static GridCell?[,] grid;

        // A dictionary mapping string labels to the GridCell object
        private static Dictionary<string, GridCell> labelMap;

        // Total graphite shipped over the project horizon (tons)
        private const double TotalTonnageHorizon = 20.25e6;

        // Lock for synchronizing adjacency rebuilds and A* searches
        private static readonly object adjacencyLock = new object();

        static void Main(string[] args)
        {
            // ─── Load grid labels and dimensions ───
            labels = new string[,]
            {
                // Col=0  Col=1  Col=2  Col=3  Col=4  Col=5  Col=6  Col=7 Col=8 Col=9
                { "A",    "B",    "C",    "D",   "",    "",   "",    "",   "",   ""},
                { "E",    "F",    "G",    "H",    "I",    "",    "",    "", "", "" },
                { "J",    "K",    "L",    "M",    "N",    "O",    "P",    "", "", "" },
                { "Q",    "R",    "S",   "T",   "U",   "V",   "W",   "X", "Y", "Z" },
                { "AA",   "AB",   "AC",   "AD",   "AE",   "AF",   "AG",   "AH" , "AI", "AK" },
                { "AL",   "AM",   "AN",   "AO",   "AP",   "AQ",   "AR", "AS", "AT", "AU"},
                { "",   "AV",   "AW",   "AX",   "AY",   "AZ",   "BA",   "BB", "BC", "BD" },
                { "",   "",   "BE",   "BF",   "BG",   "BH",   "BI", "BJ", "BK", "BL" },
                { "",   "",   "BM",   "BN",   "BO",   "BP",   "BQ", "BR", "BS", "" },
                { "",   "",   "BT",   "BU",   "BV",   "BW",   "BX", "BY", "BZ", "" },
            };
            N = labels.GetLength(0);
            M = labels.GetLength(1);
            grid = new GridCell?[N, M];
            labelMap = new Dictionary<string, GridCell>();

            // 1) Build and annotate the grid for routing
            InitializeGrid();
            AnnotateTerrain();
            BuildAdjacency();

            // Price scenarios (price per ton, probability)
            var priceScenarios = new []
            {
                (Price: 2200.0, Prob: 0.15),
                (Price: 4700.0, Prob: 0.60),
                (Price: 6300.0, Prob: 0.25)
            };

            // Ports to evaluate: Cell + one-time upgrade cost
            var ports = new []
            {
                (Name: "Rio", Cell: labelMap["BS"], UpgradeCost: 0.0),
                (Name: "Luis", Cell: labelMap["Y"],  UpgradeCost: 2_000_000_000.0)
            };

            var modes = new TransportMode[]
            {
                TransportMode.DieselTrain,
                TransportMode.ElectricTrain,
                TransportMode.DieselTruck,
                TransportMode.ElectricTruck
            };

            // 4) Define the 2026–2030 production schedule
            var schedule = new ProductionSchedule[]
            {
                new ProductionSchedule { Year = 2026, TonsToProduce = 3.05e6, ExpectedPrice = 4700 },
                new ProductionSchedule { Year = 2027, TonsToProduce = 3.55e6, ExpectedPrice = 4700 },
                new ProductionSchedule { Year = 2028, TonsToProduce = 4.05e6, ExpectedPrice = 4700 },
                new ProductionSchedule { Year = 2029, TonsToProduce = 4.55e6, ExpectedPrice = 4700 },
                new ProductionSchedule { Year = 2030, TonsToProduce = 5.05e6, ExpectedPrice = 4700 }
            };

            // ESG/reforestation budget (USD) per year
            const double AnnualESGBudget = 50_000_000.0;
            // Annual maintenance rate for built infrastructure
            const double MaintenanceRate = 0.03;  // 3% per year
            // Asset lifetimes (years) for straight‐line depreciation
            const double InfraLifetimeYears = 20.0;
            const double VehicleLifetimeYears = 10.0;
            // Discount rate for NPV calculation
            const double DiscountRate = 0.08;

            var start = labelMap["AM"];

            // Prepare storage for expected-value profits per (mode, port, riskOption)
            var evProfit = new ConcurrentDictionary<(TransportMode mode, string port, string risk), double>();
            var riskOptions = new[]
            {
                (Name: "Insurance",      UseInsurance: true,  UseSecurity: false),
                (Name: "PrivateSecurity",UseInsurance: false, UseSecurity: true)
            };
            foreach (var mode in modes)
                foreach (var (portName, _, _) in ports)
                    foreach (var r in riskOptions)
                        evProfit[(mode, portName, r.Name)] = 0.0;

            foreach (var scenario in priceScenarios)
            {
                var price = scenario.Price;
                var prob  = scenario.Prob;
                Console.WriteLine($"\n=== Price Scenario: ${price}/ton (Prob {prob:P0}) ===");
                foreach (var (portName, portCell, upgradeCost) in ports)
                {
                    Console.WriteLine($"\n-- Port: {portName} (Upgrade {upgradeCost:C}) --");
                    double portUpgradePerTon = upgradeCost / TotalTonnageHorizon;
                    Console.WriteLine($"Port upgrade adds {portUpgradePerTon:C}/ton");

                    foreach (var mode in modes)
                    {
                        foreach (var r in riskOptions)
                        {
                            List<GridCell> path;
                            lock (adjacencyLock)
                            {
                                // Rebuild weighted adjacency with appropriate risk-mitigation
                                BuildWeightedAdjacency(mode, expectedPricePerTon: price, useInsurance: r.UseInsurance, useSecurity: r.UseSecurity);
                                path = RoutePlanner.FindOptimalRoute(grid, start, portCell);
                            }
                            if (path == null)
                            {
                                Console.WriteLine($"{mode} + {r.Name}: No valid route.");
                                continue;
                            }

                            // Compute total infrastructure CapEx (USD) along this path
                            double infraCapExUsd = mode switch
                            {
                                TransportMode.DieselTrain   => path.Sum(cell => cell.StandardRailBuildCost * 1_000_000.0),
                                TransportMode.ElectricTrain => path.Sum(cell => cell.ElectricRailBuildCost * 1_000_000.0),
                                _                            => path.Sum(cell => cell.RoadBuildCost * 1_000_000.0)
                            };

                            // Compute per-ton cost with this risk-mitigation
                            double perTonBaseCost = RouteCostCalculator.ComputePerTonCost(
                                path, mode, expectedPricePerTon: price,
                                useInsurance: r.UseInsurance,
                                useSecurity: r.UseSecurity);
                            double perTonCost = perTonBaseCost + portUpgradePerTon;

                            // Prepare dynamic mine costs per year
                            var dynamicMineCosts = schedule.Select(y =>
                                y.TonsToProduce < 0.5e6 ? 1000.0 :
                                y.TonsToProduce < 14e6  ? 800.0 : 400.0
                            ).ToArray();

                            // Run projection with dynamic mine costs
                            double totalProfit = 0.0;
                            double trainsOwned = 0.0;
                            const double TonsPerTrainPerYear = 12000.0 * 52.0;
                            for (int i = 0; i < schedule.Length; i++)
                            {
                                var year = schedule[i];
                                double mineCostPerTon = dynamicMineCosts[i];

                                // Train CapEx logic (same as before)
                                double trainsNeeded = Math.Ceiling(year.TonsToProduce / TonsPerTrainPerYear);
                                double buy = Math.Max(0.0, trainsNeeded - trainsOwned);
                                double capEx = buy * 15_000_000.0;
                                trainsOwned += buy;

                                double mineOpex = year.TonsToProduce * mineCostPerTon;
                                double transportOpex = year.TonsToProduce * perTonCost;
                                double revenue = year.TonsToProduce * price;
                                double insuranceCost = r.UseInsurance ? 0.01 * revenue : 0.0;
                                double esgCost = AnnualESGBudget;
                                double maintenanceCost = MaintenanceRate * infraCapExUsd;
                                double depreciationInfra = infraCapExUsd / InfraLifetimeYears;
                                double depreciationVehicles = capEx / VehicleLifetimeYears;
                                double depreciationCost = depreciationInfra + depreciationVehicles;
                                double net = revenue - (mineOpex + transportOpex + capEx + insuranceCost + esgCost + maintenanceCost + depreciationCost);
                                // Discount this year’s net back to 2025
                                int yearOffset = schedule[i].Year - 2025;
                                totalProfit += net / Math.Pow(1 + DiscountRate, yearOffset);
                            }

                            Console.WriteLine($"{mode} + {r.Name}: Profit = {totalProfit:C} over 5 years");

                            // Accumulate probability-weighted profit (thread-safe)
                            evProfit.AddOrUpdate(
                                (mode, portName, r.Name),
                                totalProfit * prob,
                                (_, old) => old + totalProfit * prob);
                        }
                    }
                }
            }

            // Compute and display the expected-value results
            Console.WriteLine("\n=== Expected-Value Profits by Mode, Port & Risk Option ===");
            foreach (var kv in evProfit)
            {
                Console.WriteLine($"{kv.Key.mode} to {kv.Key.port} ({kv.Key.risk}): EV Profit = {kv.Value:C}");
            }
            // Find the maximum EV
            var bestEv = evProfit.OrderByDescending(kv => kv.Value).First();
            Console.WriteLine($"\n=== Optimal by Expected Value ===\n{bestEv.Key.mode} to {bestEv.Key.port} ({bestEv.Key.risk}) ⇒ EV Profit {bestEv.Value:C}");

            // === Detailed recommendation for the EV-optimal combination ===
            var bestKey = bestEv.Key;
            var bestMode = bestKey.mode;
            var bestPortName = bestKey.port;
            var bestRisk = bestKey.risk;
            Console.WriteLine("\n=== Detailed Recommendation ===");
            Console.WriteLine($"Mode: {bestMode}");
            Console.WriteLine($"Port: {bestPortName}");
            Console.WriteLine($"Risk Mitigation: {bestRisk}");
            Console.WriteLine($"Expected-Value Profit: {bestEv.Value:C}");

            // Determine goal cell and flags
            var bestPort = ports.First(p => p.Name == bestPortName);
            bool useIns = bestRisk == "Insurance";
            bool useSec = bestRisk == "PrivateSecurity";

            // Rebuild adjacency for the base-case price scenario (most likely price = 4700)
            double basePrice = 4700.0;
            List<GridCell> detailedPath;
            lock (adjacencyLock)
            {
                BuildWeightedAdjacency(bestMode, expectedPricePerTon: basePrice, useInsurance: useIns, useSecurity: useSec);
                detailedPath = RoutePlanner.FindOptimalRoute(grid, start, bestPort.Cell);
            }
            Console.WriteLine("Recommended Route: " + string.Join(" → ", detailedPath.Select(c => c.Label)));

            // Compute total infrastructure CapEx (USD) along this path
            double recInfraCapExUsd = bestMode switch
            {
                TransportMode.DieselTrain   => detailedPath.Sum(cell => cell.StandardRailBuildCost * 1_000_000.0),
                TransportMode.ElectricTrain => detailedPath.Sum(cell => cell.ElectricRailBuildCost * 1_000_000.0),
                _                            => detailedPath.Sum(cell => cell.RoadBuildCost * 1_000_000.0)
            };

            // Compute per-ton cost
            double basePerTon = RouteCostCalculator.ComputePerTonCost(
                detailedPath, bestMode, expectedPricePerTon: basePrice, useInsurance: useIns, useSecurity: useSec);
            double upgradePerTon = bestPort.UpgradeCost / TotalTonnageHorizon;
            double totalPerTon = basePerTon + upgradePerTon;
            Console.WriteLine($"Transport cost per ton at ${basePrice}/ton: {totalPerTon:C}");

            // Prepare dynamic mine costs per year
            var recMineCosts = schedule.Select(y =>
                y.TonsToProduce < 0.5e6 ? 1000.0 :
                y.TonsToProduce < 14e6  ? 800.0 : 400.0
            ).ToArray();

            // Recalculate 5-year profit at base-case price
            double totalProfitBase = 0.0;
            double recTrainsOwned = 0.0;
            const double recTonsPerTrainPerYear = 12000.0 * 52.0;
            for (int i = 0; i < schedule.Length; i++)
            {
                var year = schedule[i];
                double mineCostPerTon = recMineCosts[i];
                double trainsNeeded = Math.Ceiling(year.TonsToProduce / recTonsPerTrainPerYear);
                double buy = Math.Max(0.0, trainsNeeded - recTrainsOwned);
                double capEx = buy * 15_000_000.0;
                recTrainsOwned += buy;

                double mineOpex = year.TonsToProduce * mineCostPerTon;
                double transportOpex = year.TonsToProduce * totalPerTon;
                double revenue = year.TonsToProduce * basePrice;
                double insuranceCost = useIns ? 0.01 * revenue : 0.0;
                double esgCost = AnnualESGBudget;
                double maintenanceCost = MaintenanceRate * recInfraCapExUsd;
                double depreciationInfra = recInfraCapExUsd / InfraLifetimeYears;
                double depreciationVehicles = capEx / VehicleLifetimeYears;
                double depreciationCost = depreciationInfra + depreciationVehicles;
                double net = revenue - (mineOpex + transportOpex + capEx + insuranceCost + esgCost + maintenanceCost + depreciationCost);
                // Discount this year’s net back to 2025
                int recYearOffset = schedule[i].Year - 2025;
                totalProfitBase += net / Math.Pow(1 + DiscountRate, recYearOffset);
            }
            Console.WriteLine($"5-year profit at ${basePrice}/ton: {totalProfitBase:C}");

            // ─── 2.1 Parameter Sweep: Profit vs Price & Year1 Demand ───
            Console.WriteLine("\n=== Sensitivity: Profit vs Price & Year1 Demand ===");
            // Define price range from 2200 to 6300 in steps of 200
            var priceRange = Enumerable.Range(0, ((6300 - 2200) / 200) + 1)
                .Select(i => 2200.0 + i * 200.0)
                .ToList();
            // Use the distinct Year1 demand values from the schedule
            var demandRange = schedule.Select(y => y.TonsToProduce).Distinct().ToList();

            // Store original first-year demand
            double originalFirstYear = schedule[0].TonsToProduce;

            foreach (var pr in priceRange)
            {
                foreach (var d in demandRange)
                {
                    // Temporarily set Year1 demand
                    schedule[0].TonsToProduce = d;

                    // Rebuild adjacency & compute route and per-ton cost
                    BuildWeightedAdjacency(bestMode, pr, useIns, useSec);
                    var sweepPath = RoutePlanner.FindOptimalRoute(grid, start, bestPort.Cell);
                    if (sweepPath == null) continue;
                    double sweepPerTonBase = RouteCostCalculator.ComputePerTonCost(
                        sweepPath, bestMode, pr, useIns, useSecurity: useSec);
                    double sweepPerTon = sweepPerTonBase + bestPort.UpgradeCost / TotalTonnageHorizon;

                    // Single-year profit for Year1 only
                    double mineCost = schedule[0].TonsToProduce < 0.5e6 ? 1000.0 :
                                      schedule[0].TonsToProduce < 14e6  ? 800.0 : 400.0;
                    double revenue = d * pr;
                    double transportOpex = d * sweepPerTon;
                    double trainsNeeded = Math.Ceiling(d / (12000.0 * 52.0));
                    double capExYear = (trainsNeeded * 15_000_000.0);
                    double insuranceYear = useIns ? 0.01 * revenue : 0.0;
                    double esgYear = AnnualESGBudget;
                    double singleProfit = revenue - (d * mineCost + transportOpex + capExYear + insuranceYear + esgYear);

                    Console.WriteLine($"Price={pr:C0}, Demand={d:N0} → Year1 Profit={singleProfit:C}");
                }
            }

            // Restore original first-year demand
            schedule[0].TonsToProduce = originalFirstYear;

            // ─── 2.1b Full 5-year NPV Sensitivity ───
            Console.WriteLine("\n=== Sensitivity: 5-year NPV Profit vs Price & Year1 Demand ===");
            foreach (var pr in priceRange)
            {
                foreach (var d in demandRange)
                {
                    // Temporarily set Year1 demand
                    schedule[0].TonsToProduce = d;

                    // Rebuild adjacency & compute route and per-ton cost
                    BuildWeightedAdjacency(bestMode, pr, useIns, useSec);
                    var sweepPath = RoutePlanner.FindOptimalRoute(grid, start, bestPort.Cell);
                    if (sweepPath == null) continue;
                    double sweepPerTonBase = RouteCostCalculator.ComputePerTonCost(
                        sweepPath, bestMode, pr, useIns, useSecurity: useSec);
                    double sweepPerTon = sweepPerTonBase + bestPort.UpgradeCost / TotalTonnageHorizon;

                    // Compute full NPV profit (reuse existing npvProfit logic)
                    double infraCapExUsdSweep = bestMode switch
                    {
                        TransportMode.DieselTrain   => sweepPath.Sum(c => c.StandardRailBuildCost * 1_000_000.0),
                        TransportMode.ElectricTrain => sweepPath.Sum(c => c.ElectricRailBuildCost * 1_000_000.0),
                        _                            => sweepPath.Sum(c => c.RoadBuildCost * 1_000_000.0)
                    };

                    double npvProfit = 0.0;
                    double sweepTrainsOwned = 0.0;
                    for (int i = 0; i < schedule.Length; i++)
                    {
                        var year2 = schedule[i];
                        double mineCost2 = year2.TonsToProduce < 0.5e6 ? 1000.0 :
                                           year2.TonsToProduce < 14e6  ? 800.0 : 400.0;
                        double trainsNeeded2 = Math.Ceiling(year2.TonsToProduce / (12000.0 * 52.0));
                        double buy2 = Math.Max(0.0, trainsNeeded2 - sweepTrainsOwned);
                        double capEx2 = buy2 * 15_000_000.0;
                        sweepTrainsOwned += buy2;

                        double mineOpex2 = year2.TonsToProduce * mineCost2;
                        double transportOpex2 = year2.TonsToProduce * sweepPerTon;
                        double revenue2 = year2.TonsToProduce * pr;
                        double insurance2 = useIns ? 0.01 * revenue2 : 0.0;
                        double esg2 = AnnualESGBudget;
                        double maintenance2 = MaintenanceRate * infraCapExUsdSweep;
                        double depreciationInfra2 = infraCapExUsdSweep / InfraLifetimeYears;
                        double depreciationVehicles2 = capEx2 / VehicleLifetimeYears;
                        double depreciation2 = depreciationInfra2 + depreciationVehicles2;
                        double net2 = revenue2 - (mineOpex2 + transportOpex2 + capEx2 + insurance2 + esg2 + maintenance2 + depreciation2);

                        int offset2 = year2.Year - 2025;
                        npvProfit += net2 / Math.Pow(1 + DiscountRate, offset2);
                    }

                    Console.WriteLine($"Price={pr:C0}, Demand={d:N0} → 5-year NPV Profit={npvProfit:C}");
                }
            }
            // Restore original first-year demand
            schedule[0].TonsToProduce = originalFirstYear;

            // ─── 2.2 Monte Carlo Simulation for Best Configuration ───
            Console.WriteLine("\n=== Monte Carlo Simulation: Profit Distribution ===");
            const int MonteCarloRuns = 10000;
            var rng = new Random();
            var profitSamples = new List<double>(MonteCarloRuns);

            // Precompute route and adjacency settings for best config except price
            // We'll reuse bestMode, bestPort, useIns, useSec
            for (int iter = 0; iter < MonteCarloRuns; iter++)
            {
                // Sample a price based on the discrete distribution
                double u = rng.NextDouble();
                double simPrice;
                if (u < 0.15) simPrice = 2200.0;
                else if (u < 0.15 + 0.60) simPrice = 4700.0;
                else simPrice = 6300.0;

                // Rebuild weighted graph for this price
                BuildWeightedAdjacency(bestMode, simPrice, useIns, useSec);

                // Compute route and per-ton cost
                var mcPath = RoutePlanner.FindOptimalRoute(grid, start, bestPort.Cell);
                if (mcPath == null) { profitSamples.Add(double.NaN); continue; }
                double mcBaseCost = RouteCostCalculator.ComputePerTonCost(mcPath, bestMode, simPrice, useIns, useSecurity: useSec);
                double mcTotalPerTon = mcBaseCost + bestPort.UpgradeCost / TotalTonnageHorizon;

                // Compute 5-year profit for this sim
                double mcProfit = 0.0;
                double mcTrainsOwned = 0.0;
                for (int i = 0; i < schedule.Length; i++)
                {
                    var year = schedule[i];
                    // Mine cost per ton (economies of scale)
                    double mcMineCost = year.TonsToProduce < 0.5e6 ? 1000.0 :
                                        year.TonsToProduce < 14e6  ? 800.0 : 400.0;
                    double mcTrainsNeeded = Math.Ceiling(year.TonsToProduce / (12000.0 * 52.0));
                    double mcBuy = Math.Max(0.0, mcTrainsNeeded - mcTrainsOwned);
                    double mcCapEx = mcBuy * 15_000_000.0;
                    mcTrainsOwned += mcBuy;

                    double mcMineOpex = year.TonsToProduce * mcMineCost;
                    double mcTransportOpex = year.TonsToProduce * mcTotalPerTon;
                    double mcRevenue = year.TonsToProduce * simPrice;
                    double mcInsuranceCost = useIns ? 0.01 * mcRevenue : 0.0;
                    double mcEsgCost = AnnualESGBudget;
                    double mcNet = mcRevenue - (mcMineOpex + mcTransportOpex + mcCapEx + mcInsuranceCost + mcEsgCost);
                    mcProfit += mcNet;
                }
                profitSamples.Add(mcProfit);
            }

            // Calculate statistics (excluding any NaN entries)
            var validSamples = profitSamples.Where(p => !double.IsNaN(p)).ToList();
            double mcMean = validSamples.Average();
            double mcStd  = Math.Sqrt(validSamples.Select(p => (p - mcMean)*(p - mcMean)).Average());
            double mcLossProb = validSamples.Count(p => p < 0) / (double)validSamples.Count;

            Console.WriteLine($"Monte Carlo runs: {validSamples.Count}");
            Console.WriteLine($"Mean 5-year profit: {mcMean:C}");
            Console.WriteLine($"Std dev: {mcStd:C}");
            Console.WriteLine($"Probability of loss: {mcLossProb:P2}");

            Console.WriteLine("\nPress any key to exit.");
            Console.ReadKey();
        }

        /// <summary>
        /// Create one GridCell per (i,j), assign Row, Col, and Label.
        /// Uses the class-level 'labels' array to initialize the grid.
        /// </summary>
        static void InitializeGrid()
        {
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < M; j++)
                {
                    var lbl = labels[i, j];
                    if (string.IsNullOrEmpty(lbl))
                    {
                        grid[i, j] = null; // blank square
                        continue;
                    }
                    var cell = new GridCell
                    {
                        Row = i,
                        Col = j,
                        Label = lbl,
                        IsRainforest = false,
                        IsMountain   = false,
                        IsRisky      = false
                    };
                    grid[i, j] = cell;
                    labelMap[cell.Label] = cell;
                }
            }
        }

        /// <summary>
        /// Mark each cell’s IsRainforest/IsMountain/IsRisky flags.
        /// </summary>
        static void AnnotateTerrain()
        {
            // ─── RAIN FOREST ───
            var rainforestLabels = new List<string>
            {
                "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "Q", "R", "S", "T", "U", "AA", "AB", "AC", "AD", "AE", "AL", "AM", "AN"
            };
            foreach (var lab in rainforestLabels)
                if (labelMap.ContainsKey(lab))
                    labelMap[lab].IsRainforest = true;

            // ─── MOUNTAINS ───
            var mountainLabels = new List<string>
            {
                "AF", "AG", "AH", "AI", "AK", "AM", "AN", "AP", "AQ", "AR", "AS", "AT", "AU", "AV", "AW", "AX", "AY", "AZ", "BA", "BB", "BC", "BE", "BF", "BG", "BH", "BI", "BJ", "BK", "BN", "BO", "BP", "BQ", "BR", "BS", "BU", "BV", "BW", "BX"
            };
            foreach (var lab in mountainLabels)
                if (labelMap.ContainsKey(lab))
                    labelMap[lab].IsMountain = true;

            // ─── RISKY TERRAIN (RED) ───
            var riskyLabels = new List<string>
            {
                "F", "AB", "AC", "AF", "AG", "AH", "AN", "AR", "AS", "BA", "BB", "BN", "BO"
            };
            foreach (var lab in riskyLabels)
                if (labelMap.ContainsKey(lab))
                    labelMap[lab].IsRisky = true;

            // ─── URBAN REGIONS ───
            var urbanLabels = new List<string>
            {
                "AO", "BD", "BM", "BL", "BT", "BY", "BZ"
            };
            foreach (var lab in urbanLabels)
                if (labelMap.ContainsKey(lab))
                    labelMap[lab].IsUrban = true;
        }

        /// <summary>
        /// Build adjacency lists for each cell.
        /// 4‐way connectivity (up/down/left/right). 
        /// </summary>
        static void BuildAdjacency()
        {
            // (Δrow, Δcol) for 4‐way connectivity
            (int dr, int dc)[] deltas = new (int, int)[]
            {
                ( 1,  0),  // down
                (-1,  0),  // up
                ( 0,  1),  // right
                ( 0, -1)   // left
            };

            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < M; j++)
                {
                    var currentCell = grid[i, j];
                    if (currentCell == null)
                        continue;

                    currentCell.Neighbors.Clear();

                    foreach (var (dr, dc) in deltas)
                    {
                        int ni = i + dr;
                        int nj = j + dc;
                        if (ni < 0 || ni >= N || nj < 0 || nj >= M)
                            continue;

                        var neighborCell = grid[ni, nj];
                        if (neighborCell == null)
                            continue;

                        double moveCost = (currentCell.Cost + neighborCell.Cost) / 2.0;
                        currentCell.Neighbors.Add(new Neighbor(neighborCell, moveCost));
                    }
                }
            }
        }

        /// <summary>
        /// Rebuilds adjacency lists so that A* uses a per-ton cost metric for the given mode and price.
        /// </summary>
        static void BuildWeightedAdjacency(TransportMode mode, double expectedPricePerTon, bool useInsurance, bool useSecurity)
        {
            // Precompute mode specs
            var specs = TransportModels.ModeSpecsMap[mode];
            for (int i = 0; i < N; i++)
            {
                for (int j = 0; j < M; j++)
                {
                    var currentCell = grid[i, j];
                    if (currentCell == null)
                        continue;
                    currentCell.Neighbors.Clear();

                    // Function to compute per-cell cost per ton
                    double CellCost(GridCell cell)
                    {
                        // CapEx per ton for this cell
                        double buildCostMillion = mode switch
                        {
                            TransportMode.DieselTrain   => cell.StandardRailBuildCost,
                            TransportMode.ElectricTrain => cell.ElectricRailBuildCost,
                            _                            => cell.RoadBuildCost
                        };
                        double capExPerTon = buildCostMillion * 1_000_000.0 / TotalTonnageHorizon;

                        // Operating cost
                        double opExPerTon = specs.OpExPerTon_USD;

                        // Spoilage and Security logic
                        double spoilage = 0.0;
                        double security = 0.0;

                        if (useInsurance)
                        {
                            // Insurance covers losses: no spoilage or surcharge.
                        }
                        else if (useSecurity)
                        {
                            // Private security: no spoilage, but surcharge = 15% of OpEx.
                            security = cell.SecuritySurchargeRate * opExPerTon;
                        }
                        else
                        {
                            // No mitigation: spoilage applies, no surcharge.
                            spoilage = cell.SpoilageLossPerTon(expectedPricePerTon);
                        }

                        return capExPerTon + opExPerTon + spoilage + security;
                    }

                    foreach (var (dr, dc) in new (int dr, int dc)[]{ (1,0),(-1,0),(0,1),(0,-1) })
                    {
                        int ni = i + dr, nj = j + dc;
                        if (ni < 0 || ni >= N || nj < 0 || nj >= M) continue;
                        var neighbor = grid[ni, nj];
                        if (neighbor == null) continue;

                        // Electric trucks cannot operate in mountainous regions
                        if (mode == TransportMode.ElectricTruck && neighbor.IsMountain)
                            continue;

                        // Average cost between cells
                        double costCurr = CellCost(currentCell);
                        double costNeigh = CellCost(neighbor);
                        double moveCost = (costCurr + costNeigh) / 2.0;

                        currentCell.Neighbors.Add(new Neighbor(neighbor, moveCost));
                    }
                }
            }
        }

        /// <summary>
        /// A* search from 'start' to 'goal'. Returns the list of cells
        /// in the optimal path (including both endpoints), or null if no path.
        /// </summary>
        public static List<GridCell> AStarSearch(GridCell start, GridCell goal)
        {
            // (1) Initialize all cells
            foreach (var cell in grid)
            {
                if (cell == null) continue;
                cell.GScore   = double.PositiveInfinity;
                cell.FScore   = double.PositiveInfinity;
                cell.CameFrom = null;
            }

            // (2) Start node: GScore=0, FScore=heuristic
            start.GScore = 0.0;
            start.FScore = Heuristic(start, goal);

            // (3) Use a min‐heap (priority queue) ordered by FScore
            var openSet = new PriorityQueue<GridCell, double>();
            openSet.Enqueue(start, start.FScore);

            // (4) Track which cells have been “closed” (fully processed)
            var closedSet = new HashSet<GridCell>();

            while (openSet.Count > 0)
            {
                // (5) Pop the cell with lowest FScore
                openSet.TryDequeue(out var current, out _);

                if (current == goal)
                {
                    // Found the goal—reconstruct and return the path
                    return ReconstructPath(goal);
                }

                closedSet.Add(current);

                // (6) For each neighbor, try to relax the edge
                foreach (var nb in current.Neighbors)
                {
                    var neighbor = nb.Cell;
                    if (closedSet.Contains(neighbor))
                        continue;

                    double tentativeG = current.GScore + nb.MoveCost;
                    if (tentativeG < neighbor.GScore)
                    {
                        neighbor.CameFrom = current;
                        neighbor.GScore   = tentativeG;
                        neighbor.FScore   = tentativeG + Heuristic(neighbor, goal);

                        // Even if neighbor is already in the queue with an old priority,
                        // enqueue it again with the new FScore. The built‐in PQ will
                        // eventually pop the version with the lowest FScore first.
                        openSet.Enqueue(neighbor, neighbor.FScore);
                    }
                }
            }

            // No path found
            return null;
        }

        /// <summary>
        /// Reconstructs the path by following CameFrom pointers from 'goal' back to 'start'.
        /// </summary>
        static List<GridCell> ReconstructPath(GridCell goal)
        {
            var path = new List<GridCell>();
            var current = goal;
            while (current != null)
            {
                path.Add(current);
                current = current.CameFrom;
            }
            path.Reverse();
            return path;
        }

        /// <summary>
        /// A simple Euclidean‐distance heuristic. Because the cheapest cell cost is 1.0,
        /// return sqrt((dr)^2 + (dc)^2). This never overestimates the true cost.
        /// </summary>
        static double Heuristic(GridCell a, GridCell b)
        {
            int dr = a.Row - b.Row;
            int dc = a.Col - b.Col;
            return Math.Sqrt(dr * dr + dc * dc);
        }
    }
}