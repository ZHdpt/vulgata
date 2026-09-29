using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class TestPolicySyntax
{
    public sealed class Result
    {
        public string Code;
        public string[] Types;
        public string[] Errors;
    }

    static string Name(NameSyntax name) => name.ToString().Split('.').Last().Replace("global::", "").Replace("Attribute", "");
    static string Literal(AttributeSyntax attribute, int index) => attribute.ArgumentList?.Arguments.ElementAtOrDefault(index)?.Expression is LiteralExpressionSyntax value ? value.Token.ValueText : null;

    public static Result Inspect(string source, string suite, string file, string[] exceptions)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var errors = new List<string>();
        var permitted = new HashSet<string>(exceptions ?? Array.Empty<string>(), StringComparer.Ordinal);
        foreach (string exception in permitted)
            if (file != "PreparedChatIntegrationTests.cs" || (exception != "test-owned-keyring-environment" && exception != "modeled-http-handler-type"))
                errors.Add("Unreviewed resource exception: " + exception);

        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var attrs = method.AttributeLists.SelectMany(x => x.Attributes).ToArray();
            var facts = attrs.Where(x => Name(x.Name).EndsWith("Fact", StringComparison.Ordinal) || Name(x.Name).EndsWith("Theory", StringComparison.Ordinal)).ToArray();
            if (facts.Length == 0) continue;
            var traits = attrs.Where(x => Name(x.Name) == "Trait" && Literal(x, 0) == "Suite").ToArray();
            if (facts.Length != 1 || traits.Length != 1 || Literal(traits[0],1) != suite)
                errors.Add("Every test needs one matching Suite trait: " + method.Identifier.ValueText);
            foreach (var fact in facts)
            {
                string name = Name(fact.Name);
                if (name == "Fact" || name == "Theory") errors.Add("Unguarded test attribute: " + method.Identifier.ValueText);
                if ((name == "SuiteFact" || name == "SuiteTheory") && Literal(fact,0) != suite)
                    errors.Add("Guard conflicts with trait/manifest: " + method.Identifier.ValueText);
            }
            if (suite == "Policy") errors.Add("Infrastructure exemptions cannot contain tests");
        }

        foreach (var type in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            foreach (var baseType in type.BaseList?.Types ?? default(SeparatedSyntaxList<BaseTypeSyntax>))
            {
                string name = baseType.Type.ToString().Split('.').Last();
                if (file != "TestInfrastructure/SuiteAttributes.cs" && (name == "FactAttribute" || name == "TheoryAttribute"))
                    errors.Add("Custom attribute bypasses suite guard");
                if (name != "SuiteFactAttribute" && name != "SuiteTheoryAttribute") continue;
                if (!type.Identifier.ValueText.EndsWith("FactAttribute", StringComparison.Ordinal) && !type.Identifier.ValueText.EndsWith("TheoryAttribute", StringComparison.Ordinal))
                    errors.Add("Custom test attributes must retain a Fact/Theory suffix");
                var constructors = type.Members.OfType<ConstructorDeclarationSyntax>().ToArray();
                if (constructors.Length == 0 || constructors.Any(c => c.Initializer?.ArgumentList.Arguments.FirstOrDefault()?.Expression is not LiteralExpressionSyntax literal || literal.Token.ValueText != suite))
                    errors.Add("Custom attribute guard conflicts with manifest");
            }
        }

        foreach (var alias in root.DescendantNodes().OfType<UsingDirectiveSyntax>().Where(x => x.Alias != null))
        {
            string terminal = alias.Name.ToString().Split('.').Last().Replace("global::", "");
            if (terminal.EndsWith("Fact", StringComparison.Ordinal) || terminal.EndsWith("Theory", StringComparison.Ordinal) ||
                terminal.EndsWith("FactAttribute", StringComparison.Ordinal) || terminal.EndsWith("TheoryAttribute", StringComparison.Ordinal))
                errors.Add("Test attribute aliases obscure classification");
        }

        var excluded = new List<Microsoft.CodeAnalysis.Text.TextSpan>();
        if (permitted.Contains("test-owned-keyring-environment") && file == "PreparedChatIntegrationTests.cs")
            foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                if (call.Expression.ToString() == "Environment.GetEnvironmentVariable" && call.ArgumentList.Arguments.Count == 1 &&
                    call.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax id && (id.Identifier.ValueText == "_keyRingPathVariable" || id.Identifier.ValueText == "_applicationNameVariable")) excluded.Add(call.Span);
        if (permitted.Contains("modeled-http-handler-type") && file == "PreparedChatIntegrationTests.cs")
            foreach (var name in root.DescendantNodes().OfType<GenericNameSyntax>())
                if (name.Identifier.ValueText == "IsType" && name.Parent is MemberAccessExpressionSyntax member && member.Expression.ToString() == "Assert")
                    foreach (var arg in name.TypeArgumentList.Arguments.Where(x => x.ToString() == "SocketsHttpHandler")) excluded.Add(arg.Span);

        // Syntax tokens preserve executable interpolation expressions; string content and comments are inert.
        string code = string.Join(" ", root.DescendantTokens().Where(t =>
            t.Kind() != SyntaxKind.StringLiteralToken && t.Kind() != SyntaxKind.Utf8StringLiteralToken &&
            t.Kind() != SyntaxKind.SingleLineRawStringLiteralToken && t.Kind() != SyntaxKind.MultiLineRawStringLiteralToken &&
            t.Kind() != SyntaxKind.InterpolatedStringTextToken && t.Kind() != SyntaxKind.CharacterLiteralToken &&
            !excluded.Any(span => span.Contains(t.Span))).Select(t => t.Text));

        if (suite == "SelfContained")
        {
            foreach (var creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (creation.ArgumentList != null && creation.ArgumentList.Arguments.Count != 0) continue;
                bool http = creation is ObjectCreationExpressionSyntax direct && direct.Type.ToString().Split('.').Last() == "HttpClient";
                if (creation is ImplicitObjectCreationExpressionSyntax)
                    http = creation.Ancestors().Any(a =>
                        a is VariableDeclarationSyntax v && v.Type.ToString().Contains("HttpClient") ||
                        a is MethodDeclarationSyntax m && m.ReturnType.ToString().Contains("HttpClient") ||
                        a is PropertyDeclarationSyntax p && p.Type.ToString().Contains("HttpClient") ||
                        a is LocalFunctionStatementSyntax f && f.ReturnType.ToString().Contains("HttpClient") ||
                        a is InvocationExpressionSyntax call && call.Expression.ToString().Contains("HttpClient"));
                if (http) errors.Add("SelfContained HttpClient needs a controlled transport");
            }
        }
        return new Result {
            Code=code,
            Types=root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(t => !t.Ancestors().OfType<BaseTypeDeclarationSyntax>().Any()).Select(t => t.Identifier.ValueText).ToArray(),
            Errors=errors.ToArray()
        };
    }
}
