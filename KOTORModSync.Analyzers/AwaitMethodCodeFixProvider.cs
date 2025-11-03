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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AwaitMethodCodeFixProvider)), Shared]
    public class AwaitMethodCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Await method call";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create("S6966");

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

            // Find the invocation expression
            var token = root.FindToken(diagnosticSpan.Start);
            var invocation = token.Parent?.AncestorsAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .FirstOrDefault();

            if (invocation is null)
            {
                return;
            }

            // Check if already awaited
            var parentExpression = invocation.Parent;
            if (parentExpression is AwaitExpressionSyntax)
            {
                return;
            }

            // Check if it's part of an expression statement (e.g., "Logger.Log(msg);")
            // In that case, we need to await the statement, not just wrap the invocation
            bool isExpressionStatement = parentExpression is ExpressionStatementSyntax;

            // Register the code fix
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => AddAwaitAsync(context.Document, invocation, isExpressionStatement, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private static async Task<Document> AddAwaitAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            bool isExpressionStatement,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
            {
                return document;
            }

            // Create await expression
            var awaitExpression = SyntaxFactory.AwaitExpression(invocation);

            // Check if we need to make the containing method async
            var containingMethod = invocation.Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            SyntaxNode newRoot = root;

            // If it's an expression statement, replace the whole statement
            if (isExpressionStatement && invocation.Parent is ExpressionStatementSyntax statement)
            {
                var awaitStatement = statement.WithExpression(awaitExpression);
                newRoot = newRoot.ReplaceNode(statement, awaitStatement);
            }
            else
            {
                // Otherwise, just replace the invocation with await invocation
                newRoot = newRoot.ReplaceNode(invocation, awaitExpression);
            }

            // Then, if we need to make the method async, update it
            if (containingMethod is not null)
            {
                // Get the updated method from the new root
                var updatedMethod = newRoot.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Span.Start == containingMethod.Span.Start);

                if (updatedMethod is not null)
                {
                    var methodSymbol = semanticModel.GetDeclaredSymbol(containingMethod, cancellationToken);
                    if (methodSymbol is not null && !methodSymbol.IsAsync)
                    {
                        // Make the method async
                        var newMethod = MakeMethodAsync(updatedMethod, semanticModel, cancellationToken);
                        newRoot = newRoot.ReplaceNode(updatedMethod, newMethod);
                    }
                }
            }

            return document.WithSyntaxRoot(newRoot);
        }

        private static MethodDeclarationSyntax MakeMethodAsync(
            MethodDeclarationSyntax method,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
        {
            // Add async modifier if not present
            if (method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)))
            {
                return method; // Already async
            }

            // Create async modifier token
            var asyncToken = SyntaxFactory.Token(SyntaxKind.AsyncKeyword);

            // Add async modifier before other modifiers
            var newModifiers = method.Modifiers.Insert(0, asyncToken);

            // Change return type to Task or Task<T> if needed
            var returnType = method.ReturnType;
            var methodSymbol = semanticModel.GetDeclaredSymbol(method, cancellationToken);
            if (methodSymbol is null)
            {
                return method.WithModifiers(newModifiers);
            }

            // If return type is void, change to Task
            if (methodSymbol.ReturnsVoid)
            {
                var taskType = SyntaxFactory.IdentifierName("Task");
                return method
                    .WithModifiers(newModifiers)
                    .WithReturnType(taskType);
            }

            // If return type is T, change to Task<T>
            if (returnType is not GenericNameSyntax)
            {
                var taskOfT = SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier("Task"))
                    .WithTypeArgumentList(
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(returnType)));
                return method
                    .WithModifiers(newModifiers)
                    .WithReturnType(taskOfT);
            }

            // Already has a return type that might be Task, just add async
            return method.WithModifiers(newModifiers);
        }
    }
}
