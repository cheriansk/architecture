using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Monolith.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DbContextToTableAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MONO002";
    private static readonly LocalizableString Title = "ToTable must specify schema and use module table prefix";
    private static readonly LocalizableString MessageFormat = "Table mapping for '{0}' must specify a tenant schema and the table name must be prefixed with '{1}'";
    private static readonly LocalizableString Description = "All database tables must be mapped to a dynamic schema (schema-per-tenant) and use the module's lowercase prefix (e.g. 'catalog_products').";
    private const string Category = "Database";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId, Title, MessageFormat, Category,
        DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Fast syntax-based name check
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == "ToTable")
        {
            var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
            if (symbol == null) return;

            // Verify it is the Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions extension method
            var containingType = symbol.ContainingType?.ToDisplayString();
            if (containingType == "Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions")
            {
                var argumentList = invocation.ArgumentList;
                if (argumentList == null || argumentList.Arguments.Count == 0) return;

                // Get containing namespace
                var containingSymbol = context.ContainingSymbol;
                if (containingSymbol == null) return;
                var ns = containingSymbol.ContainingNamespace?.ToDisplayString();
                if (ns is null || !ns.StartsWith("Monolith.Modules.")) return;

                var moduleName = GetModuleName(ns);
                if (moduleName == null) return;
                var expectedPrefix = moduleName.ToLowerInvariant() + "_";

                // 1. Check if the schema argument is missing
                // Extension methods in C# invocation syntax: builder.ToTable("tableName", schema) has 2 arguments.
                // Overloads with schema parameter usually have 2 or more arguments.
                if (argumentList.Arguments.Count < 2)
                {
                    var diagnostic = Diagnostic.Create(
                        Rule, 
                        invocation.GetLocation(), 
                        symbol.Name, 
                        expectedPrefix);
                    context.ReportDiagnostic(diagnostic);
                    return;
                }

                // 2. Check if the first argument (table name) starts with the expected module prefix
                var tableNameArg = argumentList.Arguments[0];
                var constantValue = context.SemanticModel.GetConstantValue(tableNameArg.Expression, context.CancellationToken);
                if (constantValue.HasValue && constantValue.Value is string tableName)
                {
                    if (!tableName.StartsWith(expectedPrefix, System.StringComparison.Ordinal))
                    {
                        var diagnostic = Diagnostic.Create(
                            Rule, 
                            tableNameArg.GetLocation(), 
                            tableName, 
                            expectedPrefix);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
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
