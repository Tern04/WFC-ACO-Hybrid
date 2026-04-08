using System.Collections.Generic;

namespace _Project.Scripts.MapGeneration.Core
{
    /// <summary>
    /// Min-Heap implementation for the Cell class.
    /// Used to prioritize cells based on their entropy in the WFC solver.
    /// Uses tuple to freeze the priority during heap operations
    /// </summary>
    public class CellMinHeap
    {
        // (cell, priority) tuple for freezing the priority during heap operations
        private readonly List<(Cell cell, int priority)> heap;
        
        public int Count => heap.Count;
        
        /// <summary>
        /// Constructor for the CellMinHeap class.
        /// Initializes the heap with an empty list.
        /// </summary>
        public CellMinHeap()
        {
            heap = new List<(Cell, int)>();
        }

        /// <summary>
        /// Clears the heap.
        /// </summary>
        public void Clear()
        {
            heap.Clear();
        }

        /// <summary>
        /// Enqueues a cell with a given priority.
        /// </summary>
        /// <param name="cell">Cell to be enqueued</param>
        /// <param name="priority">Priority value (e.g., entropy) used for ordering in the heap</param>
        public void Enqueue(Cell cell, int priority)
        {
            heap.Add((cell, priority));
            HeapifyUp(heap.Count - 1); // Maintain heap property
        }
        
        /// <summary>
        /// Dequeues the cell with the lowest priority.
        /// </summary>
        /// <returns> Tuple of (Cell, priority) where Cell is the dequeued cell and priority
        /// is its associated priority value or (null, 0) if the heap is empty </returns>
        public (Cell cell, int priority) Dequeue()
        {
            if (heap.Count == 0)
            {
                return (null, 0);
            }

            // If there is only one element, return it and remove it from the heap
            var root = heap[0];
            if (heap.Count == 1)
            {
                heap.RemoveAt(0);
                return root;
            }
            
            // Replace the root with the last element and heapify down
            heap[0] = heap[heap.Count - 1];
            heap.RemoveAt(heap.Count - 1);
            HeapifyDown(0);

            return root;
        }
        
        /// <summary>
        /// Updates the priority of a cell in the heap.
        /// This method is used when new cell is enqueued to maintain the heap property.
        /// </summary>
        /// <param name="index">Index of the cell in the heap to update </param>
        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                
                // Compare using the frozen priority to maintain mathematical heap validity
                if (heap[index].priority < heap[parentIndex].priority)
                {
                    Swap(index, parentIndex);
                    index = parentIndex;
                }
                else
                {
                    break;
                }
            }
        }
        
        /// <summary>
        /// Heapifies the heap from the bottom up.
        /// This method is used when a cell is dequeued to maintain the heap property.
        /// </summary>
        /// <param name="index">Index of the cell in the heap to start heapifying from </param>
        private void HeapifyDown(int index)
        {
            while (true)
            {
                
                int smallest = index;
                int leftChild = 2 * index + 1;
                int rightChild = 2 * index + 2;

                if (leftChild < heap.Count && heap[leftChild].priority < heap[smallest].priority)
                    smallest = leftChild;

                if (rightChild < heap.Count && heap[rightChild].priority < heap[smallest].priority)
                    smallest = rightChild;
                
                if (smallest != index)
                {
                    Swap(index, smallest);
                    index = smallest;
                }
                else
                {
                    break;
                }
            }
        }
        
        /// <summary>
        /// Swaps two elements in the heap.
        /// </summary>
        /// <param name="i">Element to swap</param>
        /// <param name="j">Element to swap</param>
        private void Swap(int i, int j)
        {
            var temp = heap[i];
            heap[i] = heap[j];
            heap[j] = temp;
        }
    }
}