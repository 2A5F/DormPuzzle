using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DormPuzzle.Helpers;

public class FuckedObservableCollection<T>(List<T> items) : IList<T>, IReadOnlyList<T>, INotifyCollectionChanged, INotifyPropertyChanged
{
    public readonly List<T> items = items;
    private int _blockReentrancyCount;

    public FuckedObservableCollection() : this(new List<T>()) { }
    public FuckedObservableCollection(IEnumerable<T> items) : this(items.ToList()) { }
    public FuckedObservableCollection(ParallelQuery<T> items) : this(items.ToList()) { }

    List<T>.Enumerator GetEnumerator() => items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

    public void Add(T item)
    {
        CheckReentrancy();
        var index = items.Count;
        items.Add(item);
        OnCountPropertyChanged();
        OnIndexerPropertyChanged();
        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
    }

    public bool IsFixedSize => false;

    public void Clear()
    {
        CheckReentrancy();
        items.Clear();
        OnCountPropertyChanged();
        OnIndexerPropertyChanged();
        OnCollectionReset();
    }

    public bool Contains(T item) => items.Contains(item);

    public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

    public bool Remove(T item)
    {
        CheckReentrancy();
        var index = items.IndexOf(item);

        if (index < 0) return false;
        
        items.RemoveAt(index);

        OnCountPropertyChanged();
        OnIndexerPropertyChanged();
        OnCollectionChanged(NotifyCollectionChangedAction.Remove, item, index);

        return true;
    }

    public int Count => items.Count;

    public bool IsReadOnly => false;

    public bool IsSynchronized => false;

    public object SyncRoot => this;

    public int IndexOf(T item) => items.IndexOf(item);

    public void Insert(int index, T item)
    {
        CheckReentrancy();
        items.Insert(index, item);
        OnCountPropertyChanged();
        OnIndexerPropertyChanged();
        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
    }

    public void RemoveAt(int index)
    {
        CheckReentrancy();
        var removedItem = this[index];

        items.RemoveAt(index);

        OnCountPropertyChanged();
        OnIndexerPropertyChanged();
        OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
    }

    public T this[int index]
    {
        get => items[index];
        set
        {
            CheckReentrancy();
            var old = items[index];
            items[index] = value;
            OnIndexerPropertyChanged();
            OnCollectionChanged(NotifyCollectionChangedAction.Replace, old, value, index);
        }
    }

    #region Observable

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void CheckReentrancy()
    {
        if (_blockReentrancyCount > 0)
        {
            if (CollectionChanged?.GetInvocationList().Length > 1)
                throw new InvalidOperationException("ObservableCollectionReentrancyNotAllowed");
        }
    }

    private void OnCountPropertyChanged() => OnPropertyChanged(EventArgsCache.CountPropertyChanged);

    private void OnIndexerPropertyChanged() => OnPropertyChanged(EventArgsCache.IndexerPropertyChanged);
    private void OnCollectionReset() => OnCollectionChanged(EventArgsCache.ResetCollectionChanged);

    protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }

    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        NotifyCollectionChangedEventHandler? handler = CollectionChanged;
        if (handler != null)
        {
            // Not calling BlockReentrancy() here to avoid the SimpleMonitor allocation.
            _blockReentrancyCount++;
            try
            {
                handler(this, e);
            }
            finally
            {
                _blockReentrancyCount--;
            }
        }
    }
    private void OnCollectionChanged(NotifyCollectionChangedAction action, object? item, int index)
    {
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index));
    }

    private void OnCollectionChanged(NotifyCollectionChangedAction action, object? item, int index, int oldIndex)
    {
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, item, index, oldIndex));
    }

    private void OnCollectionChanged(NotifyCollectionChangedAction action, object? oldItem, object? newItem, int index)
    {
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(action, newItem, oldItem, index));
    }

    #endregion
}

internal static class EventArgsCache
{
    internal static readonly PropertyChangedEventArgs CountPropertyChanged = new PropertyChangedEventArgs("Count");
    internal static readonly PropertyChangedEventArgs IndexerPropertyChanged = new PropertyChangedEventArgs("Item[]");
    internal static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged = new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
}
