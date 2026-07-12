namespace P21.Validator.Core.Util;

public sealed class BoundedQueue<T>
{
    private readonly bool _autoShift;
    private readonly Queue<T> _queue = new();
    private readonly int _queueSize;

    public BoundedQueue(int queueSize, bool autoShift = true)
    {
        _queueSize = queueSize;
        _autoShift = autoShift;
    }

    public bool Offer(T value)
    {
        if (_queue.Count < _queueSize)
        {
            _queue.Enqueue(value);
            return true;
        }

        if (_autoShift)
        {
            _queue.Dequeue();
            _queue.Enqueue(value);
            return true;
        }

        return false;
    }

    public bool AddAll(IEnumerable<T> items)
    {
        var result = true;
        foreach (var item in items)
        {
            if (!Offer(item))
            {
                result = false;
            }
        }

        return result;
    }

    public T Element() => _queue.Peek();

    public T? Peek() => _queue.Count == 0 ? default : _queue.Peek();

    public T? Poll() => _queue.Count == 0 ? default : _queue.Dequeue();

    public void Clear() => _queue.Clear();

    public bool Contains(T value) => _queue.Contains(value);

    public bool IsEmpty() => _queue.Count == 0;

    public int Size() => _queue.Count;

    public T[] ToArray() => _queue.ToArray();

    public IEnumerator<T> GetEnumerator() => _queue.GetEnumerator();
}
