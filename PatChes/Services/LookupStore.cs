using System;
using System.Threading;
using System.Threading.Tasks;
using PatChes.Extensions;
using PatChes.Models;
using PatChes.Services.Interface;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;

namespace PatChes.Services;

public sealed class LookupStore : ILookupStore
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private readonly SourceCache<Comment, int> _comments = new(o => o.Id);
    private readonly SourceCache<Method, int> _methods = new(o => o.Id);
    private readonly SourceCache<CodeList, int> _codeLists = new(o => o.Id);
    private readonly SourceCache<Dictionary, int> _dictionaries = new(o => o.Id);
    private readonly SourceCache<Dataset, int> _datasets = new(o => o.Id);
    private readonly SourceCache<Document, int> _documents = new(o => o.Id);

    public IObservable<IChangeSet<Comment, int>> Comments => _comments.Connect();
    public IObservable<IChangeSet<Method, int>> Methods => _methods.Connect();
    public IObservable<IChangeSet<CodeList, int>> CodeLists => _codeLists.Connect();
    public IObservable<IChangeSet<Dictionary, int>> Dictionaries => _dictionaries.Connect();
    public IObservable<IChangeSet<Dataset, int>> Datasets => _datasets.Connect();
    public IObservable<IChangeSet<Document, int>> Documents => _documents.Connect();

    public void UpsertMethod(Method method)
    {
        _methods.AddOrUpdate(method);
    }

    public void RemoveMethod(int methodId)
    {
        _methods.RemoveKey(methodId);
    }

    public LookupStore(
        IServiceProvider serviceProvider,
        ICurrentProjectService currentProjectService)
    {
        _serviceProvider = serviceProvider;

        currentProjectService.Changed += () => RefreshAllAsync().AwaitWithOpt();
    }

    public async Task RefreshAsync(LookupKind kind)
    {
        await _refreshGate.WaitAsync();
        try
        {
            await RefreshCoreAsync(kind);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task RefreshAllAsync()
    {
        await _refreshGate.WaitAsync();
        try
        {
            await RefreshCoreAsync(LookupKind.Comment);
            await RefreshCoreAsync(LookupKind.Method);
            await RefreshCoreAsync(LookupKind.CodeList);
            await RefreshCoreAsync(LookupKind.Dictionary);
            await RefreshCoreAsync(LookupKind.Dataset);
            await RefreshCoreAsync(LookupKind.Document);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync(LookupKind kind)
    {
        switch (kind)
        {
            case LookupKind.Comment:
            {
                var items = await _serviceProvider.GetRequiredService<ICommentService>().GetAllCommentsAsync();
                _comments.Edit(cache =>
                {
                    cache.Clear();
                    cache.AddOrUpdate(items);
                });
                break;
            }
            case LookupKind.Method:
            {
                var items = await _serviceProvider.GetRequiredService<IMethodService>().GetAllMethodsWithoutErorrAsync();
                _methods.Edit(cache =>
                {
                    cache.Clear();
                    cache.AddOrUpdate(items);
                });
                break;
            }
            case LookupKind.CodeList:
            {
                var items = await _serviceProvider.GetRequiredService<ICodeListService>().GetAllCodeListsWithoutErorrAsync();
                _codeLists.Edit(cache =>
                {
                    cache.Clear();
                    cache.AddOrUpdate(items);
                });
                break;
            }
            case LookupKind.Dictionary:
            {
                var items = await _serviceProvider.GetRequiredService<IDictionaryService>().GetAllDictionariesWithoutErorrAsync();
                _dictionaries.Edit(cache =>
                {
                    cache.Clear();
                    cache.AddOrUpdate(items);
                });
                break;
            }
            case LookupKind.Dataset:
            {
                var items = await _serviceProvider.GetRequiredService<IDatasetService>().GetAllDatasetsWithoutErrorAsync();
                _datasets.Edit(cache =>
                {
                    cache.Clear();
                    cache.AddOrUpdate(items);
                });
                break;
            }
            case LookupKind.Document:
            {
                var items = await _serviceProvider.GetRequiredService<IDocumentService>().GetAllDocumentsWithoutErorrAsync();
                _documents.Edit(cache =>
                {
                    cache.Clear();
                    cache.AddOrUpdate(items);
                });
                break;
            }
        }
    }
}
