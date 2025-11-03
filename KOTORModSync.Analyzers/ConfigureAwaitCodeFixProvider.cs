// Copyright 2021-2025 KOTORModSync
// Licensed under the Business Source License 1.1 (BSL 1.1).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace KOTORModSync.Analyzers
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConfigureAwaitCodeFixProvider)), Shared]
    public class ConfigureAwaitCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Add ConfigureAwait(false)";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("MA0004");

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }

            var diagnostic = context.Diagnostics.FirstOrDefault();
            if (diagnostic is null)
            {
                return;
            }

            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the await expression
            var token = root.FindToken(diagnosticSpan.Start);
            if (token.Parent is not AwaitExpressionSyntax awaitExpression)
            {
                return;
            }

            // Check if ConfigureAwait already exists
            if (HasConfigureAwait(awaitExpression))
            {
                return;
            }

            // Register the code fix
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => AddConfigureAwaitAsync(context.Document, awaitExpression, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private static bool HasConfigureAwait(AwaitExpressionSyntax awaitExpression)
        {
            // Check if the expression already has ConfigureAwait
            var expression = awaitExpression.Expression;
            while (expression is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Name.Identifier.ValueText == "ConfigureAwait")
                {
                    return true;
                }

                expression = memberAccess.Expression;
            }

            return false;
        }

        private static async Task<Document> AddConfigureAwaitAsync(
            Document document,
            AwaitExpressionSyntax awaitExpression,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            // Create the ConfigureAwait(false) invocation
            var configureAwaitExpression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    awaitExpression.Expression,
                    SyntaxFactory.IdentifierName("ConfigureAwait")))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression)))));

            // Replace the await expression with await expression.ConfigureAwait(false)
            var newAwaitExpression = awaitExpression.WithExpression(configureAwaitExpression);

            var newRoot = root.ReplaceNode(awaitExpression, newAwaitExpression);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}
