﻿using Microsoft.CodeAnalysis.Shared.Extensions;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Semantics;
using System.Linq;
using System;

namespace Microsoft.CodeAnalysis.PopulateSwitch
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal class PopulateSwitchDiagnosticAnalyzer : DiagnosticAnalyzer, IBuiltInAnalyzer
    {
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(FeaturesResources.Add_missing_switch_cases), FeaturesResources.ResourceManager, typeof(FeaturesResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(WorkspacesResources.PopulateSwitch), WorkspacesResources.ResourceManager, typeof(WorkspacesResources));

        private static readonly DiagnosticDescriptor s_descriptor = new DiagnosticDescriptor(IDEDiagnosticIds.PopulateSwitchDiagnosticId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Style,
            DiagnosticSeverity.Hidden,
            isEnabledByDefault: true);

        #region Interface methods

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_descriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterOperationAction(AnalyzeOperation, OperationKind.SwitchStatement);
        }

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            var switchOperation = (ISwitchStatement)context.Operation;
            var switchBlock = switchOperation.Syntax;
            var tree = switchBlock.SyntaxTree;

            bool missingCases;
            bool missingDefaultCase;
            if (SwitchIsIncomplete(switchOperation, out missingCases, out missingDefaultCase) &&
                !tree.OverlapsHiddenPosition(switchBlock.Span, context.CancellationToken))
            {
                Debug.Assert(missingCases || missingDefaultCase);
                var properties = ImmutableDictionary<string, string>.Empty
                    .Add(PopulateSwitchHelpers.MissingCases, missingCases.ToString())
                    .Add(PopulateSwitchHelpers.MissingDefaultCase, missingDefaultCase.ToString());

                var diagnostic = Diagnostic.Create(
                    s_descriptor, switchBlock.GetLocation(), properties: properties);
                context.ReportDiagnostic(diagnostic);
            }
        }

        #endregion

        private bool SwitchIsIncomplete(
            ISwitchStatement switchStatement,
            out bool missingCases, out bool missingDefaultCase)
        {
            missingDefaultCase = !PopulateSwitchHelpers.HasDefaultCase(switchStatement);
            missingCases = false;

            var switchExpression = switchStatement.Value;
            var switchExpressionType = switchExpression?.Type;

            if (switchExpressionType?.TypeKind == TypeKind.Enum)
            {
                var missingEnumMembers = PopulateSwitchHelpers.GetMissingEnumMembers(
                    switchStatement, switchExpressionType);
                missingCases = missingEnumMembers.Count > 0;
            }

            // The switch is incomplete if we're missing any cases or we're missing a default case.
            return missingDefaultCase || missingCases;
        }

        public DiagnosticAnalyzerCategory GetAnalyzerCategory() => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;
    }
}