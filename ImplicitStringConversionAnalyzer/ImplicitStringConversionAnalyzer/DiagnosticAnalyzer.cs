using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ImplicitStringConversionAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ImplicitStringConversionAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ImplicitStringConversionAnalyzer";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSemanticModelAction(AnalyzeSemanticModel);
        }

        private void AnalyzeSemanticModel(SemanticModelAnalysisContext context)
        {
            var objectType = context.SemanticModel.Compilation.GetSpecialType(SpecialType.System_Object);
            var stringType = context.SemanticModel.Compilation.GetSpecialType(SpecialType.System_String);
            var objectToStringMembers = objectType.GetMembers("ToString");
            var binaryAddExpressions = context.SemanticModel.SyntaxTree.GetRoot().DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>().Where(node => node.Kind() == SyntaxKind.AddExpression);

            foreach (var binaryAddExpression in binaryAddExpressions)
            {
                var left = context.SemanticModel.GetTypeInfo(binaryAddExpression.Left);
                var right = context.SemanticModel.GetTypeInfo(binaryAddExpression.Right);
                
                if (Equals(left.Type, stringType) && !Equals(right.Type, stringType) && right.Type.IsReferenceType && TypeHasOverridenToString(right, objectToStringMembers))
                {
                    var diagnostic = Diagnostic.Create(Rule, binaryAddExpression.Right.GetLocation(), binaryAddExpression.Right.ToString());

                    context.ReportDiagnostic(diagnostic);
                }

                if (!Equals(left.Type, stringType) && Equals(right.Type, stringType) && left.Type.IsReferenceType && TypeHasOverridenToString(left, objectToStringMembers))
                {
                    var diagnostic = Diagnostic.Create(Rule, binaryAddExpression.Left.GetLocation(), binaryAddExpression.Left.ToString());

                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private static bool TypeHasOverridenToString(TypeInfo right, ImmutableArray<ISymbol> objectToStringMembers)
        {
            return !right.Type.GetMembers("ToString").Except(objectToStringMembers).Any();
        }
    }
}
