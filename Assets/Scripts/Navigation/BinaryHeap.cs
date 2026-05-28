using System.Collections.Generic;

public class BinaryHeap<T>
{
    struct Node
    {
        public T item;
        public float priority;
    }

    Node[] items;
    int count;

    public int Count => count;

    public BinaryHeap(int capacity = 64)
    {
        items = new Node[capacity];
        count = 0;
    }

    public void Clear()
    {
        count = 0;
    }

    public void Enqueue(T item, float priority)
    {
        if (count == items.Length)
        {
            System.Array.Resize(ref items, items.Length * 2);
        }
        items[count] = new Node { item = item, priority = priority };
        SiftUp(count);
        count++;
    }

    public T Dequeue()
    {
        T result = items[0].item;
        count--;
        if (count > 0)
        {
            items[0] = items[count];
            SiftDown(0);
        }
        items[count] = default;
        return result;
    }

    public T Peek() => items[0].item;

    void SiftUp(int i)
    {
        while (i > 0)
        {
            int parent = (i - 1) >> 1;
            if (items[i].priority < items[parent].priority)
            {
                (items[i], items[parent]) = (items[parent], items[i]);
                i = parent;
            }
            else break;
        }
    }

    void SiftDown(int i)
    {
        while (true)
        {
            int left = (i << 1) + 1;
            int right = left + 1;
            int smallest = i;

            if (left  < count && items[left].priority  < items[smallest].priority) smallest = left;
            if (right < count && items[right].priority < items[smallest].priority) smallest = right;

            if (smallest == i) break;
            (items[i], items[smallest]) = (items[smallest], items[i]);
            i = smallest;
        }
    }
}
