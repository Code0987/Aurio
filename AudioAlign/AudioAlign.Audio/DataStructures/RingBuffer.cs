﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AudioAlign.Audio.DataStructures {
    /// <summary>
    /// A generic ring buffer with a fixed size.
    /// </summary>
    class RingBuffer<T> {

        private T[] buffer;
        private int bufferSize;
        private int bufferStart;
        private int bufferFillLevel;

        /// <summary>
        /// Instantiates a new ring buffer with the given size.
        /// </summary>
        public RingBuffer(int size) {
            this.buffer = new T[size];
            this.bufferSize = size;
            this.bufferStart = 0;
            this.bufferFillLevel = 0;
        }

        /// <summary>
        /// Returns the capacity of the ring buffer.
        /// </summary>
        public int Length {
            get { return bufferSize; }
        }

        /// <summary>
        /// Returns the fill level of the ring buffer.
        /// </summary>
        public int Count {
            get { return bufferFillLevel; }
        }

        /// <summary>
        /// Adds a new element ot the ring buffer. When the buffer is already
        /// filled up to its capacity, the oldest element gets thrown away (FIFO style).
        /// </summary>
        public void Add(T data) {
            buffer[bufferStart] = data;
            bufferStart = (bufferStart + 1) % bufferSize;
            if (bufferFillLevel < bufferSize) {
                bufferFillLevel++;
            }
        }

        /// <summary>
        /// Gets an element from the ring buffer at the given index. The oldest element 
        /// is always at index 0, the newest at Count-1.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException">when the given size exceeds the fill level or the capacity</exception>
        public T this[int index] {
            get {
                if(index < 0 || index >= bufferFillLevel) {
                    throw new IndexOutOfRangeException();
                }
                int realIndex = (index + bufferStart + (bufferSize - bufferFillLevel)) % bufferSize;
                return buffer[realIndex];
            }
        }
    }
}