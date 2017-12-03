using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace MyLittleAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MyLittleAnalyzerCodeFixProvider)), Shared]
    public class MyLittleAnalyzerCodeFixProvider : CodeFixProvider
    {
        private const string title = "Add antiforgery token";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(MyLittleAnalyzerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            // TODO: Replace the following code with your own analysis, generating a CodeAction for each fix to suggest
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedSolution: c => AddAntiForgeryAsync(context.Document, declaration, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Solution> AddAntiForgeryAsync(Document document, MethodDeclarationSyntax methodDecl, CancellationToken cancellationToken)
        {
            // Get the symbol representing the method where we should add antiforgerytoken.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl, cancellationToken);

            var attributes = methodDecl.AttributeLists.Add(
                SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList<AttributeSyntax>(
                    SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("ValidateAntiForgeryToken"))
                )).WithTriviaFrom(methodDecl));
            var root = await document.GetSyntaxRootAsync(cancellationToken);

            var newSolution = document.WithSyntaxRoot(
                root.ReplaceNode(
                    methodDecl,
                    methodDecl.WithAttributeLists(attributes)
                )).Project.Solution;

            return newSolution;
        }
    }
}