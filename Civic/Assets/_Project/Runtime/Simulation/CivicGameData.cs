using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Civic.Simulation
{
    public enum ResourceCategory
    {
        Element,
        Aggregate,
        Numeric,
    }

    public enum BuildingRole
    {
        Capital,
        Production,
        Construction,
        Housing,
    }

    public sealed class ResourceDefinition
    {
        public string Id { get; }
        public string DisplayNameKo { get; }
        public ResourceCategory Category { get; }
        public CivicNumber BasePrice { get; }
        public double FoodConversion { get; }
        public bool IsStockpile { get; }
        public bool IsPopulationConsumption { get; }
        public string RequiredTechnologyId { get; }
        public int SortOrder { get; }

        public ResourceDefinition(
            string id,
            string displayNameKo,
            ResourceCategory category,
            CivicNumber basePrice,
            double foodConversion,
            bool isStockpile,
            bool isPopulationConsumption,
            string requiredTechnologyId,
            int sortOrder)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            Category = category;
            BasePrice = basePrice;
            FoodConversion = foodConversion;
            IsStockpile = isStockpile;
            IsPopulationConsumption = isPopulationConsumption;
            RequiredTechnologyId = requiredTechnologyId;
            SortOrder = sortOrder;
        }
    }

    public readonly struct ResourceAmount
    {
        public ResourceAmount(string resourceId, CivicNumber amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        public string ResourceId { get; }
        public CivicNumber Amount { get; }
    }

    public sealed class BuildingDefinition
    {
        public BuildingDefinition(
            string id,
            string displayNameKo,
            string eraId,
            BuildingRole role,
            bool isBuildable,
            CivicNumber constructionCost,
            CivicNumber treasuryCost,
            int populationUse,
            string unlockedByTechnologyId,
            int sortOrder,
            IReadOnlyList<ResourceAmount> inputs,
            IReadOnlyList<ResourceAmount> outputs)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            EraId = eraId;
            Role = role;
            IsBuildable = isBuildable;
            ConstructionCost = constructionCost;
            TreasuryCost = treasuryCost;
            PopulationUse = populationUse;
            UnlockedByTechnologyId = unlockedByTechnologyId;
            SortOrder = sortOrder;
            Inputs = inputs;
            Outputs = outputs;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public string EraId { get; }
        public BuildingRole Role { get; }
        public bool IsBuildable { get; }
        public CivicNumber ConstructionCost { get; }
        public CivicNumber TreasuryCost { get; }
        public int PopulationUse { get; }
        public string UnlockedByTechnologyId { get; }
        public int SortOrder { get; }
        public IReadOnlyList<ResourceAmount> Inputs { get; }
        public IReadOnlyList<ResourceAmount> Outputs { get; }
    }

    public sealed class TechnologyDefinition
    {
        public TechnologyDefinition(string id, string displayNameKo, string eraId, CivicNumber cost, string unlocksEraId, IReadOnlyList<string> prerequisiteTechnologyIds, double taxRateAdd, int sortOrder)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            EraId = eraId;
            Cost = cost;
            UnlocksEraId = unlocksEraId;
            PrerequisiteTechnologyIds = prerequisiteTechnologyIds;
            TaxRateAdd = taxRateAdd;
            SortOrder = sortOrder;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public string EraId { get; }
        public CivicNumber Cost { get; }
        public string UnlocksEraId { get; }
        public IReadOnlyList<string> PrerequisiteTechnologyIds { get; }
        public double TaxRateAdd { get; }
        public int SortOrder { get; }
    }

    public sealed class EraDefinition
    {
        public EraDefinition(string id, string displayNameKo, int order)
        {
            Id = id;
            DisplayNameKo = displayNameKo;
            Order = order;
        }

        public string Id { get; }
        public string DisplayNameKo { get; }
        public int Order { get; }
    }

    public sealed class CivicInitialState
    {
        public CivicInitialState(
            IReadOnlyDictionary<string, CivicNumber> resources,
            IReadOnlyDictionary<string, int> buildings,
            IReadOnlyCollection<string> technologies)
        {
            Resources = resources;
            Buildings = buildings;
            Technologies = technologies;
        }

        public IReadOnlyDictionary<string, CivicNumber> Resources { get; }
        public IReadOnlyDictionary<string, int> Buildings { get; }
        public IReadOnlyCollection<string> Technologies { get; }
    }

    public sealed class CivicGameData
    {
        public CivicGameData(
            IReadOnlyList<ResourceDefinition> resources,
            IReadOnlyList<BuildingDefinition> buildings,
            IReadOnlyList<TechnologyDefinition> technologies,
            IReadOnlyList<EraDefinition> eras,
            CivicInitialState initialState)
        {
            Resources = resources.OrderBy(resource => resource.SortOrder).ToArray();
            Buildings = buildings.OrderBy(building => building.SortOrder).ToArray();
            Technologies = technologies.OrderBy(technology => technology.SortOrder).ToArray();
            Eras = eras.OrderBy(era => era.Order).ToArray();
            InitialState = initialState;
            ResourcesById = Resources.ToDictionary(resource => resource.Id, StringComparer.Ordinal);
            BuildingsById = Buildings.ToDictionary(building => building.Id, StringComparer.Ordinal);
            TechnologiesById = Technologies.ToDictionary(technology => technology.Id, StringComparer.Ordinal);
            ErasById = Eras.ToDictionary(era => era.Id, StringComparer.Ordinal);
        }

        public IReadOnlyList<ResourceDefinition> Resources { get; }
        public IReadOnlyList<BuildingDefinition> Buildings { get; }
        public IReadOnlyList<TechnologyDefinition> Technologies { get; }
        public IReadOnlyList<EraDefinition> Eras { get; }
        public CivicInitialState InitialState { get; }
        public IReadOnlyDictionary<string, ResourceDefinition> ResourcesById { get; }
        public IReadOnlyDictionary<string, BuildingDefinition> BuildingsById { get; }
        public IReadOnlyDictionary<string, TechnologyDefinition> TechnologiesById { get; }
        public IReadOnlyDictionary<string, EraDefinition> ErasById { get; }

        public EraDefinition StartingEra => Eras.OrderBy(era => era.Order).First();
    }

    public sealed class CivicDataException : Exception
    {
        public CivicDataException(IReadOnlyList<string> errors)
            : base("Civic data validation failed:\n- " + string.Join("\n- ", errors))
        {
            Errors = errors;
        }

        public IReadOnlyList<string> Errors { get; }
    }

    public static class CivicGameDataLoader
    {
        public static CivicGameData Load(string resourcesCsv, string buildingsCsv, string technologiesCsv, string erasCsv, string initialStateCsv)
        {
            var errors = new List<string>();
            var resources = LoadResources(resourcesCsv, errors);
            var eras = LoadEras(erasCsv, errors);
            var technologies = LoadTechnologies(technologiesCsv, errors);
            var buildings = LoadBuildings(buildingsCsv, errors);
            var initialState = LoadInitialState(initialStateCsv, errors);

            Validate(resources, buildings, technologies, eras, initialState, errors);

            if (errors.Count > 0)
            {
                throw new CivicDataException(errors);
            }

            return new CivicGameData(resources, buildings, technologies, eras, initialState);
        }

        private static List<ResourceDefinition> LoadResources(string csv, ICollection<string> errors)
        {
            var rows = CivicCsvParser.Parse(csv, errors, "resources.csv");
            return rows.Select(row => new ResourceDefinition(
                Required(row, "id", "resources.csv", errors),
                Required(row, "displayNameKo", "resources.csv", errors),
                ParseEnum<ResourceCategory>(Required(row, "category", "resources.csv", errors), "resources.csv", errors),
                ParseNumber(row, "basePrice", "resources.csv", errors),
                ParseDouble(row, "foodConversion", "resources.csv", errors),
                ParseBool(row, "isStockpile", "resources.csv", errors),
                ParseBool(row, "isPopulationConsumption", "resources.csv", errors),
                Optional(row, "requiredTechnologyId"),
                ParseInt(row, "sortOrder", "resources.csv", errors))).ToList();
        }

        private static List<BuildingDefinition> LoadBuildings(string csv, ICollection<string> errors)
        {
            var rows = CivicCsvParser.Parse(csv, errors, "buildings.csv");
            return rows.Select(row => new BuildingDefinition(
                Required(row, "id", "buildings.csv", errors),
                Required(row, "displayNameKo", "buildings.csv", errors),
                Required(row, "eraId", "buildings.csv", errors),
                ParseEnum<BuildingRole>(Required(row, "role", "buildings.csv", errors), "buildings.csv", errors),
                ParseBool(row, "isBuildable", "buildings.csv", errors),
                ParseNumber(row, "constructionCost", "buildings.csv", errors),
                ParseNumber(row, "treasuryCost", "buildings.csv", errors),
                ParseInt(row, "populationUse", "buildings.csv", errors),
                Optional(row, "unlockedByTechnologyId"),
                ParseInt(row, "sortOrder", "buildings.csv", errors),
                LoadAmounts(row, "input", "buildings.csv", errors),
                LoadAmounts(row, "output", "buildings.csv", errors))).ToList();
        }

        private static List<TechnologyDefinition> LoadTechnologies(string csv, ICollection<string> errors)
        {
            var rows = CivicCsvParser.Parse(csv, errors, "technologies.csv");
            return rows.Select(row => new TechnologyDefinition(
                Required(row, "id", "technologies.csv", errors),
                Required(row, "displayNameKo", "technologies.csv", errors),
                Required(row, "eraId", "technologies.csv", errors),
                ParseNumber(row, "cost", "technologies.csv", errors),
                Optional(row, "unlocksEraId"),
                SplitIds(Optional(row, "prerequisiteTechnologyIds")),
                ParseDouble(row, "taxRateAdd", "technologies.csv", errors),
                ParseInt(row, "sortOrder", "technologies.csv", errors))).ToList();
        }

        private static List<EraDefinition> LoadEras(string csv, ICollection<string> errors)
        {
            var rows = CivicCsvParser.Parse(csv, errors, "eras.csv");
            return rows.Select(row => new EraDefinition(
                Required(row, "id", "eras.csv", errors),
                Required(row, "displayNameKo", "eras.csv", errors),
                ParseInt(row, "order", "eras.csv", errors))).ToList();
        }

        private static CivicInitialState LoadInitialState(string csv, ICollection<string> errors)
        {
            var rows = CivicCsvParser.Parse(csv, errors, "initial_state.csv");
            var resources = new Dictionary<string, CivicNumber>(StringComparer.Ordinal);
            var buildings = new Dictionary<string, int>(StringComparer.Ordinal);
            var technologies = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var kind = Required(row, "kind", "initial_state.csv", errors);
                var id = Required(row, "id", "initial_state.csv", errors);
                if (kind == "resource")
                {
                    resources[id] = ParseNumber(row, "amount", "initial_state.csv", errors);
                }
                else if (kind == "building")
                {
                    buildings[id] = ParseInt(row, "amount", "initial_state.csv", errors);
                }
                else if (kind == "technology")
                {
                    var amount = ParseInt(row, "amount", "initial_state.csv", errors);
                    if (amount != 1)
                    {
                        errors.Add($"initial_state.csv technology {id} amount must be 1.");
                    }

                    technologies.Add(id);
                }
                else
                {
                    errors.Add($"initial_state.csv has unknown kind '{kind}'.");
                }
            }

            return new CivicInitialState(resources, buildings, technologies);
        }

        private static void Validate(
            IReadOnlyList<ResourceDefinition> resources,
            IReadOnlyList<BuildingDefinition> buildings,
            IReadOnlyList<TechnologyDefinition> technologies,
            IReadOnlyList<EraDefinition> eras,
            CivicInitialState initialState,
            ICollection<string> errors)
        {
            CheckDuplicate(resources.Select(resource => resource.Id), "resource id", errors);
            CheckDuplicate(buildings.Select(building => building.Id), "building id", errors);
            CheckDuplicate(technologies.Select(technology => technology.Id), "technology id", errors);
            CheckDuplicate(eras.Select(era => era.Id), "era id", errors);

            var resourceIds = new HashSet<string>(resources.Select(resource => resource.Id), StringComparer.Ordinal);
            var buildingIds = new HashSet<string>(buildings.Select(building => building.Id), StringComparer.Ordinal);
            var technologyIds = new HashSet<string>(technologies.Select(technology => technology.Id), StringComparer.Ordinal);
            var eraIds = new HashSet<string>(eras.Select(era => era.Id), StringComparer.Ordinal);

            RequireReference(resourceIds, "population", "required core resource", errors);
            RequireReference(resourceIds, "food", "required core resource", errors);
            RequireReference(resourceIds, "science", "required core resource", errors);
            RequireReference(resourceIds, "treasury", "required core resource", errors);
            RequireReference(resourceIds, "construction_power", "required core resource", errors);
            if (eras.Count == 0)
            {
                errors.Add("eras.csv must contain at least one era.");
            }

            foreach (var building in buildings)
            {
                RequireReference(eraIds, building.EraId, $"building {building.Id} eraId", errors);
                if (!string.IsNullOrEmpty(building.UnlockedByTechnologyId))
                {
                    RequireReference(technologyIds, building.UnlockedByTechnologyId, $"building {building.Id} unlockedByTechnologyId", errors);
                }

                foreach (var amount in building.Inputs.Concat(building.Outputs))
                {
                    RequireReference(resourceIds, amount.ResourceId, $"building {building.Id} resource reference", errors);
                }
            }

            foreach (var resource in resources)
            {
                if (!string.IsNullOrEmpty(resource.RequiredTechnologyId))
                {
                    RequireReference(technologyIds, resource.RequiredTechnologyId, $"resource {resource.Id} requiredTechnologyId", errors);
                }
            }

            foreach (var technology in technologies)
            {
                RequireReference(eraIds, technology.EraId, $"technology {technology.Id} eraId", errors);
                if (!string.IsNullOrEmpty(technology.UnlocksEraId))
                {
                    RequireReference(eraIds, technology.UnlocksEraId, $"technology {technology.Id} unlocksEraId", errors);
                }

                foreach (var prerequisite in technology.PrerequisiteTechnologyIds)
                {
                    RequireReference(technologyIds, prerequisite, $"technology {technology.Id} prerequisiteTechnologyIds", errors);
                }
            }

            foreach (var id in initialState.Resources.Keys)
            {
                RequireReference(resourceIds, id, $"initial resource {id}", errors);
            }

            foreach (var id in initialState.Buildings.Keys)
            {
                RequireReference(buildingIds, id, $"initial building {id}", errors);
            }

            foreach (var id in initialState.Technologies)
            {
                RequireReference(technologyIds, id, $"initial technology {id}", errors);
            }
        }

        private static IReadOnlyList<ResourceAmount> LoadAmounts(IReadOnlyDictionary<string, string> row, string prefix, string source, ICollection<string> errors)
        {
            var result = new List<ResourceAmount>();
            for (var index = 1; index <= 5; index++)
            {
                var id = Optional(row, $"{prefix}{index}Id");
                var amountText = Optional(row, $"{prefix}{index}Amount");
                if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(amountText))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(amountText))
                {
                    errors.Add($"{source} has incomplete {prefix}{index} pair.");
                    continue;
                }

                try
                {
                    result.Add(new ResourceAmount(id, CivicNumber.Parse(amountText)));
                }
                catch (Exception exception)
                {
                    errors.Add($"{source} invalid {prefix}{index}Amount: {exception.Message}");
                }
            }

            return result;
        }

        private static IReadOnlyList<string> SplitIds(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(';').Select(part => part.Trim()).Where(part => part.Length > 0).ToArray();
        }

        private static string Required(IReadOnlyDictionary<string, string> row, string key, string source, ICollection<string> errors)
        {
            var value = Optional(row, key);
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{source} missing required value '{key}'.");
            }

            return value;
        }

        private static string Optional(IReadOnlyDictionary<string, string> row, string key)
        {
            return row.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
        }

        private static CivicNumber ParseNumber(IReadOnlyDictionary<string, string> row, string key, string source, ICollection<string> errors)
        {
            try
            {
                return CivicNumber.Parse(Required(row, key, source, errors));
            }
            catch (Exception exception)
            {
                errors.Add($"{source} invalid number '{key}': {exception.Message}");
                return CivicNumber.Zero;
            }
        }

        private static double ParseDouble(IReadOnlyDictionary<string, string> row, string key, string source, ICollection<string> errors)
        {
            var text = Required(row, key, source, errors);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                errors.Add($"{source} invalid double '{key}': {text}");
            }

            return value;
        }

        private static int ParseInt(IReadOnlyDictionary<string, string> row, string key, string source, ICollection<string> errors)
        {
            var text = Required(row, key, source, errors);
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                errors.Add($"{source} invalid integer '{key}': {text}");
            }

            return value;
        }

        private static bool ParseBool(IReadOnlyDictionary<string, string> row, string key, string source, ICollection<string> errors)
        {
            var text = Required(row, key, source, errors);
            if (!bool.TryParse(text, out var value))
            {
                errors.Add($"{source} invalid bool '{key}': {text}");
            }

            return value;
        }

        private static T ParseEnum<T>(string value, string source, ICollection<string> errors) where T : struct
        {
            var normalized = value.Replace("_", string.Empty);
            if (Enum.TryParse<T>(normalized, true, out var parsed))
            {
                return parsed;
            }

            errors.Add($"{source} invalid {typeof(T).Name}: {value}");
            return default;
        }

        private static void CheckDuplicate(IEnumerable<string> values, string label, ICollection<string> errors)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (!seen.Add(value))
                {
                    errors.Add($"Duplicate {label}: {value}");
                }
            }
        }

        private static void RequireReference(ISet<string> knownIds, string id, string label, ICollection<string> errors)
        {
            if (string.IsNullOrEmpty(id) || !knownIds.Contains(id))
            {
                errors.Add($"Unknown {label}: {id}");
            }
        }
    }

    public static class CivicCsvParser
    {
        public static IReadOnlyList<IReadOnlyDictionary<string, string>> Parse(string text, ICollection<string> errors, string source)
        {
            var rows = ParseRows(text ?? string.Empty);
            if (rows.Count == 0)
            {
                errors.Add($"{source} is empty.");
                return Array.Empty<IReadOnlyDictionary<string, string>>();
            }

            var headers = rows[0];
            var result = new List<IReadOnlyDictionary<string, string>>();
            for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row.Count == 1 && string.IsNullOrWhiteSpace(row[0]))
                {
                    continue;
                }

                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var column = 0; column < headers.Count; column++)
                {
                    map[headers[column]] = column < row.Count ? row[column] : string.Empty;
                }

                if (row.Count > headers.Count)
                {
                    errors.Add($"{source} row {rowIndex + 1} has more columns than the header.");
                }

                result.Add(map);
            }

            return result;
        }

        private static List<List<string>> ParseRows(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var index = 0; index < text.Length; index++)
            {
                var c = text[index];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (index + 1 < text.Length && text[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if (c == '\n')
                {
                    row.Add(TrimCarriageReturn(field.ToString()));
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    field.Append(c);
                }
            }

            row.Add(TrimCarriageReturn(field.ToString()));
            rows.Add(row);
            return rows;
        }

        private static string TrimCarriageReturn(string value)
        {
            return value.EndsWith("\r", StringComparison.Ordinal) ? value.Substring(0, value.Length - 1) : value;
        }
    }
}
