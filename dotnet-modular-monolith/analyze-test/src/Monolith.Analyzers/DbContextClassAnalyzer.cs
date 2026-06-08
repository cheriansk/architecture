using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DbContextClassAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MONO003";
    private static readonly LocalizableString Title = "DbContext class implementation violation";
    private static readonly LocalizableString MessageFormat = "DbContext class '{0}' must be declared as internal and implement ITenantDbContext";
    private static readonly LocalizableString Description = "All DbContext classes defined within modules must be marked as internal and implement the ITenantDbContext interface to enforce database boundaries and multi-tenant key factory routing.";
    private const string Category = "Architecture";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;

        // Skip interface declarations, enums, structs etc.
        if (typeSymbol.TypeKind != TypeKind.Class) return;

        // Check if the type inherits from DbContext (transitive check)
        if (!IsInheritingFrom(typeSymbol, "Microsoft.EntityFrameworkCore.DbContext"))
            return;

        // Check if the DbContext class is inside a module namespace
        var ns = typeSymbol.ContainingNamespace?.ToDisplayString();
        if (ns is null || !ns.StartsWith("Monolith.Modules."))
            return;

        // Check ITenantDbContext implementation
        bool implementsTenantDbContext = false;
        foreach (var iface in typeSymbol.AllInterfaces)
        {
            if (iface.ToDisplayString() == "Monolith.Shared.Infrastructure.ITenantDbContext")
            {
                implementsTenantDbContext = true;
                break;
            }
        }

        // Check if the class is public
        bool isPublic = typeSymbol.DeclaredAccessibility == Accessibility.Public;

        if (isPublic || !implementsTenantDbContext)
        {
            var location = typeSymbol.Locations.FirstOrDefault() ?? Location.None;
            var diagnostic = Diagnostic.Create(Rule, location, typeSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsInheritingFrom(INamedTypeSymbol type, string baseTypeFullName)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.ToDisplayString() == baseTypeFullName)
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
