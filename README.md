# RouteFinder

**RouteFinder** is a C#/.NET application that models and optimizes transportation of bulk materials (e.g., graphite) across a grid of regions with varying terrain, risk, and infrastructure costs. It computes cost‐effective routes, runs financial projections (including NPV and Monte Carlo simulations), and performs sensitivity analyses.

## Features

* **Grid‐based routing** using A\* search with custom per‐cell cost metrics
* Multi‐mode transport: diesel/electric trains and trucks, handling fees, insurance vs. private security
* Infrastructure CapEx and OpEx amortization, depreciation, and maintenance
* 5‐year cash‐flow projections, NPV, and probability‐weighted expected‐value analysis
* Sensitivity sweeps (Year 1 profit and full 5‑year NPV) over price and demand
* Monte Carlo simulation of price volatility scenarios

## Prerequisites

* [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
* Git (for source control)

## Getting Started

1. **Clone the repository**

   ```bash
   git clone https://github.com/SubjectAI/RouteFinder.git
   cd RouteFinder
   ```

2. **Restore dependencies & build**

   ```bash
   dotnet restore
   dotnet build
   ```

3. **Run the application**

   ```bash
   dotnet run --project RouteFinder
   ```

   This will:

   * Enumerate price scenarios and ports
   * Compute optimal routes and cost projections
   * Output expected‐value ranking and detailed recommendation
   * Perform sensitivity sweeps and Monte Carlo simulation

## Project Structure

* `Program.cs` — Main entry: grid setup, scenario loops, reporting
* `GridCell.cs` — Defines terrain, risk, costs, and adjacency logic
* `RoutePlanner.cs` — Wrapper for A\* search pathfinding
* `RouteCostCalculator.cs` — Aggregates per‑ton cost calculation
* `TransportModels.cs` — Defines transport modes, spec lookup
* `ProductionSchedule.cs` — Holds per‑year production targets & prices
* `FinancialModel.cs` — (If used) encapsulates cash‑flow & NPV logic
* `RouteFinder.Tests/` — Unit tests for core algorithms (xUnit)

## Running Tests

Inside the solution root:

```bash
cd RouteFinder.Tests
dotnet test
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/YourFeature`)
3. Commit your changes (`git commit -m "Add feature..."`)
4. Push to branch (`git push origin feature/YourFeature`)
5. Open a Pull Request on GitHub

Please adhere to the existing coding style and include unit tests for new functionality.

## .gitignore

A sample `.gitignore` for C#/.NET projects:

```gitignore
# Build directories
bin/
obj/

# User-specific files
*.user
*.suo
*.userosscache
*.sln.docstates

# Visual Studio Code settings
.vscode/

# Rider settings
.idea/

# Visual Studio files
*.vcxproj.filters
*.vcxproj.user
*.csproj.user

# Package directories
*.nuget/

# Test result files
TestResults/

# DotNet watch files
.dotnetwatch

# Logs
*.log

# OS files
.DS_Store
Thumbs.db
```
