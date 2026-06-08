using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ModuleBoundaryAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MONO001";
    private static readonly LocalizableString Title = "Cross-module boundary violation";
    private static readonly LocalizableString MessageFormat = "Module '{0}' cannot reference '{1}' from module '{2}'. Accessing another module's internal Domain or Infrastructure is prohibited.";
    private static readonly LocalizableString Description = "Modules must maintain strict boundaries. Access to internal Domain or Infrastructure layers of other modules is not allowed.";
    private const string Category = "Architecture";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        var identifier = (IdentifierNameSyntax)context.Node;

        // Fast checks before semantic model query to preserve compiler performance
        var name = identifier.Identifier.Text;
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            // Skip lowercase names (usually local variables, camelCase arguments etc.)
            return;
        }

        // Get the symbol of the identifier
        var symbolInfo = context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken);
        var symbol = symbolInfo.Symbol;
        if (symbol == null) return;

        // Get the namespace of the referenced type/member
        var targetNamespace = (symbol is INamedTypeSymbol || symbol is ITypeSymbol)
            ? symbol.ContainingNamespace?.ToDisplayString()
            : symbol.ContainingType?.ContainingNamespace?.ToDisplayString();

        if (targetNamespace is null || !targetNamespace.StartsWith("Monolith.Modules."))
            return;

        // Get the namespace of the code currently being analyzed
        var sourceSymbol = context.ContainingSymbol;
        if (sourceSymbol == null) return;
        var sourceNamespace = sourceSymbol.ContainingNamespace?.ToDisplayString();
        if (sourceNamespace is null || !sourceNamespace.StartsWith("Monolith.Modules."))
            return;

        var sourceModule = GetModuleName(sourceNamespace);
        var targetModule = GetModuleName(targetNamespace);

        if (sourceModule == null || targetModule == null)
            return;

        // If referencing another module
        if (sourceModule != targetModule)
        {
            // Restrict access to internal namespaces (.Domain or .Infrastructure)
            if (targetNamespace.Contains(".Domain") || targetNamespace.Contains(".Infrastructure"))
            {
                var diagnostic = Diagnostic.Create(
                    Rule, 
                    identifier.GetLocation(), 
                    sourceModule, 
                    symbol.ToDisplayString(), 
                    targetModule);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static string? GetModuleName(string? ns)
    {
        if (ns is null || !ns.StartsWith("Monolith.Modules."))
            return null;

        var parts = ns.Split('.');
        if (parts.Length > 2)
        {
            return parts[2];
        }
        return null;
    }
}
