﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.TaskList;

namespace Microsoft.CodeAnalysis.LanguageServer.Handler.Diagnostics;

internal sealed class TaskListDiagnosticSource : AbstractDocumentDiagnosticSource<Document>
{
    private static readonly ImmutableArray<string> s_todoCommentCustomTags = ImmutableArray.Create(PullDiagnosticConstants.TaskItemCustomTag);
    private static Tuple<ImmutableArray<string>, ImmutableArray<TaskListItemDescriptor>> s_lastRequestedTokens =
        Tuple.Create(ImmutableArray<string>.Empty, ImmutableArray<TaskListItemDescriptor>.Empty);

    private readonly IGlobalOptionService _globalOptions;

    public TaskListDiagnosticSource(Document document, IGlobalOptionService globalOptions)
        : base(document)
    {
        _globalOptions = globalOptions;
    }

    public override async Task<ImmutableArray<DiagnosticData>> GetDiagnosticsAsync(
        IDiagnosticAnalyzerService diagnosticAnalyzerService, RequestContext context, CancellationToken cancellationToken)
    {
        var service = this.Document.GetLanguageService<ITaskListService>();
        if (service == null)
            return ImmutableArray<DiagnosticData>.Empty;

        var options = _globalOptions.GetTaskListOptions();
        var descriptors = GetAndCacheDescriptors(options.Descriptors);

        var items = await service.GetTaskListItemsAsync(this.Document, descriptors, cancellationToken).ConfigureAwait(false);
        if (items.Length == 0)
            return ImmutableArray<DiagnosticData>.Empty;

        return items.SelectAsArray(i => new DiagnosticData(
            id: "TODO",
            category: "TODO",
            message: i.Message,
            severity: DiagnosticSeverity.Info,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            warningLevel: 0,
            customTags: s_todoCommentCustomTags,
            properties: ImmutableDictionary<string, string?>.Empty,
            projectId: this.Document.Project.Id,
            language: this.Document.Project.Language,
            location: new DiagnosticDataLocation(i.Span, this.Document.Id, mappedFileSpan: i.MappedSpan)));
    }

    private static ImmutableArray<TaskListItemDescriptor> GetAndCacheDescriptors(ImmutableArray<string> tokenList)
    {
        var lastRequested = s_lastRequestedTokens;
        if (!lastRequested.Item1.SequenceEqual(tokenList))
        {
            var descriptors = TaskListItemDescriptor.Parse(tokenList);
            lastRequested = Tuple.Create(tokenList, descriptors);
            s_lastRequestedTokens = lastRequested;
        }

        return lastRequested.Item2;
    }
}
