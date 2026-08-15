using System;
using System.Threading.Tasks;
using cdisc_dataset.Models;
using DynamicData;

namespace cdisc_dataset.Services.Interface;

public enum LookupKind
{
    Comment,
    Method,
    CodeList,
    Dictionary,
    Dataset,
    Variable,
    Document
}

public interface ILookupStore
{
    IObservable<IChangeSet<Comment, int>> Comments { get; }
    IObservable<IChangeSet<Method, int>> Methods { get; }
    IObservable<IChangeSet<CodeList, int>> CodeLists { get; }
    IObservable<IChangeSet<Dictionary, int>> Dictionaries { get; }
    IObservable<IChangeSet<Dataset, int>> Datasets { get; }
    IObservable<IChangeSet<Variable, int>> Variables { get; }
    IObservable<IChangeSet<Document, int>> Documents { get; }

    Task RefreshAsync(LookupKind kind);
    Task RefreshAllAsync();
}
