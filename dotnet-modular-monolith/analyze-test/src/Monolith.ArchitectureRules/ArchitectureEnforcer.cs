using NetArchTest.Rules;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Monolith.ArchitectureRules;

public class ArchitectureEnforcerResult
{
    public bool IsSuccessful { get; set; }
    public List<string> Failures { get; set; } = new();
    public string ErrorMessage => string.Join("\n", Failures);
}

public static class ArchitectureEnforcer
{
    public static ArchitectureEnforcerResult Verify(Assembly monolithAssembly)
    {
        var result = new ArchitectureEnforcerResult { IsSuccessful = true };

        // 1. Verify Domain and Infrastructure classes in modules are not public
        var internalTypesResult = Types.InAssembly(monolithAssembly)
            .That().ResideInNamespaceStartingWith("Monolith.Modules.")
            .And().ResideInNamespaceEndingWith(".Domain")
            .Or().ResideInNamespaceEndingWith(".Infrastructure")
            .Should().NotBePublic()
            .GetResult();

        if (!internalTypesResult.IsSuccessful)
        {
            result.IsSuccessful = false;
            if (internalTypesResult.FailingTypes != null)
            {
                foreach (var type in internalTypesResult.FailingTypes)
                {
                    result.Failures.Add($"Type '{type.FullName}' violates accessibility rules: module Domain and Infrastructure classes must be internal.");
                }
            }
        }

        // 2. Verify Module Isolation: No cross-module Domain/Infrastructure references
        // Find all module names dynamically based on Monolith.Modules.* namespace structures
        var allTypes = Types.InAssembly(monolithAssembly).GetTypes();
        var moduleNames = allTypes
            .Select(t => GetModuleName(t.Namespace))
            .Where(name => name != null)
            .Distinct()
            .ToList();

        foreach (var module in moduleNames)
        {
            // Gather Domain and Infrastructure namespaces of OTHER modules
            var forbiddenNamespaces = moduleNames
                .Where(m => m != module)
                .SelectMany(m => new[] { $"Monolith.Modules.{m}.Domain", $"Monolith.Modules.{m}.Infrastructure" })
                .ToArray();

            if (forbiddenNamespaces.Length == 0)
                continue;

            // Types inside Monolith.Modules.[Module].* should not reference other module Domain/Infrastructure
            var moduleIsolationResult = Types.InAssembly(monolithAssembly)
                .That().ResideInNamespaceStartingWith($"Monolith.Modules.{module}.")
                .ShouldNot()
                .HaveDependencyOnAny(forbiddenNamespaces)
                .GetResult();

            if (!moduleIsolationResult.IsSuccessful)
            {
                result.IsSuccessful = false;
                if (moduleIsolationResult.FailingTypes != null)
                {
                    foreach (var type in moduleIsolationResult.FailingTypes)
                    {
                        result.Failures.Add($"Type '{type.FullName}' in Module '{module}' violates architectural boundaries: referencing internal namespaces of another module is forbidden.");
                    }
                }
            }
        }

        return result;
    }

    private static string? GetModuleName(string? ns)
    {
        if (string.IsNullOrEmpty(ns) || !ns.StartsWith("Monolith.Modules."))
            return null;

        var parts = ns.Split('.');
        if (parts.Length > 2)
        {
            return parts[2];
        }
        return null;
    }
}
