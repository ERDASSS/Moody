namespace Database;

// todo: может переместить в отдельный проект для низкоуровневых инструментов

public class DefaultDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    where TKey : notnull
    where TValue : new()
{
    public new TValue this[TKey key]
    {
        get
        {
            if (!ContainsKey(key))
                base[key] = new TValue();
            return base[key];
        }
    }
}